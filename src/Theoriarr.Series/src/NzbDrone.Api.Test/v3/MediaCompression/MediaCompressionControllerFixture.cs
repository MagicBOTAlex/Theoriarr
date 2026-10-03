using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;
using Sonarr.Api.V3.MediaCompression;
using Sonarr.Http;
using Sonarr.Http.REST;

namespace NzbDrone.Api.Test.v3.MediaCompression
{
    [TestFixture]
    public class MediaCompressionControllerFixture : TestBase<MediaCompressionController>
    {
        [Test]
        public void should_be_routed_under_v3_and_v5()
        {
            // The SPA talks to the Sonarr domain under /api/v5 while the preserved external API is
            // /api/v3; both must expose media-compression or the frontend polling 404s.
            var routes = typeof(MediaCompressionController)
                .GetCustomAttributes(true)
                .OfType<VersionedApiControllerAttribute>()
                .Select(attribute => attribute.Template)
                .ToList();

            routes.Should().BeEquivalentTo(new[] { "api/v3/media-compression", "api/v5/media-compression" });
        }

        private MediaCompressionCapabilities GivenCapabilities()
        {
            return new MediaCompressionCapabilities
            {
                Fingerprint = "abc123",
                FFmpegPath = "/usr/bin/ffmpeg",
                FFmpegVersion = "6.1.1",
                Devices = new List<TranscodeDevice>
                {
                    new TranscodeDevice
                    {
                        Id = "software",
                        Kind = TranscodeDeviceKind.Software,
                        Name = "CPU",
                        Supported = true,
                        Enabled = true,
                        MaxParallel = 1,
                        Priority = 100,
                        Weight = 100,
                        Codecs = new List<TranscodeCodecSetting>
                        {
                            new TranscodeCodecSetting { Codec = "hevc", Encoder = "libx265", Supported = true, Enabled = true },
                            new TranscodeCodecSetting { Codec = "av1", Encoder = "libsvtav1", Supported = false, Enabled = true }
                        }
                    }
                }
            };
        }

        [Test]
        public void get_capabilities_should_return_devices()
        {
            Mocker.GetMock<IGpuCapabilityService>()
                  .Setup(service => service.GetCapabilities(false))
                  .Returns(GivenCapabilities());

            var result = Subject.GetCapabilities();

            result.Value.Fingerprint.Should().Be("abc123");
            result.Value.Devices.Should().HaveCount(1);
            result.Value.Devices[0].Kind.Should().Be("Software");
            result.Value.Devices[0].DeviceId.Should().Be("software");
            result.Value.Devices[0].Label.Should().Be("CPU");
            result.Value.Devices[0].Codecs.Should().HaveCount(2);
            result.Value.Devices[0].Codecs.Single(codec => codec.Codec == "av1").Enabled.Should().BeTrue();
            result.Value.Devices[0].Codecs.Single(codec => codec.Codec == "av1").Supported.Should().BeFalse();
        }

        [Test]
        public void reprobe_should_force_a_probe()
        {
            Mocker.GetMock<IGpuCapabilityService>()
                  .Setup(service => service.GetCapabilities(true))
                  .Returns(GivenCapabilities());

            Subject.Reprobe(new ReprobeResource { Force = true });

            Mocker.GetMock<IGpuCapabilityService>()
                  .Verify(service => service.GetCapabilities(true), Times.Once);
        }

        [Test]
        public void reprobe_with_an_empty_body_should_force_a_probe()
        {
            Mocker.GetMock<IGpuCapabilityService>()
                  .Setup(service => service.GetCapabilities(true))
                  .Returns(GivenCapabilities());

            Subject.Reprobe(null);

            Mocker.GetMock<IGpuCapabilityService>()
                  .Verify(service => service.GetCapabilities(true), Times.Once);
        }

        [Test]
        public void clear_history_should_return_the_number_of_cleared_jobs()
        {
            Mocker.GetMock<ITranscodeService>()
                  .Setup(service => service.ClearHistory())
                  .Returns(3);

            var result = Subject.ClearHistory();

            result.Value.ClearedCount.Should().Be(3);
        }

        [Test]
        public void reprobe_with_an_empty_object_body_should_force_a_probe()
        {
            Mocker.GetMock<IGpuCapabilityService>()
                  .Setup(service => service.GetCapabilities(true))
                  .Returns(GivenCapabilities());

            Subject.Reprobe(new ReprobeResource());

            Mocker.GetMock<IGpuCapabilityService>()
                  .Verify(service => service.GetCapabilities(true), Times.Once);
        }

        [Test]
        public void reprobe_should_honour_an_explicit_force_false()
        {
            Mocker.GetMock<IGpuCapabilityService>()
                  .Setup(service => service.GetCapabilities(false))
                  .Returns(GivenCapabilities());

            Subject.Reprobe(new ReprobeResource { Force = false });

            Mocker.GetMock<IGpuCapabilityService>()
                  .Verify(service => service.GetCapabilities(false), Times.Once);
        }

