using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore
{
    [TestFixture]
    public class RootFolderMediaTypeFixture : DbTest<RootFolderRepository, RootFolder>
    {
        [Test]
        public void should_round_trip_media_type()
        {
            Db.Insert(new RootFolder { Path = @"C:\Test\Movies", MediaType = MediaType.Movie });

            var stored = Db.Single<RootFolder>();

            stored.MediaType.Should().Be(MediaType.Movie);
        }
    }

    [TestFixture]
    public class TagMediaTypeFixture : DbTest<TagRepository, Tag>
    {
        [Test]
        public void should_round_trip_media_type()
        {
            Db.Insert(new Tag { Label = "movies", MediaType = MediaType.Movie });

            var stored = Db.Single<Tag>();

            stored.MediaType.Should().Be(MediaType.Movie);
        }
    }
}
