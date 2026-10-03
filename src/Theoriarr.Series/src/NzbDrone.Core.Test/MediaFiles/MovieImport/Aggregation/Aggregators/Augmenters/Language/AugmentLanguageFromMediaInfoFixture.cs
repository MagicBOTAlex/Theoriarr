using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.MediaFiles.MovieImport.Aggregation.Aggregators.Augmenters.Language;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.MovieImport.Aggregation.Aggregators.Augmenters.Language
{
    [TestFixture]
    public class AugmentLanguageFromMediaInfoFixture : CoreTest<AugmentLanguageFromMediaInfo>
    {
        [Test]
        public void should_return_null_if_media_info_is_null()
        {
            var localMovie = Builder<LocalMovie>.CreateNew()
                                                .With(l => l.MediaInfo = null)
                                                .Build();

            Subject.AugmentLanguage(localMovie, null).Should().Be(null);
        }

        [Test]
        public void should_return_null_if_audio_streams_is_null()
        {
            var mediaInfo = Builder<MediaInfoModel>.CreateNew()
                                                   .With(m => m.AudioStreams = null)
                                                   .Build();

            var localMovie = Builder<LocalMovie>.CreateNew()
                                                .With(l => l.MediaInfo = mediaInfo)
                                                .Build();

            Subject.AugmentLanguage(localMovie, null).Should().BeNull();
        }

        [Test]
        public void should_return_language_for_single_known_language()
        {
            var mediaInfo = Builder<MediaInfoModel>.CreateNew()
                                                   .With(m => m.AudioStreams = [
                                                       new MediaInfoAudioStreamModel { Language = "eng" },
                                                   ])
                                                   .Build();

            var localMovie = Builder<LocalMovie>.CreateNew()
                                                .With(l => l.MediaInfo = mediaInfo)
                                                .Build();

            var result = Subject.AugmentLanguage(localMovie, null);

            result.Languages.Count.Should().Be(1);
            result.Languages.Should().Contain(Core.Languages.Language.English);
        }

        [Test]
        public void should_only_return_one_when_language_duplicated()
        {
            var mediaInfo = Builder<MediaInfoModel>.CreateNew()
                                                   .With(m => m.AudioStreams = [
                                                       new MediaInfoAudioStreamModel { Language = "eng" },
                                                       new MediaInfoAudioStreamModel { Language = "eng" },
                                                   ])
                                                   .Build();

            var localMovie = Builder<LocalMovie>.CreateNew()
                                                .With(l => l.MediaInfo = mediaInfo)
                                                .Build();

            var result = Subject.AugmentLanguage(localMovie, null);

            result.Languages.Count.Should().Be(1);
            result.Languages.Should().Contain(Core.Languages.Language.English);
        }
    }
}
