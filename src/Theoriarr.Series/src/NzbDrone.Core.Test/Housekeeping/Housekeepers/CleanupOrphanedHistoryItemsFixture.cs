using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.History;
using NzbDrone.Core.Housekeeping.Housekeepers;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.Housekeeping.Housekeepers
{
    [TestFixture]
    public class CleanupOrphanedHistoryItemsFixture : DbTest<CleanupOrphanedHistoryItems, EpisodeHistory>
    {
        private Series _series;
        private Episode _episode;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                                     .BuildNew();

            _episode = Builder<Episode>.CreateNew()
                                       .BuildNew();
        }

        private void GivenSeries()
        {
            Db.Insert(_series);
        }

        private void GivenEpisode()
        {
            Db.Insert(_episode);
        }

        [Test]
        public void should_delete_orphaned_items_by_series()
        {
            GivenEpisode();

            var history = Builder<EpisodeHistory>.CreateNew()
                .With(h => h.Languages = new List<Language> { Language.English })
                .With(h => h.Quality = new QualityModel())
                .With(h => h.EpisodeId = _episode.Id)
                .BuildNew();
            Db.Insert(history);

            Subject.Clean();
            AllStoredModels.Should().BeEmpty();
        }

        [Test]
        public void should_delete_orphaned_items_by_episode()
        {
            GivenSeries();

            var history = Builder<EpisodeHistory>.CreateNew()
                .With(h => h.Languages = new List<Language> { Language.English })
                .With(h => h.Quality = new QualityModel())
                .With(h => h.SeriesId = _series.Id)
                .BuildNew();
            Db.Insert(history);

            Subject.Clean();
            AllStoredModels.Should().BeEmpty();
        }

        [Test]
        public void should_not_delete_unorphaned_data_by_series()
        {
            GivenSeries();
            GivenEpisode();

            var history = Builder<EpisodeHistory>.CreateListOfSize(2)
                .All()
                .With(h => h.Languages = new List<Language> { Language.English })
                .With(h => h.Quality = new QualityModel())
                .With(h => h.EpisodeId = _episode.Id)
                .TheFirst(1)
                .With(h => h.SeriesId = _series.Id)
                .BuildListOfNew();

            Db.InsertMany(history);

            Subject.Clean();
            AllStoredModels.Should().HaveCount(1);
            AllStoredModels.Should().Contain(h => h.SeriesId == _series.Id);
        }

        [Test]
        public void should_not_delete_unorphaned_data_by_episode()
        {
            GivenSeries();
            GivenEpisode();

            var history = Builder<EpisodeHistory>.CreateListOfSize(2)
                .All()
                .With(h => h.Languages = new List<Language> { Language.English })
                .With(h => h.Quality = new QualityModel())
                .With(h => h.SeriesId = _series.Id)
                .TheFirst(1)
                .With(h => h.EpisodeId = _episode.Id)
                .BuildListOfNew();

            Db.InsertMany(history);

            Subject.Clean();
            AllStoredModels.Should().HaveCount(1);
            AllStoredModels.Should().Contain(h => h.EpisodeId == _episode.Id);
        }

        [Test]
        public void should_not_delete_movie_history_items_that_have_a_coresponding_movie()
        {
            var movie = Builder<Movie>.CreateNew().BuildNew();
            Db.Insert(movie);

            var history = Builder<MovieHistory>.CreateNew()
                .With(h => h.Languages = new List<Language> { Language.English })
                .With(h => h.Quality = new QualityModel())
                .With(h => h.MovieId = movie.Id)
                .With(h => h.MediaType = MediaType.Movie)
                .BuildNew();

            Db.Insert(history);

            Subject.Clean();
            AllStoredModels.Should().HaveCount(1);
            AllStoredModels.Should().Contain(h => h.MediaType == MediaType.Movie);
        }

        [Test]
        public void should_delete_orphaned_movie_history_items()
        {
            var history = Builder<MovieHistory>.CreateNew()
                .With(h => h.Languages = new List<Language> { Language.English })
                .With(h => h.Quality = new QualityModel())
                .With(h => h.MovieId = 12345)
                .With(h => h.MediaType = MediaType.Movie)
                .BuildNew();

            Db.Insert(history);

            Subject.Clean();
            Db.All<MovieHistory>().Should().BeEmpty();
        }
    }
}
