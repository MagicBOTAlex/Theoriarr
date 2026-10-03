using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Configuration
{
    [TestFixture]
    public class ConfigRepositoryFixture : DbTest<ConfigRepository, Config>
    {
        [Test]
        public void upsert_should_insert_new_key()
        {
            Subject.Upsert("somekey", "value1");

            Subject.Get("somekey").Value.Should().Be("value1");
            Subject.All().Count(c => c.Key == "somekey").Should().Be(1);
        }

        [Test]
        public void upsert_should_update_existing_key_in_place()
        {
            Subject.Upsert("somekey", "value1");
            Subject.Upsert("somekey", "value2");

            Subject.Get("somekey").Value.Should().Be("value2");
            Subject.All().Count(c => c.Key == "somekey").Should().Be(1);
        }

        [Test]
        public void upsert_should_lowercase_key()
        {
            Subject.Upsert("SomeKey", "value1");

            Subject.Get("somekey").Value.Should().Be("value1");
        }
    }
}
