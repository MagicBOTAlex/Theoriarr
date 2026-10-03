using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles.MediaInfo;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public interface ITranscodeArgumentBuilder
    {
        TranscodePlan Build(TranscodeJob job, TranscodeDevice device, TranscodeCodec codec, long videoBitrateBps, double durationSeconds, MediaInfoModel mediaInfo, string outputPath);
        string GetEncoder(TranscodeDevice device, TranscodeCodec codec);
    }

    // Turns a logical intent (codec/mode/quality/target) into concrete ffmpeg invocations for the
    // selected backend.
    public class TranscodeArgumentBuilder : ITranscodeArgumentBuilder
    {
        public string GetEncoder(TranscodeDevice device, TranscodeCodec codec)
        {
            return GetEncoderFor(device, codec);
        }

        public static string GetEncoderFor(TranscodeDevice device, TranscodeCodec codec, bool allowUnsupported = false)
        {
            var supported = Candidates(codec).FirstOrDefault(candidate => device?.Capabilities?.Encoders?.Contains(candidate) == true);

            if (supported != null || !allowUnsupported)
            {
                return supported;
            }

            // The probe found no working encoder, but the user forced this codec on. Fall back to the
            // conventional encoder for the device family so the encode is at least attempted.
            return Fallback(codec, device?.Kind);
        }

        private static string Fallback(TranscodeCodec codec, TranscodeDeviceKind? kind)
        {
            return kind switch
            {
                TranscodeDeviceKind.Nvidia => Candidates(codec).FirstOrDefault(candidate => candidate.EndsWith("_nvenc", StringComparison.Ordinal)),
                TranscodeDeviceKind.Vaapi => Candidates(codec).FirstOrDefault(candidate => candidate.EndsWith("_vaapi", StringComparison.Ordinal)),
                TranscodeDeviceKind.Software => Candidates(codec).FirstOrDefault(candidate => candidate.StartsWith("lib", StringComparison.Ordinal)),
                _ => null
            };
        }

        public TranscodePlan Build(TranscodeJob job, TranscodeDevice device, TranscodeCodec codec, long videoBitrateBps, double durationSeconds, MediaInfoModel mediaInfo, string outputPath)
        {
            var input = $" -i {Quote(job.SourcePath)}";

            // Remux copies every stream unchanged and only changes the container, so there is no
            // encoder and no device work: it runs on the scheduler's global concurrency slot.
            if (job.Mode == TranscodeMode.Remux)
            {
                var remux = new TranscodePlan
                {
                    Job = job,
                    Device = device,
                    Codec = codec,
                    Encoder = null,
                    Mode = job.Mode,
                    QualityValue = 0,
                    VideoBitrateBps = 0,
                    Preset = null,
                    DurationSeconds = durationSeconds,
                    OutputPath = outputPath
                };

                var remuxMap = BuildMapArguments(job, mediaInfo, TargetAcceptsCopiedStreams(job, outputPath));
                remux.Commands.Add($"{input} {remuxMap} -c copy -progress pipe:1 -nostats {Quote(outputPath)}");

                return remux;
            }

            var encoder = GetEncoderFor(device, codec) ?? GetEncoderFor(device, codec, allowUnsupported: true);

            if (encoder == null)
            {
                throw new InvalidOperationException($"Device '{device?.Id}' has no encoder for {codec}");
            }

            var maxHeight = TranscodeInput.NormalizeMaxHeight(job.MaxHeight) ?? 0;
            var sourceHeight = mediaInfo?.Height ?? 0;
            var scaleFilter = maxHeight > 0 && sourceHeight > maxHeight
                ? ScaleFilter(device, maxHeight)
                : null;

            // A stream copy only succeeds when the target container can hold it, so both the remux
            // and the encode paths derive subtitle/attachment/data inclusion from the effective
            // output container (mp4/mov drop them). Pass 1 targets the null muxer, which cannot hold
            // any copied stream, so it always maps video/audio only.
            var includeCopiedStreams = TargetAcceptsCopiedStreams(job, outputPath);

            var mapArguments = BuildMapArguments(job, mediaInfo, includeCopiedStreams);
            var pass1MapArguments = BuildMapArguments(job, mediaInfo, includeCopiedStreams: false);

            var plan = new TranscodePlan
            {
                Job = job,
                Device = device,
                Codec = codec,
                Encoder = encoder,
                Mode = job.Mode,
                QualityValue = Math.Clamp(job.QualityValue ?? 23, 0, MaxQualityFor(encoder)),
                VideoBitrateBps = videoBitrateBps,
                Preset = job.Preset,
                ScaleFilter = scaleFilter,
                MapArguments = mapArguments,
                DurationSeconds = durationSeconds,
                OutputPath = outputPath
            };

            var tail = $" -c:a copy -c:s copy -progress pipe:1 -nostats {Quote(outputPath)}";

            switch (device.Kind)
            {
                case TranscodeDeviceKind.Nvidia:
                    BuildNvidia(plan, input, tail);
                    break;
                case TranscodeDeviceKind.Vaapi:
                    BuildVaapi(plan, input, tail);
                    break;
                default:
                    BuildSoftware(plan, input, tail, pass1MapArguments);
                    break;
            }

            return plan;
        }

        private void BuildNvidia(TranscodePlan plan, string input, string tail)
        {
            var gpu = plan.Device.Id.Split(':').LastOrDefault();
            var options = plan.Device.Options?.Nvidia;

            // The CUDA decode prelude can be disabled for sources the NVDEC path cannot handle; the
            // encoder still gets the GPU pin so the encode itself stays on the selected device.
            var pre = options == null || options.DecodeAccel
                ? $"-hwaccel cuda -hwaccel_device {gpu} -hwaccel_output_format cuda"
                : string.Empty;
            var preset = NvencPreset(plan.Preset);
            var vf = plan.ScaleFilter.IsNullOrWhiteSpace() ? string.Empty : $"-vf {plan.ScaleFilter} ";
            var video = $"-c:v {plan.Encoder} -gpu {gpu} -preset {preset} -tune hq";

            video += plan.Mode == TranscodeMode.Quality
                ? $" -rc vbr -cq {plan.QualityValue}"
                : $" -rc vbr -b:v {plan.VideoBitrateBps} -maxrate {(long)(plan.VideoBitrateBps * 1.5)} -bufsize {(long)(plan.VideoBitrateBps * 3)} -multipass 2 -spatial-aq 1";

            if (options?.ExtraArgs.IsNotNullOrWhiteSpace() == true)
            {
                video += $" {ValidatedExtraArgs(options.ExtraArgs)}";
            }

            plan.Commands.Add($"{pre}{input} {plan.MapArguments} {vf}{video}{tail}");
        }

        private void BuildVaapi(TranscodePlan plan, string input, string tail)
        {
            var node = plan.Device.Id.Substring("vaapi:".Length);
            var options = plan.Device.Options?.Vaapi;

            // Hardware decode is optional; the `-vaapi_device` + `format=nv12,hwupload` filter stays
            // either way because VA-API encoding always needs frames uploaded to the device.
            var hwaccel = options == null || options.DecodeAccel
                ? $"-hwaccel vaapi -hwaccel_device {node} "
                : string.Empty;
            var pre = $"{hwaccel}-vaapi_device {node}";
            var videoFilter = plan.ScaleFilter.IsNullOrWhiteSpace()
                ? "-vf format=nv12,hwupload"
                : $"-vf {plan.ScaleFilter},format=nv12,hwupload";
            var video = $"-c:v {plan.Encoder}";

            video += plan.Mode == TranscodeMode.Quality
                ? $" -rc_mode CQP -qp {plan.QualityValue}"
                : $" -rc_mode VBR -b:v {plan.VideoBitrateBps} -maxrate {(long)(plan.VideoBitrateBps * 1.5)}";

            if (options?.ExtraArgs.IsNotNullOrWhiteSpace() == true)
            {
                video += $" {ValidatedExtraArgs(options.ExtraArgs)}";
            }

            plan.Commands.Add($"{pre}{input} {plan.MapArguments} {videoFilter} {video}{tail}");
        }

        private void BuildSoftware(TranscodePlan plan, string input, string tail, string pass1MapArguments)
        {
            var preset = plan.Preset.IsNullOrWhiteSpace() ? "medium" : plan.Preset;
            var baseVideo = $"-c:v {plan.Encoder} {PresetArguments(plan.Encoder, preset)}";
            var threads = plan.Device?.Options?.Cpu?.Threads ?? 0;

            if (threads > 0)
            {
                baseVideo += $" -threads {threads}";
            }

            if (!plan.ScaleFilter.IsNullOrWhiteSpace())
            {
                baseVideo += $" -vf {plan.ScaleFilter}";
            }

            if (plan.Mode == TranscodeMode.Quality)
            {
                plan.Commands.Add($"{input} {plan.MapArguments} {baseVideo} -crf {plan.QualityValue}{tail}");
                return;
            }

            var passLog = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(plan.OutputPath) ?? ".", "ffmpeg2pass");
            var rate = $"-b:v {plan.VideoBitrateBps} -maxrate {(long)(plan.VideoBitrateBps * 1.5)} -bufsize {(long)(plan.VideoBitrateBps * 3)}";

            // Two-pass for accurate target sizing (single-pass CRF never hits a byte target). The
            // pass-1 analysis run writes to the null muxer, which has no subtitle encoder, so it maps
            // video + audio only; the final pass keeps the full map (subtitles and English-first audio).
            // -progress pipe:1 -nostats keeps the liveness signal on stdout and stops ffmpeg from
            // writing a status line to stderr twice a second (which the runner logs as an error).
            plan.Commands.Add($"{input} {pass1MapArguments} {baseVideo} {rate} -pass 1 -passlogfile {Quote(passLog)} -an -progress pipe:1 -nostats -f null -");
            plan.Commands.Add($"{input} {plan.MapArguments} {baseVideo} {rate} -pass 2 -passlogfile {Quote(passLog)}{tail}");
        }

        // Maps the probed primary video stream explicitly: 0:v:0 can be a leading mjpeg/png cover
        // (the probe deliberately skips those), which would encode the wrong stream. When requested,
        // an English audio track is written first (it is still kept, just reordered) and the
        // `default` disposition is moved to it so players that honour the flag pick it; otherwise the
        // default "-map 0:a?" is used and dispositions are left untouched. Copied-only streams
        // (subtitles, attachments, data) are mapped only when the effective output container can hold
        // them; the software pass-1 analysis run targets the null muxer and maps none of them.
        private static string BuildMapArguments(TranscodeJob job, MediaInfoModel mediaInfo, bool includeCopiedStreams)
        {
            var maps = new List<string> { $"-map 0:v:{GetPrimaryVideoStreamIndex(mediaInfo)}" };

            if (!job.PreferEnglishAudio || mediaInfo?.AudioStreams == null || mediaInfo.AudioStreams.Count == 0)
            {
                maps.Add("-map 0:a?");
            }
            else
            {
                var englishIndex = mediaInfo.AudioStreams.FindIndex(stream => IsEnglish(stream.Language));

                if (englishIndex <= 0)
                {
                    maps.Add("-map 0:a?");
                }
                else
                {
                    maps.Add($"-map 0:a:{englishIndex}");

                    var outputIndex = 1;

                    for (var index = 0; index < mediaInfo.AudioStreams.Count; index++)
                    {
                        if (index != englishIndex)
                        {
                            maps.Add($"-map 0:a:{index}");
                            outputIndex++;
                        }
                    }

                    // Reordering the maps alone leaves the source's `default` disposition on the
                    // demoted (non-English) track, so a player that honours the flag still picks it.
                    // Move the flag to the English track (now output a:0) and clear it from the rest.
                    // ffmpeg can only replace a track's disposition wholesale (`0` clears every bit),
                    // and the probed MediaInfo does not record the other bits (commentary, visual
                    // impaired, …), so those cannot be re-applied here; only `default` is guaranteed.
                    maps.Add("-disposition:a:0 default");

                    for (var index = 1; index < outputIndex; index++)
                    {
                        maps.Add($"-disposition:a:{index} 0");
                    }
                }
            }

            if (includeCopiedStreams)
            {
                maps.Add("-map 0:s?");
                maps.Add("-map 0:t?");
                maps.Add("-map 0:d?");
            }

            maps.Add("-map_metadata 0 -map_chapters 0");

            return string.Join(" ", maps);
        }

        // The probe stores the index of its chosen video stream among the file's video streams, i.e.
        // the N in "-map 0:v:N"; older media info (or an unprobed file) defaults to the first stream.
        private static int GetPrimaryVideoStreamIndex(MediaInfoModel mediaInfo)
        {
            return mediaInfo?.PrimaryVideoStreamIndex ?? 0;
        }

        // mp4/mov only hold mov_text subtitles, so stream-copying the typical subrip/ass/PGS tracks
        // makes ffmpeg exit 234. Attachments and data streams are likewise only copyable into
        // containers that support them (mkv). Drop all three for mp4/mov; mkv and anything else keep
        // the current copy-all behaviour. The container is taken from the job, falling back to the
        // output file's extension for a non-remux encode (whose output container is the source's).
        private static bool TargetAcceptsCopiedStreams(TranscodeJob job, string outputPath)
        {
            var container = job?.Container;

            if (container.IsNullOrWhiteSpace())
            {
                container = System.IO.Path.GetExtension(outputPath);
            }

            container = (container ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();

            return container != "mp4" && container != "mov" && container != "m4v";
        }

        private static bool IsEnglish(string language)
        {
            return language != null &&
                   (language.Equals("eng", StringComparison.OrdinalIgnoreCase) ||
                    language.Equals("en", StringComparison.OrdinalIgnoreCase) ||
                    language.Equals("english", StringComparison.OrdinalIgnoreCase));
        }

        // SVT-AV1 expects a numeric -preset (0-13) and libaom-av1 uses -cpu-used (0-8); the x264/x265
        // logical preset names are rejected by both, so map them per encoder. x264/x265 keep -preset.
        private static string PresetArguments(string encoder, string preset)
        {
            var safe = SanitizePreset(preset);

            return encoder switch
            {
                "libsvtav1" => $"-preset {SvtAv1Preset(safe)}",
                "libaom-av1" => $"-cpu-used {AomCpuUsed(safe)}",
                _ => $"-preset {safe ?? "medium"}"
            };
        }

        // NVENC accepts its own presets (`p1`-`p7` plus the legacy names: default/hq/medium/slow/fast/
        // hp/bd/ll/llhq/llhp/lossless/losslesshp) and rejects only the x264/x265-only logical names
        // (ultrafast/superfast/veryfast/faster/slower/veryslow/placebo). Map just those to their
        // nearest `pN` and pass every other token through unchanged, so a user's `lossless` (or an
        // unrecognised value) is never silently rewritten to a lossy default -- ffmpeg reports the
        // invalid one itself.
        public static string NvencPreset(string preset)
        {
            var safe = SanitizePreset(preset);

            if (safe.IsNullOrWhiteSpace())
            {
                return "p5";
            }

            safe = safe.ToLowerInvariant();

            return safe switch
            {
                "ultrafast" => "p1",
                "superfast" => "p1",
                "veryfast" => "p2",
                "faster" => "p3",
                "slower" => "p6",
                "veryslow" => "p7",
                "placebo" => "p7",
                _ => safe
            };
        }

        // Preset values are interpolated into a single ffmpeg argument string, so anything outside a
        // plain alphanumeric token could be split into extra options (option injection). Strip
        // everything else; callers fall back to their default when nothing usable remains.
        private static string SanitizePreset(string preset)
        {
            if (preset.IsNullOrWhiteSpace())
            {
                return null;
            }

            var builder = new StringBuilder(preset.Length);

            foreach (var character in preset.Trim())
            {
                if ((character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9'))
                {
                    builder.Append(character);
                }
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        private static string SvtAv1Preset(string preset)
        {
            if (int.TryParse(preset, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
            {
                return Math.Clamp(numeric, 0, 13).ToString(CultureInfo.InvariantCulture);
            }

            return preset?.ToLowerInvariant() switch
            {
                "ultrafast" => "13",
                "superfast" => "12",
                "veryfast" => "10",
                "faster" => "9",
                "fast" => "9",
                "medium" => "8",
                "slow" => "6",
                "slower" => "5",
                "veryslow" => "4",
                "placebo" => "2",
                _ => "8"
            };
        }

        private static string AomCpuUsed(string preset)
        {
            if (int.TryParse(preset, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
            {
                return Math.Clamp(numeric, 0, 8).ToString(CultureInfo.InvariantCulture);
            }

            return preset?.ToLowerInvariant() switch
            {
                "ultrafast" => "8",
                "superfast" => "7",
                "veryfast" => "6",
                "faster" => "5",
                "fast" => "5",
                "medium" => "4",
                "slow" => "3",
                "slower" => "2",
                "veryslow" => "1",
                "placebo" => "0",
                _ => "4"
            };
        }

        // Caps the output height for the selected backend, preserving the aspect ratio (and keeping
        // the width even). Only ever downscales: the caller applies it only when source > target.
        // CUDA frames must be scaled with `scale_cuda`; when hardware decode is disabled the frames
        // stay in system memory, so the CPU `scale` filter is used instead (scale_cuda would fail).
        // VA-API scales on the CPU before `hwupload` rather than with `scale_vaapi`: scaling after the
        // upload fails on real AMD hardware with "Cannot allocate memory", and CPU-scale-then-upload
        // matches the accepted software-decode behaviour for VA-API (C5).
        private static string ScaleFilter(TranscodeDevice device, int maxHeight)
        {
            var useCudaScale = device?.Kind == TranscodeDeviceKind.Nvidia &&
                               (device.Options?.Nvidia == null || device.Options.Nvidia.DecodeAccel);

            return useCudaScale
                ? $"scale_cuda=w=-2:h={maxHeight}"
                : $"scale=w=-2:h={maxHeight}";
        }

        // Quality is a per-encoder range: x264/x265 (and their NVENC/VA-API siblings) cap CRF/QP at
        // 51, while the AV1 encoders accept up to 63. ffmpeg aborts on an out-of-range value, so the
        // builder clamps the requested value to what the resolved encoder can take.
        public static int MaxQualityFor(string encoder)
        {
            return encoder switch
            {
                "libsvtav1" or "libaom-av1" or "av1_nvenc" or "av1_vaapi" => 63,
                _ => 51
            };
        }

        public static int MaxQualityForCodec(TranscodeCodec codec)
        {
            return codec == TranscodeCodec.Av1 ? 63 : 51;
        }

        // ExtraArgs are admin-supplied but interpolated into the single argv string, so refuse a
        // value that contains quoting or line-break characters instead of silently re-quoting it.
        private static string ValidatedExtraArgs(string extraArgs)
        {
            if (extraArgs.IsNullOrWhiteSpace())
            {
                return null;
            }

            if (!TranscodeInput.IsValidExtraArgs(extraArgs))
            {
                throw new InvalidOperationException("The device extra arguments contain a quote or line break, which is not allowed");
            }

            return extraArgs.Trim();
        }

        private static IEnumerable<string> Candidates(TranscodeCodec codec)
        {
            return codec switch
            {
                TranscodeCodec.H264 => new[] { "h264_nvenc", "h264_vaapi", "libx264" },
                TranscodeCodec.Av1 => new[] { "av1_nvenc", "av1_vaapi", "libsvtav1", "libaom-av1" },
                _ => new[] { "hevc_nvenc", "hevc_vaapi", "libx265" }
            };
        }

        // ffmpeg receives one argument string that .NET splits using Windows command-line rules: a
        // backslash is only special immediately before a quote, and a quote inside an argument is
        // written as \". Backslashes are therefore doubled before a quote (including the closing one)
        // so a Windows path ending in \ does not swallow the delimiter.
        internal static string Quote(string value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return "\"\"";
            }

            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');

            var backslashes = 0;

            foreach (var character in value)
            {
                if (character == '\\')
                {
                    backslashes++;
                }
                else if (character == '"')
                {
                    builder.Append('\\', (backslashes * 2) + 1);
                    builder.Append('"');
                    backslashes = 0;
                }
                else
                {
                    builder.Append('\\', backslashes);
                    builder.Append(character);
                    backslashes = 0;
                }
            }

            builder.Append('\\', backslashes * 2);
            builder.Append('"');

            return builder.ToString();
        }
    }
}
