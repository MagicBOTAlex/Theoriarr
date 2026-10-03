using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.Download.Pending.PendingReleaseServiceTests
{
    [TestFixture]
    public class MoviePendingReleaseServiceFixture : CoreTest<PendingReleaseService>
    {
        private Movie _movie;
        private RemoteMovie _remoteMovie;
        private DownloadDecision _temporarilyRejected;
        private List<PendingRelease> _heldReleases;

        [SetUp]
        public void Setup()
        {
            _movie = Builder<Movie>.CreateNew()
                                   .With(m => m.Id = 1)
                                   .With(m => m.Tags = new HashSet<int>())
                                   .With(m => m.QualityProfile = new QualityProfile { Items = NzbDrone.Core.Test.Qualities.QualityFixture.GetDefaultQualities() })
                                   .Build();

            _remoteMovie = new RemoteMovie
            {
                ParsedMovieInfo = new ParsedMovieInfo
                {
                    Quality = new QualityModel(Quality.HDTV720p),
                    Year = 1998,
                    MovieTitles = new List<string> { "A Movie" }
                },
                Movie = _movie,
                MovieMatchType = MovieMatchType.Title,
                Release = new ReleaseInfo
                {
                    PublishDate = DateTime.UtcNow,
                    Title = "A.Movie.1998",
                    Size = 200,
                    DownloadProtocol = DownloadProtocol.Usenet,
                    Indexer = "Test Indexer"
                }
            };

            _temporarilyRejected = new DownloadDecision(_remoteMovie, new DownloadRejection(DownloadRejectionReason.MinimumAgeDelay, "Temp Rejected", RejectionType.Temporary));

            _heldReleases = new List<PendingRelease>();

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Setup(s => s.All())
                  .Returns(_heldReleases);

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Setup(s => s.AllByMovieId(It.IsAny<int>()))
                  .Returns<int>(i => _heldReleases.Where(v => v.MovieId == i).ToList());

            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.GetMovies(It.IsAny<IEnumerable<int>>()))
                  .Returns(new List<Movie> { _movie });

            Mocker.GetMock<IDelayProfileService>()
                  .Setup(s => s.AllForTags(It.IsAny<HashSet<int>>()))
                  .Returns(new List<DelayProfile> { new DelayProfile() });

            Mocker.GetMock<IDelayProfileService>()
                  .Setup(s => s.BestForTags(It.IsAny<HashSet<int>>()))
                  .Returns(new DelayProfile());

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetSeries(It.IsAny<IEnumerable<int>>()))
                  .Returns(new List<Series>());
        }

        private void GivenHeldRelease()
        {
            _heldReleases.Add(new PendingRelease
            {
                Id = 5,
                MovieId = _movie.Id,
                Title = _remoteMovie.Release.Title,
                Release = _remoteMovie.Release,
                ParsedMovieInfo = _remoteMovie.ParsedMovieInfo,
                Reason = PendingReleaseReason.Delay,
                Added = DateTime.UtcNow
            });
        }

        [Test]
        public void should_add_delayed_movie_release_with_movie_id()
        {
            Subject.Add(_temporarilyRejected, PendingReleaseReason.Delay);

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(v => v.Insert(It.Is<PendingRelease>(p => p.MovieId == _movie.Id &&
                                                                    p.ParsedMovieInfo != null &&
                                                                    p.ParsedEpisodeInfo == null)),
                          Times.Once());
        }

        [Test]
        public void should_not_add_duplicate_movie_release()
        {
            GivenHeldRelease();

            Subject.Add(_temporarilyRejected, PendingReleaseReason.Delay);

            Mocker.GetMock<IPendingReleaseRepository>()
                  .Verify(v => v.Insert(It.IsAny<PendingRelease>()), Times.Never());
        }

        [Test]
        public void should_return_movie_pending_queue_item_without_dereferencing_series()
        {
            GivenHeldRelease();

            Subject.Handle(new ApplicationStartedEvent());

            var queue = Subject.GetPendingQueue();

            queue.Should().HaveCount(1);
            queue[0].Movie.Should().NotBeNull();
            queue[0].Movie.Id.Should().Be(_movie.Id);
            queue[0].RemoteMovie.Should().NotBeNull();
            queue[0].Series.Should().BeNull();
        }
    }
}
