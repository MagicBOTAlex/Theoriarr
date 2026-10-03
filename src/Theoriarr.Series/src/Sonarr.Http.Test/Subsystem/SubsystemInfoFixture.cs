using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using Sonarr.Http.Subsystem;

namespace Sonarr.Http.Test.Subsystem
{
    [TestFixture]
    public class SubsystemInfoFixture
    {
        private readonly ISubsystemInfo _subject = new SubsystemInfo();

        [Test]
        public void should_report_sonarr_for_series()
        {
            _subject.GetAppName(AppSubsystem.Series).Should().Be("Sonarr");
        }

        [Test]
        public void should_report_radarr_for_movies()
        {
            _subject.GetAppName(AppSubsystem.Movies).Should().Be("Radarr");
        }

        [Test]
        public void should_use_movie_build_info_for_the_movie_domain()
        {
            _subject.GetVersion(AppSubsystem.Movies).Should().Be(MovieBuildInfo.Version);
            _subject.GetBuildTime(AppSubsystem.Movies).Should().NotBe(default(DateTime));
            _subject.GetRuntimeName(AppSubsystem.Movies).Should().NotBeNullOrWhiteSpace();
            _subject.GetRuntimeVersion(AppSubsystem.Movies).Should().NotBeNull();
        }

        [Test]
        public void should_report_the_unified_build_for_series()
        {
            _subject.GetVersion(AppSubsystem.Series).Should().Be(BuildInfo.Version);
            _subject.GetBuildTime(AppSubsystem.Series).Should().NotBe(default(DateTime));
        }
    }
}
