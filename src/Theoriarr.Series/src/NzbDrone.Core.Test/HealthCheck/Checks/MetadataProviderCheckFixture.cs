using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.HealthCheck.Checks;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.HealthCheck.Checks
{
    [TestFixture]
    public class MetadataProviderCheckFixture : CoreTest<MetadataProviderCheck>
    {
        private void GivenStatus(bool isSharedPublic, string warning)
        {
            Mocker.GetMock<IMetadataProviderStatusService>()
                .Setup(s => s.GetStatus())
                .Returns(new MetadataProviderStatus
                {
                    IsSharedPublic = isSharedPublic,
                    Warning = warning
                });
        }

        [Test]
        public void should_warn_for_the_shared_public_providarr()
        {
            GivenStatus(true, "You are using the shared public Providarr instance. Host your own instance.");

            var result = Subject.Check();

            result.Type.Should().Be(HealthCheckResult.Warning);
            result.Reason.Should().Be(HealthCheckReason.MetadataCacheAggressive);
            result.Message.Should().Contain("Host your own");
        }

        [Test]
        public void should_be_ok_for_a_self_hosted_providarr()
        {
            GivenStatus(false, null);

            Subject.Check().ShouldBeOk();
        }

        [Test]
        public void should_be_ok_when_there_is_no_warning()
        {
            GivenStatus(true, null);

            Subject.Check().ShouldBeOk();
        }
    }
}
