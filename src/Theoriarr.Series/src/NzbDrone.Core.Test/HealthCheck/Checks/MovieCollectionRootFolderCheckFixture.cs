using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.HealthCheck.Checks;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Movies.Collections;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.HealthCheck.Checks
{
    [TestFixture]
    public class MovieCollectionRootFolderCheckFixture : CoreTest<MovieCollectionRootFolderCheck>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ILocalizationService>()
                  .Setup(s => s.GetLocalizedString(It.IsAny<string>()))
                  .Returns("Some Warning Message");

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.All())
                  .Returns(new List<RootFolder>());
        }

        [Test]
        public void should_return_ok_when_no_collections()
        {
            Mocker.GetMock<IMovieCollectionService>()
                  .Setup(s => s.GetAllCollections())
                  .Returns(new List<MovieCollection>());

            Subject.Check().ShouldBeOk();
        }

        [Test]
        public void should_return_error_when_collection_root_folder_is_blank()
        {
            var collections = new List<MovieCollection>
            {
                new MovieCollection { RootFolderPath = string.Empty }
            };

            Mocker.GetMock<IMovieCollectionService>()
                  .Setup(s => s.GetAllCollections())
                  .Returns(collections);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(It.IsAny<string>()))
                  .Returns(false);

            Subject.Check().ShouldBeError();
        }

        [Test]
        public void should_return_ok_when_collection_root_folder_exists()
        {
            var rootFolderPath = "/movies".AsOsAgnostic();

            var collections = new List<MovieCollection>
            {
                new MovieCollection { RootFolderPath = rootFolderPath }
            };

            var rootFolders = new List<RootFolder>
            {
                new RootFolder { Path = rootFolderPath }
            };

            Mocker.GetMock<IMovieCollectionService>()
                  .Setup(s => s.GetAllCollections())
                  .Returns(collections);

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.All())
                  .Returns(rootFolders);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FolderExists(It.IsAny<string>()))
                  .Returns(true);

            Subject.Check().ShouldBeOk();
        }
    }
}
