using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Update;

namespace NzbDrone.Core.Test.UpdateTests
{
    public class UpdatePackageProviderFixture : CoreTest<UpdatePackageProvider>
    {
        [SetUp]
        public void Setup()
        {
            if (OsInfo.Os == Os.LinuxMusl || OsInfo.Os == Os.Bsd)
            {
                throw new IgnoreException("Ignore until we have musl releases");
            }
        }

        [Test]
        public void no_update_when_version_higher()
        {
            Subject.GetLatestUpdate("main", new Version(10, 0)).Should().BeNull();
        }

        [Test]
        public void no_update_when_version_lower()
        {
            Subject.GetLatestUpdate("main", new Version(3, 0)).Should().BeNull();
        }

        [Test]
        public void no_update_for_invalid_branch()
        {
            Subject.GetLatestUpdate("invalid_branch", new Version(3, 0)).Should().BeNull();
        }

        [Test]
        public void should_get_no_recent_updates()
        {
            Subject.GetRecentUpdates("main", new Version(4, 0), null).Should().BeEmpty();
        }
    }
}
