using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using FFMpegCore;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.MediaFiles.MediaInfo
{
    public interface IVideoFileInfoReader
    {
        MediaInfoModel GetMediaInfo(string filename);
        TimeSpan? GetRunTime(string filename);
        MediaInfoProbeResult ProbeMediaInfo(string filename);
    }

    public class VideoFileInfoReader : IVideoFileInfoReader
    {
        // A file that cannot be probed is remembered (keyed on path, size and last write time) and
        // only retried up to the configured number of attempts, so a permanently unreadable file
        // does not get re-probed by every disk scan forever. The entry is forgotten when the file
        // changes, and expires on its own so a transcode/re-download is picked up.
        private static readonly TimeSpan ProbeFailureLifetime = TimeSpan.FromHours(24);

        private readonly IDiskProvider _diskProvider;
        private readonly IConfigService _configService;
        private readonly ICached<ProbeFailureEntry> _probeFailures;
        private readonly Logger _logger;

        public const int MINIMUM_MEDIA_INFO_SCHEMA_REVISION = 14;
        public const int CURRENT_MEDIA_INFO_SCHEMA_REVISION = 14;

        private static readonly string[] ValidHdrColourPrimaries = { "bt2020" };
        private static readonly string[] HlgTransferFunctions = { "arib-std-b67" };
        private static readonly string[] PqTransferFunctions = { "smpte2084" };
        private static readonly string[] ValidHdrTransferFunctions = HlgTransferFunctions.Concat(PqTransferFunctions).ToArray();

        private class ProbeFailureEntry
        {
            public int Attempts { get; set; }
            public string Error { get; set; }
        }

        public VideoFileInfoReader(IDiskProvider diskProvider, IConfigService configService, ICacheManager cacheManager, Logger logger)
        {
            _diskProvider = diskProvider;
            _configService = configService;
            _probeFailures = cacheManager.GetCache<ProbeFailureEntry>(GetType(), "probeFailures");
            _logger = logger;

            // We bundle ffprobe for all platforms
            GlobalFFOptions.Configure(options => options.BinaryFolder = AppDomain.CurrentDomain.BaseDirectory);
        }

        public MediaInfoModel GetMediaInfo(string filename)
        {
            return ProbeMediaInfo(filename).MediaInfo;
        }

        public TimeSpan? GetRunTime(string filename)
        {
            return ProbeMediaInfo(filename).MediaInfo?.RunTime;
        }

        public MediaInfoProbeResult ProbeMediaInfo(string filename)
        {
            if (!_diskProvider.FileExists(filename))
            {
                throw new FileNotFoundException("Media file does not exist: " + filename);
            }

            if (MediaFileExtensions.DiskExtensions
                .Concat(MediaFileExtensions.StreamingExtensions)
                .Contains(Path.GetExtension(filename)))
            {
                return new MediaInfoProbeResult { Error = "the file type is not supported for media probing" };
            }

            // Remember failed probes per path + size + mtime and stop retrying after the configured
            // number of attempts, so a corrupt/unreadable file is not ffprobed again on every scan.
            var cacheKey = GetProbeCacheKey(filename);
            var maxAttempts = Math.Max(1, _configService.MediaInfoProbeMaxAttempts);
            var previousFailure = _probeFailures.Find(cacheKey);

            if (previousFailure != null && previousFailure.Attempts >= maxAttempts)
            {
                _logger.Debug("Skipping media probe for '{0}' after {1} failed attempts: {2}", filename, previousFailure.Attempts, previousFailure.Error);

                return new MediaInfoProbeResult { Error = previousFailure.Error };
            }

            try
            {
                _logger.Debug("Getting media info from {0}", filename);

                var analysis = FFProbe.Analyse(filename, customArguments: "-probesize 50000000");

                if (analysis.PrimaryAudioStream?.ChannelLayout.IsNullOrWhiteSpace() ?? true)
                {
                    analysis = FFProbe.Analyse(filename, customArguments: "-probesize 150000000 -analyzeduration 150000000");
                }

                var primaryVideoStream = GetPrimaryVideoStream(analysis);
                var primaryVideoStreamIndex = GetPrimaryVideoStreamIndex(analysis);

                var mediaInfoModel = new MediaInfoModel();
                mediaInfoModel.ContainerFormat = analysis.Format.FormatName;
                mediaInfoModel.VideoFormat = primaryVideoStream?.CodecName;
                mediaInfoModel.VideoCodecID = primaryVideoStream?.CodecTagString;
                mediaInfoModel.VideoProfile = primaryVideoStream?.Profile;
                mediaInfoModel.VideoMultiViewCount = GetMultiViewCount(primaryVideoStream?.Tags);
                mediaInfoModel.PrimaryVideoStreamIndex = primaryVideoStreamIndex;
                mediaInfoModel.VideoBitrate = GetBitrate(primaryVideoStream);

                var pixelFormatComponents = GetPixelFormat(primaryVideoStream?.PixelFormat)?.Components;

                mediaInfoModel.VideoBitDepth = pixelFormatComponents != null && pixelFormatComponents.Count > 0
                    ? pixelFormatComponents.Min(x => x.BitDepth)
                    : 8;
                mediaInfoModel.VideoColourPrimaries = primaryVideoStream?.ColorPrimaries;
                mediaInfoModel.VideoTransferCharacteristics = primaryVideoStream?.ColorTransfer;
                mediaInfoModel.Height = primaryVideoStream?.Height ?? 0;
                mediaInfoModel.Width = primaryVideoStream?.Width ?? 0;
                mediaInfoModel.RunTime = GetBestRuntime(analysis.PrimaryAudioStream?.Duration, primaryVideoStream?.Duration, analysis.Format.Duration);
                mediaInfoModel.VideoFps = primaryVideoStream?.FrameRate ?? 0;
                mediaInfoModel.ScanType = primaryVideoStream?.FieldOrder switch
                {
                    "tt" or "bb" or "tb" or "bt" => "Interlaced",
                    _ => "Progressive"
                };
                mediaInfoModel.RawStreamData = string.Concat(analysis.OutputData);

                var primaryAudioStream = analysis.PrimaryAudioStream;

                // MOVIES-only flat audio fields, derived from the probe so movie consumers
                // (import specs, movie media-info API, script/metadata exports) keep working.
                mediaInfoModel.AudioFormat = primaryAudioStream?.CodecName;
                mediaInfoModel.AudioCodecID = primaryAudioStream?.CodecTagString;
                mediaInfoModel.AudioProfile = primaryAudioStream?.Profile;
                mediaInfoModel.AudioBitrate = GetBitrate(primaryAudioStream);
                mediaInfoModel.AudioStreamCount = analysis.AudioStreams?.Count ?? 0;
                mediaInfoModel.AudioChannels = primaryAudioStream?.Channels ?? 0;
                mediaInfoModel.AudioChannelPositions = primaryAudioStream?.ChannelLayout;

                mediaInfoModel.AudioStreams = analysis.AudioStreams?
                    .OrderBy(stream => stream.Index)
                    .Select(stream =>
                    {
                        var model = new MediaInfoAudioStreamModel
                        {
                            Language = stream.Language.IsNotNullOrWhiteSpace() ? stream.Language : "und",
                            Format = stream.CodecName,
                            CodecId = stream.CodecTagString,
                            Profile = stream.Profile,
                            Bitrate = GetBitrate(stream),
                            Channels = stream.Channels,
                            ChannelPositions = stream.ChannelLayout
                        };

                        if ((stream.Tags?.TryGetValue("title", out var audioTitle) ?? false) && audioTitle.IsNotNullOrWhiteSpace())
                        {
                            model.Title = audioTitle.Trim();
                        }
                        else if ((stream.Tags?.TryGetValue("name", out var audioName) ?? false) && audioName.IsNotNullOrWhiteSpace())
                        {
                            model.Title = audioName.Trim();
                        }

                        return  model;
                    })
                    .ToList();

                mediaInfoModel.SubtitleStreams = analysis.SubtitleStreams?
                    .Where(stream => stream.Language.IsNotNullOrWhiteSpace())
                    .OrderBy(stream => stream.Index)
                    .Select(stream =>
                    {
                        var model = new MediaInfoSubtitleStreamModel
                        {
                            Language = stream.Language,
                            Format = stream.CodecName,
                        };

                        if ((stream.Tags?.TryGetValue("title", out var subtitleTitle) ?? false) && subtitleTitle.IsNotNullOrWhiteSpace())
                        {
                            model.Title = subtitleTitle.Trim();
                        }
                        else if ((stream.Tags?.TryGetValue("name", out var subtitleName) ?? false) && subtitleName.IsNotNullOrWhiteSpace())
                        {
                            model.Title = subtitleName.Trim();
                        }

                        if (stream.Disposition?.TryGetValue("forced", out var forcedSubtitle) ?? false)
                        {
                            model.Forced = forcedSubtitle;
                        }

                        if (stream.Disposition?.TryGetValue("hearing_impaired", out var hearingImpairedSubtitle) ?? false)
                        {
                            model.HearingImpaired = hearingImpairedSubtitle;
                        }

                        return  model;
                    })
                    .ToList();

                mediaInfoModel.AudioLanguages = analysis.AudioStreams?
                    .Select(stream => stream.Language)
                    .Where(language => language.IsNotNullOrWhiteSpace())
                    .ToList();

                mediaInfoModel.Subtitles = analysis.SubtitleStreams?
                    .Select(stream => stream.Language)
                    .Where(language => language.IsNotNullOrWhiteSpace())
                    .ToList();

                mediaInfoModel.SchemaRevision = CURRENT_MEDIA_INFO_SCHEMA_REVISION;

                if (analysis.Format.Tags?.TryGetValue("title", out var title) ?? false)
                {
                    mediaInfoModel.Title = title;
                }

                FFProbeFrames frames = null;

                // if it looks like PQ10 or similar HDR, do a frame analysis to figure out which type it is
                if (PqTransferFunctions.Contains(mediaInfoModel.VideoTransferCharacteristics))
                {
                    frames = FFProbe.GetFrames(filename, customArguments: $"-read_intervals \"%+#1\" -select_streams v:{mediaInfoModel.PrimaryVideoStreamIndex}");
                }

                var streamSideData = primaryVideoStream?.SideData ?? new();
                var framesSideData = frames?.Frames.FirstOrDefault()?.SideData ?? new();

                var sideData = streamSideData.Concat(framesSideData).ToList();
                mediaInfoModel.VideoHdrFormat = GetHdrFormat(mediaInfoModel.VideoBitDepth, mediaInfoModel.VideoColourPrimaries, mediaInfoModel.VideoTransferCharacteristics, sideData);

                _logger.Debug("Media info for '{0}': container={1}, video={2} {3}x{4} {5}fps, audioStreams={6} [{7}], subtitleStreams={8} [{9}]",
                    filename,
                    mediaInfoModel.ContainerFormat,
                    mediaInfoModel.VideoFormat,
                    mediaInfoModel.Width,
                    mediaInfoModel.Height,
                    mediaInfoModel.VideoFps,
                    mediaInfoModel.AudioStreams?.Count ?? 0,
                    mediaInfoModel.AudioStreams == null ? string.Empty : string.Join(", ", mediaInfoModel.AudioStreams.Select(a => $"{a.Format}/{a.Channels}ch/{a.Language}")),
                    mediaInfoModel.SubtitleStreams?.Count ?? 0,
                    mediaInfoModel.SubtitleStreams == null ? string.Empty : string.Join(", ", mediaInfoModel.SubtitleStreams.Select(s => s.Language)));

                _probeFailures.Remove(cacheKey);

                return new MediaInfoProbeResult { MediaInfo = mediaInfoModel };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to parse media info from file: {0}", filename);

                var error = GetProbeErrorMessage(filename, ex);

                RecordProbeFailure(cacheKey, error);

                return new MediaInfoProbeResult { Error = error };
            }
        }

        private string GetProbeCacheKey(string filename)
        {
            try
            {
                var size = _diskProvider.GetFileSize(filename);
                var lastWrite = _diskProvider.FileGetLastWrite(filename).Ticks;

                return $"{filename}|{size}|{lastWrite}";
            }
            catch
            {
                // If the file cannot be stat'ed, key on the path alone; the fault guard on the
                // probe itself still applies.
                return filename;
            }
        }

        private void RecordProbeFailure(string cacheKey, string error)
        {
            var entry = _probeFailures.Find(cacheKey) ?? new ProbeFailureEntry();

            entry.Attempts++;
            entry.Error = error;

            _probeFailures.Set(cacheKey, entry, ProbeFailureLifetime);
        }

        private static string GetProbeErrorMessage(string filename, Exception ex)
        {
            var message = ex.Message;

            if (message.IsNullOrWhiteSpace())
            {
                return null;
            }

            // FFMpegCore concatenates ffprobe's stderr into the message; the last line names the
            // actual cause and is prefixed with the file path we already report separately.
            var reason = message.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                .Select(line => line.Trim())
                                .LastOrDefault()?
                                .TrimEnd(')')
                                .Trim();

            if (reason.IsNullOrWhiteSpace())
            {
                return null;
            }

            if (reason.StartsWith(filename, StringComparison.Ordinal))
            {
                reason = reason[filename.Length..].TrimStart(':', ' ').Trim();
            }

            return reason;
        }

        private static TimeSpan GetBestRuntime(TimeSpan? audio, TimeSpan? video, TimeSpan general)
        {
            if (!video.HasValue || video.Value.TotalMilliseconds == 0)
            {
                if (!audio.HasValue || audio.Value.TotalMilliseconds == 0)
                {
                    return general;
                }

                return audio.Value;
            }

            return video.Value;
        }

        private static long GetBitrate(MediaStream mediaStream)
        {
            if (mediaStream?.BitRate is > 0)
            {
                return mediaStream.BitRate;
            }

            if ((mediaStream?.Tags?.TryGetValue("BPS", out var bitratePerSecond) ?? false) && bitratePerSecond.IsNotNullOrWhiteSpace())
            {
                return Convert.ToInt64(bitratePerSecond);
            }

            return 0;
        }

        // The index of the chosen primary stream among the file's video streams, i.e. the N in
        // "-map 0:v:N". A leading cover/motion-image stream is skipped so the transcoder never
        // encodes the cover instead of the feature.
        public static int GetPrimaryVideoStreamIndex(IMediaAnalysis mediaAnalysis)
        {
            var primaryVideoStream = GetPrimaryVideoStream(mediaAnalysis);

            if (primaryVideoStream == null || mediaAnalysis?.VideoStreams == null)
            {
                return 0;
            }

            var index = mediaAnalysis.VideoStreams.FindIndex(stream => stream.Index == primaryVideoStream.Index);

            return index < 0 ? 0 : index;
        }

        public static VideoStream GetPrimaryVideoStream(IMediaAnalysis mediaAnalysis)
        {
            if (mediaAnalysis?.VideoStreams == null || mediaAnalysis.VideoStreams.Count <= 1)
            {
                return mediaAnalysis?.PrimaryVideoStream ?? mediaAnalysis?.VideoStreams?.FirstOrDefault();
            }

            // Prefer a stream that is neither flagged as an attached picture nor an image codec: the
            // disposition is the authoritative cover marker, but some containers only reveal it
            // through the codec (bmp/webp/gif on top of the classic mjpeg/png), so both are excluded.
            var realVideo = mediaAnalysis.VideoStreams.FirstOrDefault(stream => !IsCoverStream(stream));

            return realVideo
                ?? mediaAnalysis.VideoStreams.FirstOrDefault(stream => !IsAttachedPicture(stream))
                ?? mediaAnalysis.PrimaryVideoStream;
        }

        private static bool IsCoverStream(MediaStream stream)
        {
            if (IsAttachedPicture(stream))
            {
                return true;
            }

            var codecFilter = new[] { "mjpeg", "png", "bmp", "webp", "gif" };

            return codecFilter.Contains(stream?.CodecName, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsAttachedPicture(MediaStream stream)
        {
            return stream?.Disposition != null &&
                   stream.Disposition.TryGetValue("attached_pic", out var attachedPicture) &&
                   attachedPicture;
        }

        private static IReadOnlyList<FFProbePixelFormat> _pixelFormats;

        private static FFProbePixelFormat GetPixelFormat(string format)
        {
            if (format.IsNullOrWhiteSpace())
            {
                return null;
            }

            if (FFProbe.TryGetPixelFormat(format, out var pixelFormat) && pixelFormat != null)
            {
                return pixelFormat;
            }

            // The library lookup is case-sensitive; fall back to a case-insensitive search so a
            // pixel format reported with different casing (e.g. "YUV420P") still resolves.
            try
            {
                _pixelFormats ??= FFProbe.GetPixelFormats().PixelFormats;
            }
            catch (Exception)
            {
                _pixelFormats = Array.Empty<FFProbePixelFormat>();
            }

            return _pixelFormats.FirstOrDefault(x => x.Name.Equals(format, StringComparison.OrdinalIgnoreCase));
        }

        public static int GetMultiViewCount(IReadOnlyDictionary<string, string> videoTags)
        {
            return (videoTags?.ContainsKey("stereo_mode") ?? false) ? 2 : 1;
        }

        public static HdrFormat GetHdrFormat(int bitDepth, string colorPrimaries, string transferFunction, List<Dictionary<string, JsonNode>> sideData)
        {
            if (bitDepth < 10)
            {
                return HdrFormat.None;
            }

            if (TryGetSideData(sideData, FFMpegCoreSideDataTypes.DoviConfigurationRecordSideData, out var dovi))
            {
                var hasHdr10Plus = TryGetSideData(sideData, FFMpegCoreSideDataTypes.HdrDynamicMetadataSpmte2094, out _);

                dovi.TryGetValue("dv_bl_signal_compatibility_id", out var dvBlSignalCompatibilityId);

                return dvBlSignalCompatibilityId?.GetValue<int>() switch
                {
                    1 => hasHdr10Plus ? HdrFormat.DolbyVisionHdr10Plus : HdrFormat.DolbyVisionHdr10,
                    2 => HdrFormat.DolbyVisionSdr,
                    4 => HdrFormat.DolbyVisionHlg,
                    6 => hasHdr10Plus ? HdrFormat.DolbyVisionHdr10Plus : HdrFormat.DolbyVisionHdr10,
                    _ => HdrFormat.DolbyVision
                };
            }

            if (!ValidHdrColourPrimaries.Contains(colorPrimaries) || !ValidHdrTransferFunctions.Contains(transferFunction))
            {
                return HdrFormat.None;
            }

            if (HlgTransferFunctions.Contains(transferFunction))
            {
                return HdrFormat.Hlg10;
            }

            if (PqTransferFunctions.Contains(transferFunction))
            {
                if (TryGetSideData(sideData, FFMpegCoreSideDataTypes.HdrDynamicMetadataSpmte2094, out _))
                {
                    return HdrFormat.Hdr10Plus;
                }

                if (TryGetSideData(sideData, FFMpegCoreSideDataTypes.MasteringDisplayMetadata, out _) ||
                    TryGetSideData(sideData, FFMpegCoreSideDataTypes.ContentLightLevelMetadata, out _))
                {
                    return HdrFormat.Hdr10;
                }

                return HdrFormat.Pq10;
            }

            return HdrFormat.None;
        }

        private static bool TryGetSideData(IReadOnlyList<Dictionary<string, JsonNode>> list, string name, out Dictionary<string, JsonNode> result)
        {
            result = list?.FirstOrDefault(item =>
                item.TryGetValue("side_data_type", out var rawSideDataType) &&
                rawSideDataType.GetValue<string>().Equals(name, StringComparison.OrdinalIgnoreCase));

            return result != null;
        }
    }
}
