using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.Transcoding
{
    [TestFixture]
    public class TranscodeProfileServiceFixture : CoreTest<TranscodeProfileService>
    {
        private List<TranscodeProfile> _profiles;
        private int _nextId;

        [SetUp]
        public void Setup()
        {
            _profiles = new List<TranscodeProfile>();
            _nextId = 1;

            Mocker.GetMock<ITranscodeProfileRepository>()
                  .Setup(repository => repository.All())
                  .Returns(() => _profiles.ToList());

            Mocker.GetMock<ITranscodeProfileRepository>()
                  .Setup(repository => repository.Get(It.IsAny<int>()))
                  .Returns<int>(id => _profiles.FirstOrDefault(profile => profile.Id == id));

            Mocker.GetMock<ITranscodeProfileRepository>()
                  .Setup(repository => repository.Insert(It.IsAny<TranscodeProfile>()))
                  .Returns<TranscodeProfile>(profile =>
                  {
                      profile.Id = _nextId++;
                      _profiles.Add(profile);
                      return profile;
                  });

            Mocker.GetMock<ITranscodeProfileRepository>()
                  .Setup(repository => repository.Update(It.IsAny<TranscodeProfile>()))
                  .Callback<TranscodeProfile>(profile =>
                  {
                      var index = _profiles.FindIndex(candidate => candidate.Id == profile.Id);

                      if (index >= 0)
                      {
                          _profiles[index] = profile;
                      }
                  });

            Mocker.GetMock<ITranscodeProfileRepository>()
                  .Setup(repository => repository.Delete(It.IsAny<int>()))
                  .Callback<int>(id => _profiles.RemoveAll(profile => profile.Id == id));
        }

        private TranscodeProfile GivenProfile(string name, bool isDefault = false)
        {
            var profile = Subject.Add(new TranscodeProfile
            {
                Name = name,
                Codec = "hevc",
                Mode = TranscodeMode.Quality,
                QualityValue = 22,
                IsDefault = isDefault
            });

            return profile;
        }

        [Test]
        public void add_should_make_the_first_profile_the_default()
        {
            var profile = GivenProfile("First");

            profile.IsDefault.Should().BeTrue();
        }

        [Test]
        public void add_should_clear_other_defaults_when_a_new_default_is_created()
        {
            var first = GivenProfile("First");
            var second = GivenProfile("Second", isDefault: true);

            first.IsDefault.Should().BeFalse();
            second.IsDefault.Should().BeTrue();
        }

        [Test]
        public void add_should_normalize_a_remux_profile()
        {
            var profile = Subject.Add(new TranscodeProfile
            {
                Name = "  Remux to MKV  ",
                Codec = "hevc",
                Mode = TranscodeMode.Remux,
                Container = "MKV",
                Preset = "medium",
                DeviceId = "nvenc:0",
                QualityValue = 18
            });

            profile.Name.Should().Be("Remux to MKV");
            profile.Codec.Should().BeNull();
            profile.Container.Should().Be("mkv");
            profile.Preset.Should().BeNull();
            profile.DeviceId.Should().BeNull();
            profile.QualityValue.Should().Be(0);
        }

        [Test]
        public void add_should_normalize_max_height_and_clear_it_for_remux()
        {
            var capped = Subject.Add(new TranscodeProfile
            {
                Name = "Downscale",
                Codec = "hevc",
                Mode = TranscodeMode.Quality,
                MaxHeight = 1080
            });

            capped.MaxHeight.Should().Be(1080);

            var uncapped = Subject.Add(new TranscodeProfile
            {
                Name = "Uncapped",
                Codec = "hevc",
                Mode = TranscodeMode.Quality,
                MaxHeight = 0
            });

            uncapped.MaxHeight.Should().BeNull();

            var remux = Subject.Add(new TranscodeProfile
            {
                Name = "Remux HD",
                Mode = TranscodeMode.Remux,
                Container = "mkv",
                MaxHeight = 1080
            });

            remux.MaxHeight.Should().BeNull();
        }

        [Test]
        public void add_should_normalize_the_tag()
        {
            var profile = Subject.Add(new TranscodeProfile
            {
                Name = "Tagged",
                Codec = "hevc",
                Mode = TranscodeMode.Quality,
                Tag = "  Transcoded/ [x]  "
            });

            profile.Tag.Should().Be("Transcoded x");

            var blank = Subject.Add(new TranscodeProfile
            {
                Name = "Blank",
                Codec = "hevc",
                Mode = TranscodeMode.Quality,
                Tag = "   "
            });

            blank.Tag.Should().BeNull();
        }

        [Test]
        public void add_should_reject_an_unknown_codec_and_container()
        {
            var remux = Subject.Add(new TranscodeProfile
            {
                Name = "Odd",
                Mode = TranscodeMode.Remux,
                Container = "avi"
            });

            remux.Container.Should().Be("mkv");

            var quality = Subject.Add(new TranscodeProfile
            {
                Name = "Odd2",
                Codec = "mpeg2",
                Mode = TranscodeMode.Quality
            });

            quality.Codec.Should().Be("hevc");
            quality.Container.Should().BeNull();
        }

        [Test]
        public void delete_should_promote_another_profile_to_default()
        {
            var first = GivenProfile("First");
            var second = GivenProfile("Second");

            Subject.Delete(first.Id);

            second.IsDefault.Should().BeTrue();
        }

        [Test]
        public void add_should_drop_an_invalid_preset()
        {
            var control = Subject.Add(new TranscodeProfile
            {
                Name = "Control",
                Codec = "hevc",
                Mode = TranscodeMode.Quality,
                Preset = "medium\nslow"
            });

            control.Preset.Should().BeNull();

            var overlong = Subject.Add(new TranscodeProfile
            {
                Name = "Overlong",
                Codec = "hevc",
                Mode = TranscodeMode.Quality,
                Preset = new string('a', 200)
            });

            overlong.Preset.Should().BeNull();

            var injected = Subject.Add(new TranscodeProfile
            {
                Name = "Injected",
                Codec = "hevc",
                Mode = TranscodeMode.Quality,
                Preset = "medium -vf scale=1:1"
            });

            injected.Preset.Should().BeNull();
        }

        [Test]
        public void add_should_trim_a_valid_preset()
        {
            var profile = Subject.Add(new TranscodeProfile
            {
                Name = "Valid",
                Codec = "hevc",
                Mode = TranscodeMode.Quality,
                Preset = "  slow  "
            });

            profile.Preset.Should().Be("slow");
        }

        [Test]
        public void add_should_keep_the_mov_container_for_a_remux_profile()
        {
            var profile = Subject.Add(new TranscodeProfile
            {
                Name = "Mov",
                Mode = TranscodeMode.Remux,
                Container = "MOV"
            });

            profile.Container.Should().Be("mov");
        }

        [Test]
        public void update_should_promote_another_profile_when_the_only_default_is_unset()
        {
            var first = GivenProfile("First");
            var second = GivenProfile("Second");

            first.IsDefault = false;
            Subject.Update(first);

            _profiles.Single(profile => profile.Id == first.Id).IsDefault.Should().BeFalse();
            _profiles.Single(profile => profile.Id == second.Id).IsDefault.Should().BeTrue();
            _profiles.Count(profile => profile.IsDefault).Should().Be(1);
        }

        [Test]
        public void add_should_promote_a_default_when_none_exists()
        {
            _profiles.Add(new TranscodeProfile { Id = 1, Name = "Orphan A", Codec = "hevc", Mode = TranscodeMode.Quality });
            _profiles.Add(new TranscodeProfile { Id = 2, Name = "Orphan B", Codec = "hevc", Mode = TranscodeMode.Quality });
            _nextId = 3;

            var added = Subject.Add(new TranscodeProfile
            {
                Name = "New",
                Codec = "hevc",
                Mode = TranscodeMode.Quality
            });

            _profiles.Count(profile => profile.IsDefault).Should().Be(1);
            _profiles.Single(profile => profile.Id == added.Id).IsDefault.Should().BeTrue();
        }

        [Test]
        public void add_should_clamp_quality_to_the_resolved_codec_maximum()
        {
            var hevc = Subject.Add(new TranscodeProfile
            {
                Name = "Hevc",
                Codec = "hevc",
                Mode = TranscodeMode.Quality,
                QualityValue = 60
            });

            hevc.QualityValue.Should().Be(51);

            var av1 = Subject.Add(new TranscodeProfile
            {
                Name = "Av1",
                Codec = "av1",
                Mode = TranscodeMode.Quality,
                QualityValue = 60
            });

            av1.QualityValue.Should().Be(60);
        }

        [Test]
        public void add_should_round_max_height_up_to_even_and_floor_below_two()
        {
            var odd = Subject.Add(new TranscodeProfile
            {
                Name = "Odd",
                Codec = "hevc",
                Mode = TranscodeMode.Quality,
                MaxHeight = 1081
            });

            odd.MaxHeight.Should().Be(1082);

            var tiny = Subject.Add(new TranscodeProfile
            {
                Name = "Tiny",
                Codec = "hevc",
                Mode = TranscodeMode.Quality,
                MaxHeight = 1
            });

            tiny.MaxHeight.Should().Be(2);
        }

        [Test]
        public void update_should_keep_the_only_profile_as_default_when_it_is_unset()
        {
            var only = GivenProfile("Only");
            only.IsDefault.Should().BeTrue();

            only.IsDefault = false;
            Subject.Update(only);

            _profiles.Single(profile => profile.Id == only.Id).IsDefault.Should().BeTrue();
            _profiles.Count(profile => profile.IsDefault).Should().Be(1);
        }
    }
}
