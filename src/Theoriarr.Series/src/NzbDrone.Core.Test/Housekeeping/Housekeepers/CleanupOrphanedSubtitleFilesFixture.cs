using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Extras.Files;
using NzbDrone.Core.Extras.Subtitles;
using NzbDrone.Core.Housekeeping.Housekeepers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.Housekeeping.Housekeepers
{
    [TestFixture]
    public class CleanupOrphanedSubtitleFilesFixture : DbTest<CleanupOrphanedSubtitleFiles, SubtitleFile>
    {
        [Test]
        public void should_delete_subtitle_files_that_dont_have_a_coresponding_series()
        {
            var episodeFile = Builder<EpisodeFile>.CreateNew()
                .With(h => h.Quality = new QualityModel())
                .With(h => h.Languages = new List<Language> { Language.English })
                .BuildNew();

            Db.Insert(episodeFile);

            var subtitleFile = Builder<SubtitleFile>.CreateNew()
                                                    .With(m => m.EpisodeFileId = episodeFile.Id)
                                                    .With(m => m.Language = Language.English)
                                                    .BuildNew();

            Db.Insert(subtitleFile);
            Subject.Clean();
            AllStoredModels.Should().BeEmpty();
        }

        [Test]
        public void should_not_delete_subtitle_files_that_have_a_coresponding_series()
        {
            var series = Builder<Series>.CreateNew()
                                        .BuildNew();

            var episodeFile = Builder<EpisodeFile>.CreateNew()
                .With(h => h.Quality = new QualityModel())
                .With(h => h.Languages = new List<Language> { Language.English })
                .BuildNew();

            Db.Insert(series);
            Db.Insert(episodeFile);

            var subtitleFile = Builder<SubtitleFile>.CreateNew()
                                                    .With(m => m.SeriesId = series.Id)
                                                    .With(m => m.EpisodeFileId = episodeFile.Id)
                                                    .With(m => m.Language = Language.English)
                                                    .BuildNew();

            Db.Insert(subtitleFile);
            Subject.Clean();
            AllStoredModels.Should().HaveCount(1);
        }

        [Test]
        public void should_delete_subtitle_files_that_dont_have_a_coresponding_episode_file()
        {
            var series = Builder<Series>.CreateNew()
                                        .BuildNew();

            Db.Insert(series);

            var subtitleFile = Builder<SubtitleFile>.CreateNew()
                                                    .With(m => m.SeriesId = series.Id)
                                                    .With(m => m.EpisodeFileId = 10)
                                                    .With(m => m.Language = Language.English)
                                                    .BuildNew();

            Db.Insert(subtitleFile);
            Subject.Clean();
            AllStoredModels.Should().BeEmpty();
        }

        [Test]
        public void should_not_delete_subtitle_files_that_have_a_coresponding_episode_file()
        {
            var series = Builder<Series>.CreateNew()
                                        .BuildNew();

            var episodeFile = Builder<EpisodeFile>.CreateNew()
                .With(h => h.Quality = new QualityModel())
                .With(h => h.Languages = new List<Language> { Language.English })
                .BuildNew();

            Db.Insert(series);
            Db.Insert(episodeFile);

            var subtitleFile = Builder<SubtitleFile>.CreateNew()
                                                    .With(m => m.SeriesId = series.Id)
                                                    .With(m => m.EpisodeFileId = episodeFile.Id)
                                                    .With(m => m.Language = Language.English)
                                                    .BuildNew();

            Db.Insert(subtitleFile);
            Subject.Clean();
            AllStoredModels.Should().HaveCount(1);
        }

        [Test]
        public void should_delete_subtitle_files_that_have_episodefileid_of_zero()
        {
            var series = Builder<Series>.CreateNew()
                                        .BuildNew();

            Db.Insert(series);

            var subtitleFile = Builder<SubtitleFile>.CreateNew()
                                                 .With(m => m.SeriesId = series.Id)
                                                 .With(m => m.EpisodeFileId = 0)
                                                 .With(m => m.Language = Language.English)
                                                 .BuildNew();

            Db.Insert(subtitleFile);
            Subject.Clean();
            AllStoredModels.Should().HaveCount(0);
        }

        [Test]
        public void should_not_delete_movie_subtitle_files_that_have_a_coresponding_movie()
        {
            var movie = Builder<Movie>.CreateNew()
                                      .BuildNew();

            Db.Insert(movie);

            var subtitleFile = Builder<SubtitleFile>.CreateNew()
                                                    .With(m => m.SeriesId = null)
                                                    .With(m => m.EpisodeFileId = null)
                                                    .With(m => m.MovieId = movie.Id)
                                                    .With(m => m.MovieFileId = null)
                                                    .With(m => m.MediaType = MediaType.Movie)
                                                    .With(m => m.Language = Language.English)
                                                    .BuildNew();

            Db.Insert(subtitleFile);
            Subject.Clean();
            AllStoredModels.Should().HaveCount(1);
        }

        [Test]
        public void should_delete_orphaned_movie_subtitle_files()
        {
            var subtitleFile = Builder<SubtitleFile>.CreateNew()
                                                    .With(m => m.SeriesId = null)
                                                    .With(m => m.EpisodeFileId = null)
                                                    .With(m => m.MovieId = 12345)
                                                    .With(m => m.MovieFileId = null)
                                                    .With(m => m.MediaType = MediaType.Movie)
                                                    .With(m => m.Language = Language.English)
                                                    .BuildNew();

            Db.Insert(subtitleFile);
            Subject.Clean();
            AllStoredModels.Should().BeEmpty();
        }
    }
}
