using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.AutoTagging;
using NzbDrone.Core.Download;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Releases;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Tags
{
    [TestFixture]
    public class TagServiceFixture : TestBase<TagService>
    {
        private const int TagId = 5;

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ITagRepository>()
                  .Setup(s => s.Get(TagId))
                  .Returns(new Tag { Id = TagId, Label = "shared" });

            Mocker.GetMock<ITagRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<Tag> { new Tag { Id = TagId, Label = "shared" } });

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllForTag(TagId))
                  .Returns(new List<Series> { new Series { Id = 1 } });

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeriesTags())
                  .Returns(new Dictionary<int, List<int>>
                  {
                      { 1, new List<int> { TagId } }
                  });

            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.AllMovieTags())
                  .Returns(new Dictionary<int, List<int>>
                  {
                      { 99, new List<int> { TagId } }
                  });

            Mocker.GetMock<IDelayProfileService>().Setup(s => s.AllForTag(TagId)).Returns(new List<DelayProfile>());
            Mocker.GetMock<IDelayProfileService>().Setup(s => s.All()).Returns(new List<DelayProfile>());
            Mocker.GetMock<IImportListFactory>().Setup(s => s.AllForTag(TagId)).Returns(new List<ImportListDefinition>());
            Mocker.GetMock<IImportListFactory>().Setup(s => s.All()).Returns(new List<ImportListDefinition>());
            Mocker.GetMock<INotificationFactory>().Setup(s => s.AllForTag(TagId)).Returns(new List<NotificationDefinition>());
            Mocker.GetMock<INotificationFactory>().Setup(s => s.All()).Returns(new List<NotificationDefinition>());
            Mocker.GetMock<IReleaseProfileService>().Setup(s => s.AllForTag(TagId)).Returns(new List<ReleaseProfile>());
            Mocker.GetMock<IReleaseProfileService>().Setup(s => s.AllExcludedForTag(TagId)).Returns(new List<ReleaseProfile>());
            Mocker.GetMock<IReleaseProfileService>().Setup(s => s.All()).Returns(new List<ReleaseProfile>());
            Mocker.GetMock<IIndexerFactory>().Setup(s => s.AllForTag(TagId)).Returns(new List<IndexerDefinition>());
            Mocker.GetMock<IIndexerFactory>().Setup(s => s.All()).Returns(new List<IndexerDefinition>());
            Mocker.GetMock<IAutoTaggingService>().Setup(s => s.AllForTag(TagId)).Returns(new List<AutoTag>());
            Mocker.GetMock<IAutoTaggingService>().Setup(s => s.All()).Returns(new List<AutoTag>());
            Mocker.GetMock<IDownloadClientFactory>().Setup(s => s.AllForTag(TagId)).Returns(new List<DownloadClientDefinition>());
            Mocker.GetMock<IDownloadClientFactory>().Setup(s => s.All()).Returns(new List<DownloadClientDefinition>());
        }

        [Test]
        public void details_should_carry_movie_usage()
        {
            var details = Subject.Details(TagId);

            details.MovieIds.Should().Equal(99);
            details.SeriesIds.Should().Equal(1);
            details.InUse.Should().BeTrue();
        }

        [Test]
        public void details_should_mark_movie_only_tag_as_in_use()
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllForTag(TagId))
                  .Returns(new List<Series>());

            var details = Subject.Details(TagId);

            details.SeriesIds.Should().BeEmpty();
            details.MovieIds.Should().Equal(99);
            details.InUse.Should().BeTrue();
        }

        [Test]
        public void details_list_should_carry_movie_usage()
        {
            var details = Subject.Details().Single(d => d.Id == TagId);

            details.MovieIds.Should().Equal(99);
            details.SeriesIds.Should().Equal(1);
        }
    }
}
