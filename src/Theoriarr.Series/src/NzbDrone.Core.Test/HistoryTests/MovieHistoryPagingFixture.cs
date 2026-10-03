using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.History;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.HistoryTests
{
    [TestFixture]
    public class MovieHistoryPagingFixture : DbTest<HistoryRepository, EpisodeHistory>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<IQualityProfileRepository>(Mocker.Resolve<QualityProfileRepository>());
            Mocker.SetConstant<IQualityProfileRankRepository>(Mocker.Resolve<QualityProfileRankRepository>());
            Mocker.SetConstant<IQualityProfileRankService>(Mocker.Resolve<QualityProfileRankService>());
            Mocker.SetConstant<IQualityProfileService>(Mocker.Resolve<QualityProfileService>());
        }

        [Test]
        public void should_sort_by_movie_metadata_sort_title()
        {
            var profile = AddProfile("A", Quality.HDTV720p, Quality.SDTV);
            var movieZ = AddMovie("Zulu", profile.Id);
            var movieA = AddMovie("Alpha", profile.Id);

            InsertMovieHistory(movieZ.Id, Quality.HDTV720p, Language.English);
            InsertMovieHistory(movieA.Id, Quality.HDTV720p, Language.English);

            var spec = new PagingSpec<MovieHistory>
            {
                Page = 1,
                PageSize = 10,
                SortKey = "movieMetadata.sortTitle",
                SortDirection = SortDirection.Ascending
            };

            var result = Subject.GetPaged(spec, null, null);

            result.Records.Should().HaveCount(2);
            result.Records.Select(r => r.MovieId).Should().Equal(movieA.Id, movieZ.Id);
        }

        [Test]
        public void should_apply_per_call_language_filter_without_leaking_to_the_next_call()
        {
            var profile = AddProfile("A", Quality.HDTV720p, Quality.SDTV);
            var movie = AddMovie("Alpha", profile.Id);

            InsertMovieHistory(movie.Id, Quality.HDTV720p, Language.English);

            var spec = new PagingSpec<MovieHistory>
            {
                Page = 1,
                PageSize = 10,
                SortKey = "date",
                SortDirection = SortDirection.Descending
            };

            var filtered = Subject.GetPaged(spec, new[] { Language.English.Id }, null);
            var unfiltered = Subject.GetPaged(spec, null, null);

            filtered.TotalRecords.Should().Be(1);
            unfiltered.TotalRecords.Should().Be(1);
        }

        private QualityProfile AddProfile(string name, Quality cutoff, params Quality[] qualities)
        {
            var profile = new QualityProfile
            {
                Name = name,
                Cutoff = cutoff.Id,
                Items = qualities.Select(q => new QualityProfileQualityItem { Quality = q, Allowed = true }).ToList()
            };

            Mocker.Resolve<IQualityProfileService>().Add(profile);

            return profile;
        }

        private Movie AddMovie(string sortTitle, int profileId)
        {
            var metadata = new MovieMetadata
            {
                Title = sortTitle,
                SortTitle = sortTitle,
                TmdbId = RandomNumber,
                Year = 2000
            };
            metadata.Id = Db.Insert(metadata).Id;

            var movie = new Movie
            {
                MovieMetadataId = metadata.Id,
                QualityProfileId = profileId,
                Path = $"/movies/{sortTitle}",
                Monitored = true
            };
            movie.Id = Db.Insert(movie).Id;

            return movie;
        }

        private void InsertMovieHistory(int movieId, Quality quality, Language language)
        {
            Subject.Insert(new MovieHistory
            {
                MovieId = movieId,
                SourceTitle = "test",
                Date = DateTime.UtcNow,
                Quality = new QualityModel(quality),
                EventType = MovieHistoryEventType.Grabbed,
                Languages = new List<Language> { language }
            });
        }
    }
}