        private void GivenCompressionConfig()
        {
            Mocker.GetMock<IConfigService>().SetupGet(config => config.DefaultVideoCodec).Returns("hevc");
            Mocker.GetMock<IConfigService>().SetupGet(config => config.DefaultRateControlMode).Returns(TranscodeMode.Quality);
            Mocker.GetMock<IConfigService>().SetupGet(config => config.DefaultQualityValue).Returns(23);
            Mocker.GetMock<IConfigService>().SetupGet(config => config.DefaultPreset).Returns("medium");
            Mocker.GetMock<IConfigService>().SetupGet(config => config.DefaultTargetEpisodeSizeMB).Returns(1500);
            Mocker.GetMock<IConfigService>().SetupGet(config => config.DefaultTargetMovieSizeMB).Returns(4000);
            Mocker.GetMock<IConfigService>().SetupGet(config => config.DefaultReducePercent).Returns(60);

            Mocker.GetMock<ITranscodeService>()
                  .Setup(service => service.Queue(It.IsAny<TranscodeJob>()))
                  .Returns((TranscodeJob job) =>
                  {
                      job.Id = 42;
                      return job;
                  });
        }

        [Test]
        public void create_jobs_should_queue_a_transcode_for_an_episode_file()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 }
            });

            result.Value.Jobs.Should().HaveCount(1);
            result.Value.Jobs[0].Id.Should().Be(42);
            result.Value.Jobs[0].VideoCodec.Should().Be("hevc");
            result.Value.Jobs[0].Status.Should().Be("Queued");

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.Queue(It.Is<TranscodeJob>(job => job.SourcePath == "/media/show/episode.mkv")), Times.Once);

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(queue => queue.Push(It.IsAny<TranscodeMediaCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Once);
        }

        [Test]
        public void create_jobs_with_requeue_ids_should_mark_the_source_job()
        {
            GivenCompressionConfig();

            var source = new TranscodeJob
            {
                Id = 7,
                EpisodeFileId = 5,
                Status = TranscodeJobStatus.Failed,
                SourcePath = "/media/show/episode.mkv"
            };

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.Find(7))
                  .Returns(source);

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                Force = true,
                RequeueJobIds = new List<int> { 7 }
            });

            result.Value.Jobs.Should().HaveCount(1);
            source.RequeuedJobId.Should().Be(42);

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Verify(repository => repository.Update(It.Is<TranscodeJob>(job => job.Id == 7 && job.RequeuedJobId == 42)), Times.Once);
        }

        [Test]
        public void create_jobs_with_requeue_ids_should_preserve_the_source_job_settings()
        {
            GivenCompressionConfig();

            var source = new TranscodeJob
            {
                Id = 7,
                EpisodeFileId = 5,
                Status = TranscodeJobStatus.Failed,
                SourcePath = "/media/show/episode.mkv",
                ProfileId = 5,
                Mode = TranscodeMode.Quality,
                VideoCodec = "hevc",
                QualityValue = 22,
                MaxHeight = 1440,
                Preset = "medium",
                Tag = "D1440p"
            };

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.Find(7))
                  .Returns(source);

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                Force = true,
                RequeueJobIds = new List<int> { 7 }
            });

            result.Value.Jobs.Should().HaveCount(1);
            result.Value.Jobs[0].ProfileId.Should().Be(5);
            result.Value.Jobs[0].MaxHeight.Should().Be(1440);
            result.Value.Jobs[0].QualityValue.Should().Be(22);

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.Queue(It.Is<TranscodeJob>(job => job.MaxHeight == 1440 && job.ProfileId == 5 && job.Tag == "D1440p")), Times.Once);
        }

        [Test]
        public void create_jobs_should_ignore_an_already_requeued_source()
        {
            GivenCompressionConfig();

            var source = new TranscodeJob
            {
                Id = 7,
                EpisodeFileId = 5,
                RequeuedJobId = 99,
                Status = TranscodeJobStatus.Failed,
                SourcePath = "/media/show/episode.mkv"
            };

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.Find(7))
                  .Returns(source);

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                Force = true,
                RequeueJobIds = new List<int> { 7 }
            });

            result.Value.Jobs.Should().BeEmpty();

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.Queue(It.IsAny<TranscodeJob>()), Times.Never);
        }

        [Test]
        public void create_jobs_should_expand_selected_series_to_their_files()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.GetFilesBySeries(3))
                  .Returns(new List<EpisodeFile>
                  {
                      new EpisodeFile { Id = 11, SeriesId = 3, RelativePath = "a.mkv", Size = 100 },
                      new EpisodeFile { Id = 12, SeriesId = 3, RelativePath = "b.mkv", Size = 200 }
                  });

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(It.IsAny<int>()))
                  .Returns<int>(id => new EpisodeFile { Id = id, SeriesId = 3, RelativePath = id + ".mkv", Size = 100 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(3))
                  .Returns(new Series { Id = 3, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                SeriesIds = new List<int> { 3 }
            });

            result.Value.Jobs.Should().HaveCount(2);
        }

        [Test]
        public void create_jobs_should_filter_series_files_by_season()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.GetFilesBySeries(3))
                  .Returns(new List<EpisodeFile>
                  {
                      new EpisodeFile { Id = 11, SeriesId = 3, SeasonNumber = 1, RelativePath = "s1.mkv", Size = 100 },
                      new EpisodeFile { Id = 12, SeriesId = 3, SeasonNumber = 2, RelativePath = "s2.mkv", Size = 200 },
                      new EpisodeFile { Id = 13, SeriesId = 3, SeasonNumber = 2, RelativePath = "s2b.mkv", Size = 300 },
                      new EpisodeFile { Id = 14, SeriesId = 3, SeasonNumber = 0, RelativePath = "special.mkv", Size = 50 }
                  });

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(It.IsAny<int>()))
                  .Returns<int>(id => new EpisodeFile { Id = id, SeriesId = 3, RelativePath = id + ".mkv", Size = 100 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(3))
                  .Returns(new Series { Id = 3, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                SeriesIds = new List<int> { 3 },
                SeasonNumbers = new List<int> { 2 }
            });

            result.Value.Jobs.Should().HaveCount(2);
            result.Value.Jobs.Select(job => job.EpisodeFileId).Should().BeEquivalentTo(new int?[] { 12, 13 });
        }

        [Test]
        public void create_jobs_should_apply_the_selected_profile()
        {
            GivenCompressionConfig();

            Mocker.GetMock<ITranscodeProfileService>()
                  .Setup(service => service.Find(7))
                  .Returns(new TranscodeProfile
                  {
                      Id = 7,
                      Name = "Remux to MKV",
                      Mode = TranscodeMode.Remux,
                      Container = "mkv"
                  });

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mp4", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                ProfileId = 7
            });

            result.Value.Jobs.Should().HaveCount(1);
            result.Value.Jobs[0].Mode.Should().Be("Remux");
            result.Value.Jobs[0].Container.Should().Be("mkv");
            result.Value.Jobs[0].ProfileId.Should().Be(7);
        }

        [Test]
        public void create_jobs_should_apply_max_height_from_the_profile_and_allow_a_request_override()
        {
            GivenCompressionConfig();

            Mocker.GetMock<ITranscodeProfileService>()
                  .Setup(service => service.Find(9))
                  .Returns(new TranscodeProfile
                  {
                      Id = 9,
                      Name = "Downscale to 1080p",
                      Codec = "hevc",
                      Mode = TranscodeMode.Quality,
                      QualityValue = 22,
                      MaxHeight = 1080
                  });

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var fromProfile = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                ProfileId = 9
            });

            fromProfile.Value.Jobs[0].MaxHeight.Should().Be(1080);

            var fromRequest = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                ProfileId = 9,
                MaxHeight = 720
            });

            fromRequest.Value.Jobs[0].MaxHeight.Should().Be(720);
        }

        [Test]
        public void create_jobs_should_reject_an_unknown_remux_container()
        {
            GivenCompressionConfig();

            var act = () => Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Mode = "Remux",
                Container = "../../x"
            });

            act.Should().Throw<BadRequestException>();

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.Queue(It.IsAny<TranscodeJob>()), Times.Never);
        }

        [Test]
        public void create_jobs_should_normalize_the_remux_container()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Mode = "Remux",
                Container = " MOV "
            });

            result.Value.Jobs.Should().HaveCount(1);
            result.Value.Jobs[0].Container.Should().Be("mov");
        }

        [Test]
        public void create_jobs_should_ignore_a_container_when_the_mode_is_not_remux()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Mode = "Quality",
                Container = "../../x"
            });

            result.Value.Jobs.Should().HaveCount(1);
            result.Value.Jobs[0].Container.Should().BeNull();
        }

        [Test]
        public void create_jobs_should_validate_a_container_when_the_config_default_is_remux()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IConfigService>()
                  .SetupGet(config => config.DefaultRateControlMode)
                  .Returns(TranscodeMode.Remux);

            var act = () => Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Container = "avi"
            });

            act.Should().Throw<BadRequestException>();
        }

        [Test]
        public void create_jobs_should_validate_a_container_for_a_remux_profile()
        {
            GivenCompressionConfig();

            Mocker.GetMock<ITranscodeProfileService>()
                  .Setup(service => service.Find(7))
                  .Returns(new TranscodeProfile { Id = 7, Name = "Remux", Mode = TranscodeMode.Remux, Container = "mkv" });

            var act = () => Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                ProfileId = 7,
                Container = "avi"
            });

            act.Should().Throw<BadRequestException>();
        }

        [Test]
        public void create_jobs_should_treat_a_blank_preset_as_unset()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Preset = "   "
            });

            result.Value.Jobs.Should().HaveCount(1);
            result.Value.Jobs[0].Preset.Should().Be("medium");
        }

        [Test]
        public void create_jobs_should_reject_a_preset_with_control_characters()
        {
            GivenCompressionConfig();

            var act = () => Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Preset = "medium\nslow"
            });

            act.Should().Throw<BadRequestException>();
        }

        [Test]
        public void create_jobs_should_skip_a_file_that_already_has_an_active_job()
        {
            GivenCompressionConfig();

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.GetActive())
                  .Returns(new List<TranscodeJob>
                  {
                      new TranscodeJob { Id = 1, Status = TranscodeJobStatus.Queued, EpisodeFileId = 5 }
                  });

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 }
            });

            result.Value.Jobs.Should().BeEmpty();

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.Queue(It.IsAny<TranscodeJob>()), Times.Never);
        }

        [Test]
        public void create_jobs_should_skip_a_file_whose_source_path_already_has_an_active_job()
        {
            GivenCompressionConfig();

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.GetActive())
                  .Returns(new List<TranscodeJob>
                  {
                      new TranscodeJob { Id = 1, Status = TranscodeJobStatus.Running, SourcePath = "/media/show/episode.mkv" }
                  });

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 }
            });

            result.Value.Jobs.Should().BeEmpty();
        }

        [Test]
        public void create_jobs_should_not_queue_the_same_source_path_twice()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(6))
                  .Returns(new EpisodeFile { Id = 6, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5, 6 }
            });

            result.Value.Jobs.Should().HaveCount(1);

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.Queue(It.IsAny<TranscodeJob>()), Times.Once);
        }

        [Test]
        public void force_stop_without_ids_should_stop_every_blocked_job()
        {
            var blocked = new List<TranscodeJob>
            {
                new TranscodeJob { Id = 4, Status = TranscodeJobStatus.Running, DeviceId = "software" },
                new TranscodeJob { Id = 5, Status = TranscodeJobStatus.Running, DeviceId = "software" }
            };

            Mocker.GetMock<ITranscodeService>()
                  .Setup(service => service.GetJobsExceedingSettings())
                  .Returns(blocked);

            Mocker.GetMock<ITranscodeService>()
                  .Setup(service => service.ForceStopJobs(It.IsAny<IEnumerable<int>>()))
                  .Returns(blocked);

            var result = Subject.ForceStopJobs(new ForceStopTranscodeJobsResource());

            result.Value.Should().HaveCount(2);

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.ForceStopJobs(It.Is<IEnumerable<int>>(ids => ids.OrderBy(id => id).SequenceEqual(new[] { 4, 5 }))), Times.Once);
        }

        [Test]
        public void force_stop_with_ids_should_pass_them_through()
        {
            var stopped = new List<TranscodeJob> { new TranscodeJob { Id = 9 } };

            Mocker.GetMock<ITranscodeService>()
                  .Setup(service => service.ForceStopJobs(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 9 }))))
                  .Returns(stopped);

            var result = Subject.ForceStopJobs(new ForceStopTranscodeJobsResource { JobIds = new List<int> { 9 } });

            result.Value.Single().Id.Should().Be(9);
        }

        [Test]
        public void blocked_jobs_should_be_returned()
        {
            Mocker.GetMock<ITranscodeService>()
                  .Setup(service => service.GetJobsExceedingSettings())
                  .Returns(new List<TranscodeJob> { new TranscodeJob { Id = 3, Status = TranscodeJobStatus.Running } });

            Subject.GetBlockedJobs().Value.Should().HaveCount(1);
        }

        [Test]
        public void get_jobs_should_include_active_jobs_and_recent_terminal_jobs()
        {
            var jobs = new List<TranscodeJob>
            {
                new TranscodeJob { Id = 3, Status = TranscodeJobStatus.Running },
                new TranscodeJob { Id = 2, Status = TranscodeJobStatus.Completed }
            };

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.GetRecentIncludingActive(100))
                  .Returns(jobs);

            var result = Subject.GetJobs();

            result.Value.Should().HaveCount(2);

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Verify(repository => repository.GetRecentIncludingActive(100), Times.Once);
        }

        [Test]
        public void get_jobs_should_enrich_the_series_season_and_movie()
        {
            var jobs = new List<TranscodeJob>
            {
                new TranscodeJob { Id = 1, MediaType = MediaType.Series, SeriesId = 5, EpisodeFileId = 50 },
                new TranscodeJob { Id = 2, MediaType = MediaType.Movie, MovieId = 7 }
            };

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.GetRecentIncludingActive(100))
                  .Returns(jobs);

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 5 }))))
                  .Returns(new List<Series> { new Series { Id = 5, Title = "Breaking Bad" } });

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 50 }))))
                  .Returns(new List<EpisodeFile> { new EpisodeFile { Id = 50, SeasonNumber = 3 } });

            Mocker.GetMock<IMovieService>()
                  .Setup(service => service.GetMovies(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 7 }))))
                  .Returns(new List<Movie> { new Movie { Id = 7, Title = "El Camino" } });

            var result = Subject.GetJobs().Value;

            result.Should().Contain(resource => resource.SeriesTitle == "Breaking Bad" && resource.SeasonNumber == 3);
            result.Should().Contain(resource => resource.MovieTitle == "El Camino");
        }

        [Test]
        public void bulk_resolve_without_ids_should_be_rejected()
        {
            var act = () => Subject.BulkResolveJobs(new BulkResolveTranscodeJobsResource { Action = "KeepBoth" });

            act.Should().Throw<BadRequestException>();

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.ResolveJobs(It.IsAny<IEnumerable<int>>(), It.IsAny<TranscodeReviewAction>()), Times.Never);
        }

        [Test]
        public void bulk_resolve_with_a_null_body_should_be_rejected()
        {
            var act = () => Subject.BulkResolveJobs(null);

            act.Should().Throw<BadRequestException>();

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.ResolveJobs(It.IsAny<IEnumerable<int>>(), It.IsAny<TranscodeReviewAction>()), Times.Never);
        }

        [Test]
        public void bulk_resolve_without_an_action_should_use_the_configured_default()
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(config => config.TranscodeReviewDefault)
                  .Returns(TranscodeReviewAction.Discard);

            Mocker.GetMock<ITranscodeService>()
                  .Setup(service => service.ResolveJobs(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 7 })), TranscodeReviewAction.Discard))
                  .Returns(new List<TranscodeResolveResult> { new TranscodeResolveResult(7, new TranscodeJob { Id = 7 }, null) });

            var result = Subject.BulkResolveJobs(new BulkResolveTranscodeJobsResource
            {
                JobIds = new List<int> { 7 }
            });

            result.Value.Resolved.Single().Id.Should().Be(7);
            result.Value.Failed.Should().BeEmpty();
        }

        [Test]
        public void bulk_resolve_should_report_failed_ids_with_their_reason()
        {
            Mocker.GetMock<ITranscodeService>()
                  .Setup(service => service.ResolveJobs(It.IsAny<IEnumerable<int>>(), It.IsAny<TranscodeReviewAction>()))
                  .Returns(new List<TranscodeResolveResult>
                  {
                      new TranscodeResolveResult(7, new TranscodeJob { Id = 7 }, null),
                      new TranscodeResolveResult(8, null, "The transcoded output file is missing")
                  });

            var result = Subject.BulkResolveJobs(new BulkResolveTranscodeJobsResource
            {
                Action = "Overwrite",
                JobIds = new List<int> { 7, 8 }
            });

            result.Value.Resolved.Single().Id.Should().Be(7);
            result.Value.Failed.Single().JobId.Should().Be(8);
            result.Value.Failed.Single().Error.Should().Contain("missing");
        }

        [Test]
        public void resolve_job_with_an_unknown_action_should_be_rejected()
        {
            var act = () => Subject.ResolveJob(9, new ResolveTranscodeJobResource { Action = "nuke" });

            act.Should().Throw<BadRequestException>();

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.Resolve(It.IsAny<int>(), It.IsAny<TranscodeReviewAction>()), Times.Never);
        }

        [Test]
        public void resolve_job_without_an_action_should_use_the_configured_default()
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(config => config.TranscodeReviewDefault)
                  .Returns(TranscodeReviewAction.Discard);

            Mocker.GetMock<ITranscodeService>()
                  .Setup(service => service.Resolve(9, TranscodeReviewAction.Discard))
                  .Returns(new TranscodeJob { Id = 9, Status = TranscodeJobStatus.Completed });

            var result = Subject.ResolveJob(9, new ResolveTranscodeJobResource());

            result.Value.Id.Should().Be(9);

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.Resolve(9, TranscodeReviewAction.Discard), Times.Once);
        }

        [Test]
        public void bulk_resolve_with_ids_should_pass_them_and_the_action()
        {
            Mocker.GetMock<ITranscodeService>()
                  .Setup(service => service.ResolveJobs(It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 7 })), TranscodeReviewAction.Discard))
                  .Returns(new List<TranscodeResolveResult> { new TranscodeResolveResult(7, new TranscodeJob { Id = 7 }, null) });

            var result = Subject.BulkResolveJobs(new BulkResolveTranscodeJobsResource
            {
                Action = "Discard",
                JobIds = new List<int> { 7 }
            });

            result.Value.Resolved.Single().Id.Should().Be(7);
        }

        [Test]
        public void cancel_job_should_delegate_to_the_service()
        {
            Subject.CancelJob(7);

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.CancelJob(7), Times.Once);
        }

        [Test]
        public void resolve_job_should_pass_the_action()
        {
            Mocker.GetMock<ITranscodeService>()
                  .Setup(service => service.Resolve(9, TranscodeReviewAction.KeepBoth))
                  .Returns(new TranscodeJob { Id = 9, Status = TranscodeJobStatus.Completed });

            var result = Subject.ResolveJob(9, new ResolveTranscodeJobResource { Action = "KeepBoth" });

            result.Value.Id.Should().Be(9);
            result.Value.Status.Should().Be("Completed");
        }

        [Test]
        public void create_jobs_should_report_projected_savings_for_percentage_reduction()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Mode = "PercentageReduction",
                TargetPercent = 50
            });

            result.Value.ProjectedSavingsBytes.Should().Be(500_000);
        }

        [Test]
        public void create_jobs_should_report_projected_savings_for_target_size()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Mode = "TargetSize",
                TargetSize = 400_000
            });

            result.Value.ProjectedSavingsBytes.Should().Be(600_000);
        }

        [Test]
        public void create_jobs_should_report_zero_projected_savings_for_quality()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Mode = "Quality"
            });

            result.Value.ProjectedSavingsBytes.Should().Be(0);
        }

        [Test]
        public void create_jobs_should_assign_a_rate_control_label()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(It.IsAny<int>()))
                  .Returns<int>(id => new EpisodeFile { Id = id, SeriesId = 1, RelativePath = id + ".mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var quality = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Mode = "Quality"
            });

            quality.Value.Jobs[0].RateControl.Should().Be("constant-quality");

            var percentage = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 6 },
                Mode = "PercentageReduction"
            });

            percentage.Value.Jobs[0].RateControl.Should().Be("vbr");

            var remux = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 7 },
                Mode = "Remux",
                Container = "mkv"
            });

            remux.Value.Jobs[0].RateControl.Should().BeNull();
        }

        [Test]
        public void create_jobs_should_bypass_the_active_job_dedup_when_forced()
        {
            GivenCompressionConfig();

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.GetActive())
                  .Returns(new List<TranscodeJob>
                  {
                      new TranscodeJob { Id = 1, Status = TranscodeJobStatus.Queued, EpisodeFileId = 5 }
                  });

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Force = true
            });

            result.Value.Jobs.Should().HaveCount(1);
        }

        [Test]
        public void create_jobs_should_bypass_the_active_source_path_dedup_when_forced()
        {
            GivenCompressionConfig();

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.GetActive())
                  .Returns(new List<TranscodeJob>
                  {
                      new TranscodeJob { Id = 1, Status = TranscodeJobStatus.Running, SourcePath = "/media/show/episode.mkv" }
                  });

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Force = true
            });

            result.Value.Jobs.Should().HaveCount(1);
        }

        [Test]
        public void create_jobs_should_treat_case_only_distinct_paths_as_distinct_on_linux()
        {
            if (OsInfo.IsWindows)
            {
                Assert.Ignore("Path comparison is intentionally case-insensitive on Windows");
            }

            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(6))
                  .Returns(new EpisodeFile { Id = 6, SeriesId = 1, RelativePath = "Episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5, 6 }
            });

            result.Value.Jobs.Should().HaveCount(2);
        }

        [Test]
        public void create_jobs_should_skip_a_duplicate_on_a_second_sequential_request()
        {
            GivenCompressionConfig();

            var active = new List<TranscodeJob>();

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.GetActive())
                  .Returns(() => active.ToList());

            Mocker.GetMock<ITranscodeService>()
                  .Setup(service => service.Queue(It.IsAny<TranscodeJob>()))
                  .Returns((TranscodeJob job) =>
                  {
                      job.Id = active.Count + 1;
                      active.Add(job);
                      return job;
                  });

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var first = Subject.CreateJobs(new TranscodeJobRequestResource { EpisodeFileIds = new List<int> { 5 } });
            var second = Subject.CreateJobs(new TranscodeJobRequestResource { EpisodeFileIds = new List<int> { 5 } });

            first.Value.Jobs.Should().HaveCount(1);
            second.Value.Jobs.Should().BeEmpty();

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.Queue(It.IsAny<TranscodeJob>()), Times.Once);
        }

        [Test]
        public void create_jobs_should_skip_a_vanished_episode_file()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Throws(new ModelNotFoundException(typeof(EpisodeFile), 5));

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(6))
                  .Returns(new EpisodeFile { Id = 6, SeriesId = 1, RelativePath = "b.mkv", Size = 100 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5, 6 }
            });

            result.Value.Jobs.Should().HaveCount(1);
            result.Value.Jobs[0].EpisodeFileId.Should().Be(6);
        }

        [Test]
        public void create_jobs_should_skip_a_file_whose_series_vanished()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Throws(new ModelNotFoundException(typeof(Series), 1));

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 }
            });

            result.Value.Jobs.Should().BeEmpty();
        }

        [Test]
        public void create_jobs_should_skip_a_vanished_movie_file()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMovieFileService>()
                  .Setup(service => service.GetMovie(9))
                  .Throws(new ModelNotFoundException(typeof(MovieFile), 9));

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                MovieFileIds = new List<int> { 9 }
            });

            result.Value.Jobs.Should().BeEmpty();
        }

        [Test]
        public void create_jobs_should_clamp_an_out_of_range_target_percent()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Mode = "PercentageReduction",
                TargetPercent = -50
            });

            result.Value.Jobs[0].TargetPercent.Should().Be(1);
            result.Value.ProjectedSavingsBytes.Should().Be(990_000);
            result.Value.ProjectedSavingsBytes.Should().BeLessThanOrEqualTo(1_000_000);
        }

        [Test]
        public void create_jobs_should_fall_back_to_the_default_target_when_target_size_is_not_positive()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(It.IsAny<int>()))
                  .Returns<int>(id => new EpisodeFile { Id = id, SeriesId = 1, RelativePath = id + ".mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var expected = 1500L * 1024 * 1024;

            var zero = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Mode = "TargetSize",
                TargetSize = 0
            });

            zero.Value.Jobs[0].TargetSize.Should().Be(expected);

            var negative = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 6 },
                Mode = "TargetSize",
                TargetSize = -1
            });

            negative.Value.Jobs[0].TargetSize.Should().Be(expected);
        }

        [Test]
        public void create_jobs_should_clamp_an_out_of_range_quality()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(It.IsAny<int>()))
                  .Returns<int>(id => new EpisodeFile { Id = id, SeriesId = 1, RelativePath = id + ".mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var low = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Mode = "Quality",
                Quality = -1
            });

            low.Value.Jobs[0].QualityValue.Should().Be(0);

            var high = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 6 },
                Mode = "Quality",
                Quality = 200
            });

            high.Value.Jobs[0].QualityValue.Should().Be(51);
        }

        [Test]
        public void create_jobs_should_normalize_the_request_codec()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(It.IsAny<int>()))
                  .Returns<int>(id => new EpisodeFile { Id = id, SeriesId = 1, RelativePath = id + ".mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var typo = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Codec = "hvec"
            });

            typo.Value.Jobs[0].VideoCodec.Should().Be("hevc");

            var upper = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 6 },
                Codec = "H264"
            });

            upper.Value.Jobs[0].VideoCodec.Should().Be("h264");
        }

        [Test]
        public void create_jobs_should_trim_a_mode()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Mode = "remux ",
                Container = "mkv"
            });

            result.Value.Jobs[0].Mode.Should().Be("Remux");
            result.Value.Jobs[0].Container.Should().Be("mkv");
        }

        [Test]
        public void create_jobs_should_reject_an_unknown_mode()
        {
            GivenCompressionConfig();

            var act = () => Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Mode = "Remuz"
            });

            act.Should().Throw<BadRequestException>();

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.Queue(It.IsAny<TranscodeJob>()), Times.Never);
        }

        [Test]
        public void create_jobs_should_not_404_a_stale_profile_when_there_is_nothing_to_queue()
        {
            GivenCompressionConfig();

            Mocker.GetMock<ITranscodeProfileService>()
                  .Setup(service => service.Find(99))
                  .Returns((TranscodeProfile)null);

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                ProfileId = 99
            });

            result.Value.Jobs.Should().BeEmpty();
            result.Value.RequestedCount.Should().Be(0);
            result.Value.SkippedCount.Should().Be(0);
        }

        [Test]
        public void create_jobs_should_404_a_stale_profile_when_there_is_work()
        {
            GivenCompressionConfig();

            Mocker.GetMock<ITranscodeProfileService>()
                  .Setup(service => service.Find(99))
                  .Returns((TranscodeProfile)null);

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var act = () => Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                ProfileId = 99
            });

            act.Should().Throw<NotFoundException>();

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.Queue(It.IsAny<TranscodeJob>()), Times.Never);
        }

        [Test]
        public void create_jobs_should_report_requested_and_skipped_counts()
        {
            GivenCompressionConfig();

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.GetActive())
                  .Returns(new List<TranscodeJob>
                  {
                      new TranscodeJob { Id = 1, Status = TranscodeJobStatus.Queued, EpisodeFileId = 6 }
                  });

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(It.IsAny<int>()))
                  .Returns<int>(id => new EpisodeFile { Id = id, SeriesId = 1, RelativePath = id + ".mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5, 6, 7 }
            });

            result.Value.Jobs.Should().HaveCount(2);
            result.Value.RequestedCount.Should().Be(3);
            result.Value.SkippedCount.Should().Be(1);
        }

        [Test]
        public void update_devices_should_reject_an_unknown_device_id()
        {
            Mocker.GetMock<IGpuCapabilityService>()
                  .Setup(service => service.GetDevices())
                  .Returns(new List<TranscodeDevice> { new TranscodeDevice { Id = "software" } });

            var act = () => Subject.UpdateDevices(new List<TranscodeDeviceResource>
            {
                new TranscodeDeviceResource { DeviceId = "ghost" }
            });

            act.Should().Throw<BadRequestException>();

            Mocker.GetMock<IGpuCapabilityService>()
                  .Verify(service => service.UpdateDevices(It.IsAny<List<TranscodeDevice>>()), Times.Never);
        }

        [Test]
        public void update_devices_should_accept_a_known_device_id()
        {
            Mocker.GetMock<IGpuCapabilityService>()
                  .Setup(service => service.GetDevices())
                  .Returns(new List<TranscodeDevice> { new TranscodeDevice { Id = "software" } });

            Mocker.GetMock<IGpuCapabilityService>()
                  .Setup(service => service.UpdateDevices(It.IsAny<List<TranscodeDevice>>()))
                  .Returns(new List<TranscodeDevice> { new TranscodeDevice { Id = "software" } });

            var result = Subject.UpdateDevices(new List<TranscodeDeviceResource>
            {
                new TranscodeDeviceResource { DeviceId = "software", Enabled = true }
            });

            result.Value.Should().HaveCount(1);

            Mocker.GetMock<ITranscodeService>().Verify(service => service.Wake(), Times.Once);
        }

        [Test]
        public void add_profile_should_reject_a_blank_name()
        {
            var act = () => Subject.AddProfile(new TranscodeProfileResource { Name = "   ", Mode = "Quality" });

            act.Should().Throw<BadRequestException>();

            Mocker.GetMock<ITranscodeProfileService>()
                  .Verify(service => service.Add(It.IsAny<TranscodeProfile>()), Times.Never);
        }

        [Test]
        public void add_profile_should_reject_a_duplicate_name()
        {
            Mocker.GetMock<ITranscodeProfileService>()
                  .Setup(service => service.GetAll())
                  .Returns(new List<TranscodeProfile> { new TranscodeProfile { Id = 1, Name = "Archive" } });

            var act = () => Subject.AddProfile(new TranscodeProfileResource { Name = "archive", Mode = "Quality" });

            act.Should().Throw<BadRequestException>();

            Mocker.GetMock<ITranscodeProfileService>()
                  .Verify(service => service.Add(It.IsAny<TranscodeProfile>()), Times.Never);
        }

        [Test]
        public void add_profile_should_reject_an_unknown_mode()
        {
            Mocker.GetMock<ITranscodeProfileService>()
                  .Setup(service => service.GetAll())
                  .Returns(new List<TranscodeProfile>());

            var act = () => Subject.AddProfile(new TranscodeProfileResource { Name = "New", Mode = "Remuz" });

            act.Should().Throw<BadRequestException>();

            Mocker.GetMock<ITranscodeProfileService>()
                  .Verify(service => service.Add(It.IsAny<TranscodeProfile>()), Times.Never);
        }

        [Test]
        public void update_profile_should_reject_an_unknown_mode()
        {
            Mocker.GetMock<ITranscodeProfileService>()
                  .Setup(service => service.Get(3))
                  .Returns(new TranscodeProfile { Id = 3, Name = "Archive" });

            Mocker.GetMock<ITranscodeProfileService>()
                  .Setup(service => service.GetAll())
                  .Returns(new List<TranscodeProfile> { new TranscodeProfile { Id = 3, Name = "Archive" } });

            var act = () => Subject.UpdateProfile(3, new TranscodeProfileResource { Name = "Archive", Mode = "Remuz" });

            act.Should().Throw<BadRequestException>();

            Mocker.GetMock<ITranscodeProfileService>()
                  .Verify(service => service.Update(It.IsAny<TranscodeProfile>()), Times.Never);
        }

        [Test]
        public void update_profile_should_reject_a_name_used_by_another_profile()
        {
            Mocker.GetMock<ITranscodeProfileService>()
                  .Setup(service => service.Get(3))
                  .Returns(new TranscodeProfile { Id = 3, Name = "Archive" });

            Mocker.GetMock<ITranscodeProfileService>()
                  .Setup(service => service.GetAll())
                  .Returns(new List<TranscodeProfile>
                  {
                      new TranscodeProfile { Id = 3, Name = "Archive" },
                      new TranscodeProfile { Id = 4, Name = "Small" }
                  });

            var act = () => Subject.UpdateProfile(3, new TranscodeProfileResource { Name = "small", Mode = "Quality" });

            act.Should().Throw<BadRequestException>();

            Mocker.GetMock<ITranscodeProfileService>()
                  .Verify(service => service.Update(It.IsAny<TranscodeProfile>()), Times.Never);
        }

        [Test]
        public void add_profile_should_accept_a_valid_profile()
        {
            Mocker.GetMock<ITranscodeProfileService>()
                  .Setup(service => service.GetAll())
                  .Returns(new List<TranscodeProfile>());

            Mocker.GetMock<ITranscodeProfileService>()
                  .Setup(service => service.Add(It.IsAny<TranscodeProfile>()))
                  .Returns((TranscodeProfile profile) =>
                  {
                      profile.Id = 9;
                      return profile;
                  });

            var result = Subject.AddProfile(new TranscodeProfileResource { Name = "New", Mode = "Quality" });

            result.Value.Id.Should().Be(9);

            Mocker.GetMock<ITranscodeProfileService>()
                  .Verify(service => service.Add(It.IsAny<TranscodeProfile>()), Times.Once);
        }

        [Test]
        public void cancel_job_should_return_not_found_for_an_unknown_id()
        {
            Mocker.GetMock<ITranscodeService>()
                  .Setup(service => service.CancelJob(99))
                  .Returns(CancelJobResult.NotFound);

            var act = () => Subject.CancelJob(99);

            act.Should().Throw<NotFoundException>();
        }

        [Test]
        public void cancel_job_should_return_conflict_for_a_non_cancellable_job()
        {
            Mocker.GetMock<ITranscodeService>()
                  .Setup(service => service.CancelJob(7))
                  .Returns(CancelJobResult.NotCancellable);

            var act = () => Subject.CancelJob(7);

            act.Should().Throw<ConflictException>();
        }

        [Test]
        public void cancel_job_should_return_ok_when_it_cancels()
        {
            Mocker.GetMock<ITranscodeService>()
                  .Setup(service => service.CancelJob(7))
                  .Returns(CancelJobResult.Cancelled);

            Subject.CancelJob(7);

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.CancelJob(7), Times.Once);
        }

        [Test]
        public void create_jobs_should_pass_the_priority_through()
        {
            GivenCompressionConfig();

            Mocker.GetMock<IMediaFileService>()
                  .Setup(service => service.Get(5))
                  .Returns(new EpisodeFile { Id = 5, SeriesId = 1, RelativePath = "episode.mkv", Size = 1_000_000 });

            Mocker.GetMock<ISeriesService>()
                  .Setup(service => service.GetSeries(1))
                  .Returns(new Series { Id = 1, Path = "/media/show" });

            var result = Subject.CreateJobs(new TranscodeJobRequestResource
            {
                EpisodeFileIds = new List<int> { 5 },
                Priority = 7
            });

            result.Value.Jobs[0].Priority.Should().Be(7);

            Mocker.GetMock<ITranscodeService>()
                  .Verify(service => service.Queue(It.Is<TranscodeJob>(job => job.Priority == 7)), Times.Once);
        }
    }
}
