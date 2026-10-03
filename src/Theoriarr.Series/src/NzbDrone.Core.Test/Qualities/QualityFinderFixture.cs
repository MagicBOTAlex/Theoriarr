using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Test.Qualities
{
    [TestFixture]
    public class QualityFinderFixture
    {
        [TestCase(QualitySource.Television, 480)]
        [TestCase(QualitySource.Unknown, 480)]
        public void should_return_SDTV(QualitySource source, int resolution)
        {
            QualityFinder.FindBySourceAndResolution(source, resolution).Should().Be(Quality.SDTV);
        }

        [TestCase(QualitySource.Television, 720)]
        [TestCase(QualitySource.Unknown, 720)]
        public void should_return_HDTV_720p(QualitySource source, int resolution)
        {
            QualityFinder.FindBySourceAndResolution(source, resolution).Should().Be(Quality.HDTV720p);
        }

        [TestCase(QualitySource.Television, 1080)]
        [TestCase(QualitySource.Unknown, 1080)]
        public void should_return_HDTV_1080p(QualitySource source, int resolution)
        {
            QualityFinder.FindBySourceAndResolution(source, resolution).Should().Be(Quality.HDTV1080p);
        }

        [TestCase(QualitySource.Bluray, 720)]
        public void should_return_Bluray720p(QualitySource source, int resolution)
        {
            QualityFinder.FindBySourceAndResolution(source, resolution).Should().Be(Quality.Bluray720p);
        }

        [TestCase(QualitySource.DVD, 720)]
        [TestCase(QualitySource.DVD, 1080)]
        [TestCase(QualitySource.DVD, 2160)]
        public void should_stay_within_the_same_source_when_the_resolution_is_unavailable(QualitySource source, int resolution)
        {
            QualityFinder.FindBySourceAndResolution(source, resolution).Should().Be(Quality.DVD);
        }
    }
}
