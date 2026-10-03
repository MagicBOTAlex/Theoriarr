using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.Transcoding
{
    [TestFixture]
    public class TranscodeArgumentBuilderFixture : CoreTest<TranscodeArgumentBuilder>
    {
        private TranscodeJob _job;

        [SetUp]
        public void Setup()
        {
            _job = new TranscodeJob
            {
                Id = 1,
                SourcePath = "/media/input file.mkv",
                Mode = TranscodeMode.Quality,
                QualityValue = 23,
                Preset = "medium",
                VideoCodec = "hevc"
            };
        }

        private static TranscodeDevice Device(TranscodeDeviceKind kind, string id, params string[] encoders)
        {
            return new TranscodeDevice
            {
                Id = id,
                Kind = kind,
                Name = id,
                Supported = true,
                Enabled = true,
                Capabilities = new DeviceCapability
                {
                    Id = id,
                    Kind = kind,
                    Supported = true,
                    Encoders = encoders.ToList()
                }
            };
        }

        [Test]
        public void software_quality_should_use_crf_and_preset()
        {
            var device = Device(TranscodeDeviceKind.Software, "software", "libx264", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands.Should().HaveCount(1);
            plan.Commands[0].Should().Contain("-c:v libx265").And.Contain("-crf 23").And.Contain("-preset medium");
            plan.Commands[0].Should().Contain("-map 0:v:0").And.Contain("-c:a copy").And.Contain("-progress pipe:1");
            plan.Commands[0].Should().Contain("\"/media/input file.mkv\"");
        }

        [Test]
        public void software_target_should_use_two_passes()
        {
            _job.Mode = TranscodeMode.TargetSize;
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 4_000_000, 60, null, "/tmp/out.mkv");

            plan.Commands.Should().HaveCount(2);
            plan.Commands[0].Should().Contain("-pass 1").And.Contain("-b:v 4000000");
            plan.Commands[1].Should().Contain("-pass 2").And.Contain("-progress pipe:1");

            // The null muxer used by pass 1 has no subtitle encoder, so pass 1 must not map them.
            plan.Commands[0].Should().NotContain("0:s");
            plan.Commands[1].Should().Contain("-map 0:s?");
        }

        [Test]
        public void software_target_pass1_should_not_map_subtitles_with_english_audio_first()
        {
            _job.Mode = TranscodeMode.TargetSize;
            _job.PreferEnglishAudio = true;

            var mediaInfo = new MediaInfoModel
            {
                AudioStreams = new List<MediaInfoAudioStreamModel>
                {
                    new MediaInfoAudioStreamModel { Language = "ita" },
                    new MediaInfoAudioStreamModel { Language = "eng" }
                }
            };

            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 4_000_000, 60, mediaInfo, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-map 0:v:0 -map 0:a:1 -map 0:a:0").And.Contain("-disposition:a:0 default").And.NotContain("0:s");
            plan.Commands[1].Should().Contain("-map 0:v:0 -map 0:a:1 -map 0:a:0").And.Contain("-disposition:a:0 default").And.Contain("-map 0:s?");
        }

        [Test]
        public void software_should_map_the_probed_primary_video_stream()
        {
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, new MediaInfoModel { PrimaryVideoStreamIndex = 1 }, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-map 0:v:1").And.NotContain("-map 0:v:0");
        }

        [Test]
        public void software_av1_should_use_a_numeric_svt_av1_preset()
        {
            var device = Device(TranscodeDeviceKind.Software, "software", "libsvtav1");
            var plan = Subject.Build(_job, device, TranscodeCodec.Av1, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-c:v libsvtav1 -preset 8");
            plan.Commands[0].Should().NotContain("-preset medium");
        }

        [Test]
        public void software_av1_should_use_cpu_used_for_libaom()
        {
            var device = Device(TranscodeDeviceKind.Software, "software", "libaom-av1");
            var plan = Subject.Build(_job, device, TranscodeCodec.Av1, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-c:v libaom-av1 -cpu-used 4");
            plan.Commands[0].Should().NotContain("-preset");
        }

        [Test]
        public void software_x265_should_keep_the_logical_preset()
        {
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-c:v libx265 -preset medium");
        }

        [Test]
        public void nvenc_target_should_pin_gpu_and_rate()
        {
            _job.Mode = TranscodeMode.TargetSize;
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:0", "hevc_nvenc");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 4_000_000, 60, null, "/tmp/out.mkv");

            plan.Commands.Should().HaveCount(1);
            plan.Commands[0].Should().Contain("-hwaccel cuda -hwaccel_device 0");
            plan.Commands[0].Should().Contain("-c:v hevc_nvenc -gpu 0");
            plan.Commands[0].Should().Contain("-rc vbr -b:v 4000000");
        }

        [TestCase("ultrafast", "p1")]
        [TestCase("superfast", "p1")]
        [TestCase("veryfast", "p2")]
        [TestCase("faster", "p3")]
        [TestCase("slower", "p6")]
        [TestCase("veryslow", "p7")]
        [TestCase("placebo", "p7")]
        [TestCase("p3", "p3")]
        [TestCase("P7", "p7")]
        [TestCase(null, "p5")]
        [TestCase("", "p5")]
        public void nvenc_should_map_the_x264_only_logical_presets(string preset, string expected)
        {
            TranscodeArgumentBuilder.NvencPreset(preset).Should().Be(expected);
        }

        // NVENC's own presets (p1-p7 and the legacy names) and unrecognised tokens are passed through
        // unchanged; only the x264/x265-only names are rewritten, so e.g. `lossless` stays lossless.
        [TestCase("p1")]
        [TestCase("p7")]
        [TestCase("default")]
        [TestCase("hq")]
        [TestCase("medium")]
        [TestCase("slow")]
        [TestCase("fast")]
        [TestCase("hp")]
        [TestCase("bd")]
        [TestCase("ll")]
        [TestCase("llhq")]
        [TestCase("llhp")]
        [TestCase("lossless")]
        [TestCase("losslesshp")]
        [TestCase("P5")]
        [TestCase("nonsense")]
        public void nvenc_should_pass_through_native_and_unknown_presets(string preset)
        {
            TranscodeArgumentBuilder.NvencPreset(preset).Should().Be(preset.ToLowerInvariant());
        }

        [Test]
        public void nvenc_should_use_the_mapped_preset_in_the_built_command()
        {
            _job.Preset = "veryslow";
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:0", "hevc_nvenc");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-preset p7").And.NotContain("-preset veryslow");
        }

        [Test]
        public void nvenc_should_pass_a_native_preset_through_in_the_built_command()
        {
            _job.Preset = "lossless";
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:0", "hevc_nvenc");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-preset lossless");
        }

        [Test]
        public void nvenc_should_pass_an_existing_preset_through()
        {
            _job.Preset = "p7";
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:0", "hevc_nvenc");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-preset p7");
        }

        [Test]
        public void software_should_sanitize_a_hostile_preset()
        {
            _job.Preset = "medium -vf scale=1:1 -crf 0";
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-preset mediumvfscale11crf0");
            plan.Commands[0].Should().NotContain("-vf scale=1:1").And.NotContain("-crf 0");
        }

        [Test]
        public void nvenc_should_sanitize_a_hostile_preset()
        {
            _job.Preset = "p5 -rc constqp";
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:0", "hevc_nvenc");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-preset p5").And.NotContain("-rc constqp");
        }

        [Test]
        public void vaapi_quality_should_use_cqp()
        {
            _job.Mode = TranscodeMode.Quality;
            _job.Preset = null;
            var device = Device(TranscodeDeviceKind.Vaapi, "vaapi:/dev/dri/renderD128", "hevc_vaapi");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands.Should().HaveCount(1);
            plan.Commands[0].Should().Contain("-vaapi_device /dev/dri/renderD128");
            plan.Commands[0].Should().Contain("-vf format=nv12,hwupload");
            plan.Commands[0].Should().Contain("-c:v hevc_vaapi -rc_mode CQP -qp 23");
        }

        [Test]
        public void software_should_emit_the_configured_thread_count()
        {
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            device.Options = new TranscodeDeviceOptions { Cpu = new CpuTranscodeOptions { Threads = 4 } };
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-threads 4");
        }

        [Test]
        public void software_should_omit_threads_when_they_are_unset()
        {
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().NotContain("-threads");
        }

        [Test]
        public void nvenc_should_skip_the_decode_prelude_when_decode_accel_is_disabled()
        {
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:0", "hevc_nvenc");
            device.Options = new TranscodeDeviceOptions { Nvidia = new NvidiaTranscodeOptions { DecodeAccel = false } };
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().NotContain("-hwaccel");
            plan.Commands[0].Should().Contain("-c:v hevc_nvenc -gpu 0");
        }

        [Test]
        public void nvenc_should_keep_the_decode_prelude_when_decode_accel_is_enabled()
        {
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:0", "hevc_nvenc");
            device.Options = new TranscodeDeviceOptions { Nvidia = new NvidiaTranscodeOptions { DecodeAccel = true } };
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-hwaccel cuda -hwaccel_device 0 -hwaccel_output_format cuda");
        }

        [Test]
        public void nvenc_should_append_extra_args_to_the_video_arguments()
        {
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:0", "hevc_nvenc");
            device.Options = new TranscodeDeviceOptions { Nvidia = new NvidiaTranscodeOptions { ExtraArgs = "-rc-lookahead 20" } };
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-rc vbr -cq 23 -rc-lookahead 20");
        }

        [Test]
        public void nvenc_should_ignore_blank_extra_args()
        {
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:0", "hevc_nvenc");
            device.Options = new TranscodeDeviceOptions { Nvidia = new NvidiaTranscodeOptions { ExtraArgs = "   " } };
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-tune hq -rc vbr -cq 23 ");
        }

        [Test]
        public void vaapi_should_emit_the_decode_prelude_when_decode_accel_is_enabled()
        {
            var device = Device(TranscodeDeviceKind.Vaapi, "vaapi:/dev/dri/renderD128", "hevc_vaapi");
            device.Options = new TranscodeDeviceOptions { Vaapi = new VaapiTranscodeOptions { DecodeAccel = true } };
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-hwaccel vaapi -hwaccel_device /dev/dri/renderD128 -vaapi_device /dev/dri/renderD128");
            plan.Commands[0].Should().Contain("-vf format=nv12,hwupload");
        }

        [Test]
        public void vaapi_should_skip_the_decode_prelude_when_decode_accel_is_disabled()
        {
            var device = Device(TranscodeDeviceKind.Vaapi, "vaapi:/dev/dri/renderD128", "hevc_vaapi");
            device.Options = new TranscodeDeviceOptions { Vaapi = new VaapiTranscodeOptions { DecodeAccel = false } };
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().NotContain("-hwaccel");
            plan.Commands[0].Should().Contain("-vaapi_device /dev/dri/renderD128");
            plan.Commands[0].Should().Contain("-vf format=nv12,hwupload");
        }

        [Test]
        public void vaapi_should_append_extra_args_to_the_video_arguments()
        {
            var device = Device(TranscodeDeviceKind.Vaapi, "vaapi:/dev/dri/renderD128", "hevc_vaapi");
            device.Options = new TranscodeDeviceOptions { Vaapi = new VaapiTranscodeOptions { ExtraArgs = "-qp 25" } };
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-c:v hevc_vaapi -rc_mode CQP -qp 23 -qp 25");
        }

        [Test]
        public void build_should_guard_against_null_options()
        {
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            device.Options = null;
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().NotContain("-threads");
        }

        [Test]
        public void get_encoder_should_return_null_when_device_lacks_codec()
        {
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:1", "h264_nvenc", "hevc_nvenc");

            Subject.GetEncoder(device, TranscodeCodec.Av1).Should().BeNull();
            Subject.GetEncoder(device, TranscodeCodec.Hevc).Should().Be("hevc_nvenc");
        }

        [Test]
        public void get_encoder_should_fall_back_to_the_conventional_encoder_when_forced()
        {
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:1", "h264_nvenc", "hevc_nvenc");

            TranscodeArgumentBuilder.GetEncoderFor(device, TranscodeCodec.Av1).Should().BeNull();
            TranscodeArgumentBuilder.GetEncoderFor(device, TranscodeCodec.Av1, allowUnsupported: true).Should().Be("av1_nvenc");
        }

        [Test]
        public void build_should_use_the_conventional_encoder_when_a_codec_is_forced()
        {
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:1", "h264_nvenc", "hevc_nvenc");
            var plan = Subject.Build(_job, device, TranscodeCodec.Av1, 0, 60, null, "/tmp/out.mkv");

            plan.Encoder.Should().Be("av1_nvenc");
            plan.Commands[0].Should().Contain("-c:v av1_nvenc");
        }

        [Test]
        public void software_should_downscale_when_the_source_exceeds_max_height()
        {
            _job.MaxHeight = 1080;
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, new MediaInfoModel { Height = 2160 }, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-vf scale=w=-2:h=1080");
        }

        [Test]
        public void software_should_not_upscale_when_the_source_is_below_max_height()
        {
            _job.MaxHeight = 1080;
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, new MediaInfoModel { Height = 720 }, "/tmp/out.mkv");

            plan.Commands[0].Should().NotContain("scale");
        }

        [Test]
        public void nvenc_should_use_the_cuda_scale_filter()
        {
            _job.MaxHeight = 1440;
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:0", "hevc_nvenc");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, new MediaInfoModel { Height = 2160 }, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-vf scale_cuda=w=-2:h=1440");
        }

        [Test]
        public void vaapi_should_scale_on_the_cpu_before_hwupload()
        {
            _job.MaxHeight = 1080;
            var device = Device(TranscodeDeviceKind.Vaapi, "vaapi:/dev/dri/renderD128", "hevc_vaapi");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, new MediaInfoModel { Height = 2160 }, "/tmp/out.mkv");

            // scale_vaapi after the upload fails on real AMD hardware, so the CPU scale runs first.
            plan.Commands[0].Should().Contain("-vf scale=w=-2:h=1080,format=nv12,hwupload");
            plan.Commands[0].Should().NotContain("scale_vaapi");
        }

        [Test]
        public void build_should_put_english_audio_first_when_requested()
        {
            _job.PreferEnglishAudio = true;

            var mediaInfo = new MediaInfoModel
            {
                Height = 1080,
                AudioStreams = new List<MediaInfoAudioStreamModel>
                {
                    new MediaInfoAudioStreamModel { Language = "ita" },
                    new MediaInfoAudioStreamModel { Language = "eng" }
                }
            };

            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, mediaInfo, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-map 0:v:0 -map 0:a:1 -map 0:a:0").And.NotContain("-map 0:a?");
            plan.Commands[0].Should().Contain("-map 0:s?");

            // The reorder moves `default` onto the English track and clears it from the demoted one.
            plan.Commands[0].Should().Contain("-disposition:a:0 default");
            plan.Commands[0].Should().Contain("-disposition:a:1 0");
        }

        [Test]
        public void build_should_clear_default_on_every_demoted_audio_track()
        {
            _job.PreferEnglishAudio = true;

            var mediaInfo = new MediaInfoModel
            {
                AudioStreams = new List<MediaInfoAudioStreamModel>
                {
                    new MediaInfoAudioStreamModel { Language = "fra" },
                    new MediaInfoAudioStreamModel { Language = "eng" },
                    new MediaInfoAudioStreamModel { Language = "deu" }
                }
            };

            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, mediaInfo, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-map 0:v:0 -map 0:a:1 -map 0:a:0 -map 0:a:2");
            plan.Commands[0].Should().Contain("-disposition:a:0 default");
            plan.Commands[0].Should().Contain("-disposition:a:1 0");
            plan.Commands[0].Should().Contain("-disposition:a:2 0");
        }

        [Test]
        public void build_should_keep_the_default_audio_map_when_english_is_already_first()
        {
            _job.PreferEnglishAudio = true;

            var mediaInfo = new MediaInfoModel
            {
                AudioStreams = new List<MediaInfoAudioStreamModel>
                {
                    new MediaInfoAudioStreamModel { Language = "eng" },
                    new MediaInfoAudioStreamModel { Language = "ita" }
                }
            };

            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, mediaInfo, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-map 0:a?");
            plan.Commands[0].Should().NotContain("-disposition");
        }

        [Test]
        public void build_should_not_touch_dispositions_without_a_reorder()
        {
            _job.PreferEnglishAudio = false;

            var mediaInfo = new MediaInfoModel
            {
                AudioStreams = new List<MediaInfoAudioStreamModel>
                {
                    new MediaInfoAudioStreamModel { Language = "ita" },
                    new MediaInfoAudioStreamModel { Language = "eng" }
                }
            };

            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, mediaInfo, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-map 0:a?");
            plan.Commands[0].Should().NotContain("-disposition");
        }

        [Test]
        public void remux_should_copy_streams_without_an_encoder_or_device()
        {
            _job.Mode = TranscodeMode.Remux;
            var plan = Subject.Build(_job, null, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Encoder.Should().BeNull();
            plan.Commands.Should().HaveCount(1);
            plan.Commands[0].Should().Contain("-c copy").And.NotContain("-c:v");
            plan.Commands[0].Should().Contain("-map 0:v:0").And.Contain("-map 0:s?");
        }

        [Test]
        public void remux_to_mp4_should_drop_subtitles()
        {
            _job.Mode = TranscodeMode.Remux;
            _job.Container = "mp4";
            var plan = Subject.Build(_job, null, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mp4");

            plan.Commands.Should().HaveCount(1);
            plan.Commands[0].Should().Contain("-c copy").And.NotContain("0:s");
        }

        [Test]
        public void remux_to_mov_should_drop_subtitles()
        {
            _job.Mode = TranscodeMode.Remux;
            _job.Container = "mov";
            var plan = Subject.Build(_job, null, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mov");

            plan.Commands[0].Should().NotContain("0:s");
        }

        [Test]
        public void remux_to_mkv_should_keep_subtitles()
        {
            _job.Mode = TranscodeMode.Remux;
            _job.Container = "mkv";
            var plan = Subject.Build(_job, null, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-map 0:s?");
        }

        [Test]
        public void remux_should_detect_the_container_from_the_output_extension()
        {
            _job.Mode = TranscodeMode.Remux;
            _job.Container = null;
            var plan = Subject.Build(_job, null, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mp4");

            plan.Commands[0].Should().NotContain("0:s");
        }

        [Test]
        public void quote_should_escape_a_trailing_backslash_before_the_closing_quote()
        {
            _job.SourcePath = @"C:\media\show\";
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, @"C:\out\file.mkv");

            plan.Commands[0].Should().Contain(@"""C:\media\show\\""");
        }

        [Test]
        public void quote_should_escape_backslashes_and_embedded_quotes()
        {
            _job.SourcePath = "C:\\a\\\"b\\c.mkv";
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain(@"""C:\a\\\""b\c.mkv""");
        }

        [Test]
        public void max_quality_for_encoder_should_follow_the_encoder_family()
        {
            TranscodeArgumentBuilder.MaxQualityFor("libx265").Should().Be(51);
            TranscodeArgumentBuilder.MaxQualityFor("h264_nvenc").Should().Be(51);
            TranscodeArgumentBuilder.MaxQualityFor("hevc_vaapi").Should().Be(51);
            TranscodeArgumentBuilder.MaxQualityFor("libsvtav1").Should().Be(63);
            TranscodeArgumentBuilder.MaxQualityFor("av1_vaapi").Should().Be(63);
        }

        [Test]
        public void max_quality_for_codec_should_follow_the_codec()
        {
            TranscodeArgumentBuilder.MaxQualityForCodec(TranscodeCodec.H264).Should().Be(51);
            TranscodeArgumentBuilder.MaxQualityForCodec(TranscodeCodec.Hevc).Should().Be(51);
            TranscodeArgumentBuilder.MaxQualityForCodec(TranscodeCodec.Av1).Should().Be(63);
        }

        [Test]
        public void software_quality_should_clamp_x265_crf_to_the_encoder_maximum()
        {
            _job.QualityValue = 60;
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-crf 51").And.NotContain("-crf 60");
        }

        [Test]
        public void software_quality_should_keep_av1_crf_above_51()
        {
            _job.QualityValue = 60;
            var device = Device(TranscodeDeviceKind.Software, "software", "libsvtav1");
            var plan = Subject.Build(_job, device, TranscodeCodec.Av1, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-crf 60");
        }

        [Test]
        public void nvenc_quality_should_clamp_hevc_cq_to_the_encoder_maximum()
        {
            _job.QualityValue = 60;
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:0", "hevc_nvenc");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-cq 51").And.NotContain("-cq 60");
        }

        [Test]
        public void nvenc_quality_should_keep_av1_cq_above_51()
        {
            _job.QualityValue = 60;
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:0", "av1_nvenc");
            var plan = Subject.Build(_job, device, TranscodeCodec.Av1, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-cq 60");
        }

        [Test]
        public void max_height_should_round_an_odd_height_up_to_even()
        {
            _job.MaxHeight = 1081;
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, new MediaInfoModel { Height = 2160 }, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-vf scale=w=-2:h=1082");
        }

        [Test]
        public void max_height_should_floor_a_value_below_two()
        {
            _job.MaxHeight = 1;
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, new MediaInfoModel { Height = 2160 }, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-vf scale=w=-2:h=2");
        }

        [Test]
        public void nvenc_should_use_cpu_scale_when_decode_accel_is_disabled()
        {
            _job.MaxHeight = 1080;
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:0", "hevc_nvenc");
            device.Options = new TranscodeDeviceOptions { Nvidia = new NvidiaTranscodeOptions { DecodeAccel = false } };
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, new MediaInfoModel { Height = 2160 }, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-vf scale=w=-2:h=1080").And.NotContain("scale_cuda");
        }

        [Test]
        public void software_encode_to_mp4_should_drop_subtitles_attachments_and_data()
        {
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mp4");

            plan.Commands[0].Should().NotContain("0:s").And.NotContain("0:t").And.NotContain("0:d");
        }

        [Test]
        public void software_encode_to_mkv_should_map_subtitles_attachments_and_data()
        {
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-map 0:s?").And.Contain("-map 0:t?").And.Contain("-map 0:d?");
        }

        [Test]
        public void remux_to_mkv_should_map_attachments_and_data()
        {
            _job.Mode = TranscodeMode.Remux;
            _job.Container = "mkv";
            var plan = Subject.Build(_job, null, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().Contain("-map 0:s?").And.Contain("-map 0:t?").And.Contain("-map 0:d?");
        }

        [Test]
        public void software_target_pass1_should_not_map_attachments_or_data()
        {
            _job.Mode = TranscodeMode.TargetSize;
            var device = Device(TranscodeDeviceKind.Software, "software", "libx265");
            var plan = Subject.Build(_job, device, TranscodeCodec.Hevc, 4_000_000, 60, null, "/tmp/out.mkv");

            plan.Commands[0].Should().NotContain("0:s").And.NotContain("0:t").And.NotContain("0:d");
        }

        [Test]
        public void nvenc_should_reject_extra_args_with_a_quote()
        {
            var device = Device(TranscodeDeviceKind.Nvidia, "nvenc:0", "hevc_nvenc");
            device.Options = new TranscodeDeviceOptions { Nvidia = new NvidiaTranscodeOptions { ExtraArgs = "-rc-lookahead \"20\"" } };

            FluentActions.Invoking(() => Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv"))
                         .Should().Throw<System.InvalidOperationException>();
        }

        [Test]
        public void vaapi_should_reject_extra_args_with_a_newline()
        {
            var device = Device(TranscodeDeviceKind.Vaapi, "vaapi:/dev/dri/renderD128", "hevc_vaapi");
            device.Options = new TranscodeDeviceOptions { Vaapi = new VaapiTranscodeOptions { ExtraArgs = "-qp 25\n-qp 30" } };

            FluentActions.Invoking(() => Subject.Build(_job, device, TranscodeCodec.Hevc, 0, 60, null, "/tmp/out.mkv"))
                         .Should().Throw<System.InvalidOperationException>();
        }
    }
}
