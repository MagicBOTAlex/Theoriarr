using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using FFMpegCore;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common.Categories;

namespace NzbDrone.Core.Test.MediaFiles.MediaInfo
{
    [TestFixture]
    [DiskAccessTest]
    public class VideoFileInfoReaderFixture : CoreTest<VideoFileInfoReader>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(It.IsAny<string>()))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.OpenReadStream(It.IsAny<string>()))
                  .Returns<string>(s => new FileStream(s, FileMode.Open, FileAccess.Read));
        }

        [Test]
        public void get_runtime()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Files", "Media", "H264_sample.mp4");

            Subject.GetRunTime(path).Value.Seconds.Should().Be(10);
        }

        [Test]
        public void get_info()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Files", "Media", "H264_sample.mp4");

            var info = Subject.GetMediaInfo(path);

            info.VideoFormat.Should().Be("h264");
            info.VideoCodecID.Should().Be("avc1");
            info.VideoProfile.Should().Be("Constrained Baseline");
            info.PrimaryAudioStream.Format.Should().Be("aac");
            info.PrimaryAudioStream.CodecId.Should().Be("mp4a");
            info.PrimaryAudioStream.Profile.Should().Be("LC");
            info.PrimaryAudioStream.Bitrate.Should().Be(125509);
            info.PrimaryAudioStream.Channels.Should().Be(2);
            info.PrimaryAudioStream.ChannelPositions.Should().Be("stereo");
            info.AudioStreams?.Select(l => l.Language).Should().BeEquivalentTo("eng");
            info.AudioStreamCount.Should().Be(1);
            info.AudioFormat.Should().Be("aac");
            info.AudioCodecID.Should().Be("mp4a");
            info.AudioProfile.Should().Be("LC");
            info.AudioBitrate.Should().Be(125509);
            info.AudioChannels.Should().Be(2);
            info.AudioChannelPositions.Should().Be("stereo");
            info.AudioLanguages.Should().BeEquivalentTo("eng");
            info.Subtitles.Should().BeEmpty();
            info.Height.Should().Be(320);
            info.RunTime.Seconds.Should().Be(10);
            info.ScanType.Should().Be("Progressive");
            info.SubtitleStreams?.Select(l => l.Language).Should().BeEmpty();
            info.VideoBitrate.Should().Be(193694);
            info.VideoFps.Should().Be(24);
            info.Width.Should().Be(480);
            info.VideoBitDepth.Should().Be(8);
            info.VideoMultiViewCount.Should().Be(1);
            info.PrimaryVideoStreamIndex.Should().Be(0);
            info.VideoColourPrimaries.Should().Be("smpte170m");
            info.VideoTransferCharacteristics.Should().Be("bt709");
            info.Title.Should().Be("Sample Title");
        }

        [Test]
        public void get_info_unicode()
        {
            var srcPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "Files", "Media", "H264_sample.mp4");

            var tempPath = GetTempFilePath();
            Directory.CreateDirectory(tempPath);

            var path = Path.Combine(tempPath, "H264_Pok\u00E9mon.mkv");

            File.Copy(srcPath, path);

            var info = Subject.GetMediaInfo(path);

            info.VideoFormat.Should().Be("h264");
            info.VideoCodecID.Should().Be("avc1");
            info.VideoProfile.Should().Be("Constrained Baseline");
            info.PrimaryAudioStream.Format.Should().Be("aac");
            info.PrimaryAudioStream.CodecId.Should().Be("mp4a");
            info.PrimaryAudioStream.Profile.Should().Be("LC");
            info.PrimaryAudioStream.Bitrate.Should().Be(125509);
            info.PrimaryAudioStream.Channels.Should().Be(2);
            info.PrimaryAudioStream.ChannelPositions.Should().Be("stereo");
            info.AudioStreams?.Select(l => l.Language).Should().BeEquivalentTo("eng");
            info.AudioStreamCount.Should().Be(1);
            info.AudioFormat.Should().Be("aac");
            info.AudioCodecID.Should().Be("mp4a");
            info.AudioProfile.Should().Be("LC");
            info.AudioBitrate.Should().Be(125509);
            info.AudioChannels.Should().Be(2);
            info.AudioChannelPositions.Should().Be("stereo");
            info.AudioLanguages.Should().BeEquivalentTo("eng");
            info.Subtitles.Should().BeEmpty();
            info.Height.Should().Be(320);
            info.RunTime.Seconds.Should().Be(10);
            info.ScanType.Should().Be("Progressive");
            info.SubtitleStreams?.Select(l => l.Language).Should().BeEmpty();
            info.VideoBitrate.Should().Be(193694);
            info.VideoFps.Should().Be(24);
            info.Width.Should().Be(480);
            info.VideoColourPrimaries.Should().Be("smpte170m");
            info.VideoTransferCharacteristics.Should().Be("bt709");
            info.Title.Should().Be("Sample Title");
        }

        [TestCase(8, "", "", null, null, HdrFormat.None)]
        [TestCase(10, "", "", null, null, HdrFormat.None)]
        [TestCase(10, "bt709", "bt709", null, null, HdrFormat.None)]
        [TestCase(8, "bt2020", "smpte2084", null, null, HdrFormat.None)]
        [TestCase(10, "bt2020", "bt2020-10", null, null, HdrFormat.None)]
        [TestCase(10, "bt2020", "arib-std-b67", null, null, HdrFormat.Hlg10)]
        [TestCase(10, "bt2020", "smpte2084", null, null, HdrFormat.Pq10)]
        [TestCase(10, "bt2020", "smpte2084", new[] { "" }, null, HdrFormat.Pq10)]
        [TestCase(10, "bt2020", "smpte2084", new[] { FFMpegCoreSideDataTypes.MasteringDisplayMetadata }, null, HdrFormat.Hdr10)]
        [TestCase(10, "bt2020", "smpte2084", new[] { FFMpegCoreSideDataTypes.ContentLightLevelMetadata }, null, HdrFormat.Hdr10)]
        [TestCase(10, "bt2020", "smpte2084", new[] { FFMpegCoreSideDataTypes.HdrDynamicMetadataSpmte2094 }, null, HdrFormat.Hdr10Plus)]
        [TestCase(10, "bt2020", "smpte2084", new[] { FFMpegCoreSideDataTypes.DoviConfigurationRecordSideData }, null, HdrFormat.DolbyVision)]
        [TestCase(10, "bt2020", "smpte2084", new[] { FFMpegCoreSideDataTypes.DoviConfigurationRecordSideData }, 1, HdrFormat.DolbyVisionHdr10)]
        [TestCase(10, "bt2020", "smpte2084", new[] { FFMpegCoreSideDataTypes.DoviConfigurationRecordSideData, FFMpegCoreSideDataTypes.HdrDynamicMetadataSpmte2094 }, 1, HdrFormat.DolbyVisionHdr10Plus)]
        [TestCase(10, "bt2020", "smpte2084", new[] { FFMpegCoreSideDataTypes.DoviConfigurationRecordSideData, FFMpegCoreSideDataTypes.HdrDynamicMetadataSpmte2094 }, 6, HdrFormat.DolbyVisionHdr10Plus)]
        [TestCase(10, "bt2020", "smpte2084", new[] { FFMpegCoreSideDataTypes.DoviConfigurationRecordSideData }, 2, HdrFormat.DolbyVisionSdr)]
        [TestCase(10, "bt2020", "smpte2084", new[] { FFMpegCoreSideDataTypes.DoviConfigurationRecordSideData }, 4, HdrFormat.DolbyVisionHlg)]
        public void should_detect_hdr_correctly(int bitDepth, string colourPrimaries, string transferFunction, string[] sideDataTypes, int? doviConfigId, HdrFormat expected)
        {
            var sideData = sideDataTypes?.Select(sideDataType =>
            {
                var sideData = new Dictionary<string, JsonNode>
                {
                    { "side_data_type", JsonValue.Create(sideDataType) }
                };

                if (doviConfigId.HasValue)
                {
                    sideData.Add("dv_bl_signal_compatibility_id", JsonValue.Create(doviConfigId.Value));
                }

                return sideData;
            }).ToList();

            var result = VideoFileInfoReader.GetHdrFormat(bitDepth, colourPrimaries, transferFunction, sideData);

            result.Should().Be(expected);
        }

        [Test]
        public void should_report_single_view_when_no_stereo_mode_tag()
        {
            VideoFileInfoReader.GetMultiViewCount(null).Should().Be(1);
            VideoFileInfoReader.GetMultiViewCount(new Dictionary<string, string> { { "title", "3D" } }).Should().Be(1);
        }

        [Test]
        public void should_report_3d_when_stereo_mode_tag_present()
        {
            var tags = new Dictionary<string, string> { { "stereo_mode", "block_lr" } };

            VideoFileInfoReader.GetMultiViewCount(tags).Should().Be(2);
        }

        [Test]
        public void should_skip_a_cover_flagged_as_attached_picture()
        {
            var cover = new VideoStream
            {
                Index = 0,
                CodecName = "bmp",
                Disposition = new Dictionary<string, bool> { { "attached_pic", true } }
            };

            var video = new VideoStream { Index = 1, CodecName = "h264" };

            var analysis = GivenAnalysis(cover, video);

            VideoFileInfoReader.GetPrimaryVideoStream(analysis.Object).Should().BeSameAs(video);
            VideoFileInfoReader.GetPrimaryVideoStreamIndex(analysis.Object).Should().Be(1);
        }

        [Test]
        public void should_skip_a_webp_cover_without_the_disposition()
        {
            var cover = new VideoStream { Index = 0, CodecName = "webp" };
            var video = new VideoStream { Index = 1, CodecName = "hevc" };

            var analysis = GivenAnalysis(cover, video);

            VideoFileInfoReader.GetPrimaryVideoStreamIndex(analysis.Object).Should().Be(1);
        }

        [Test]
        public void should_use_the_only_video_stream_even_when_it_is_a_cover()
        {
            var cover = new VideoStream
            {
                Index = 0,
                CodecName = "mjpeg",
                Disposition = new Dictionary<string, bool> { { "attached_pic", true } }
            };

            var analysis = GivenAnalysis(cover);

            VideoFileInfoReader.GetPrimaryVideoStreamIndex(analysis.Object).Should().Be(0);
        }

        private static Mock<IMediaAnalysis> GivenAnalysis(params VideoStream[] streams)
        {
            var analysis = new Mock<IMediaAnalysis>();
            analysis.SetupGet(media => media.VideoStreams).Returns(streams.ToList());
            analysis.SetupGet(media => media.PrimaryVideoStream).Returns(streams.FirstOrDefault());

            return analysis;
        }

        [Test]
        public void should_not_reprobe_a_failed_file_after_max_attempts()
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.MediaInfoProbeMaxAttempts)
                  .Returns(1);

            var path = CreateCorruptMediaFile();

            Subject.GetMediaInfo(path).Should().BeNull();

            // The file becomes readable, but with the same on-disk identity (size/mtime are stubbed
            // to constants) the recorded failure wins and it is not probed again.
            OverwriteWithValidSample(path);

            Subject.GetMediaInfo(path).Should().BeNull();
        }

        [Test]
        public void should_reprobe_a_failed_file_until_max_attempts()
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.MediaInfoProbeMaxAttempts)
                  .Returns(2);

            var path = CreateCorruptMediaFile();

            Subject.GetMediaInfo(path).Should().BeNull();

            OverwriteWithValidSample(path);

            Subject.GetMediaInfo(path).Should().NotBeNull();
        }

        private static string CreateCorruptMediaFile()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), "theoriarr-probe-" + System.Guid.NewGuid());
            Directory.CreateDirectory(tempPath);

            var path = Path.Combine(tempPath, "Corrupt.mkv");
            File.WriteAllText(path, "this is not a valid video file");

            return path;
        }

        private static void OverwriteWithValidSample(string path)
        {
            File.Copy(Path.Combine(TestContext.CurrentContext.TestDirectory, "Files", "Media", "H264_sample.mp4"), path, true);
        }
    }
}
