using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Common.Options;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource
{
    [TestFixture]
    public class MetadataProviderStatusServiceFixture : CoreTest<MetadataProviderStatusService>
    {
        private void GivenMetadata(MetadataOptions options)
        {
            Mocker.SetConstant<IOptions<MetadataOptions>>(Options.Create(options));
        }

        private void GivenPolicy(string json)
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(s => s.Get(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(r => new HttpResponse(r, new HttpHeader(), Encoding.ASCII.GetBytes(json)));
        }

        [Test]
        public void should_warn_for_the_shared_public_providarr()
        {
            GivenMetadata(new MetadataOptions());
            GivenPolicy("{\"cache\":{\"default_ttl_seconds\":259200,\"endpoint_ttl_seconds\":{\"movie\":604800}},\"rate_limits\":{\"inbound\":{\"requests_per_second\":2.0,\"burst\":5}}}");

            var status = Subject.GetStatus();

            status.IsSharedPublic.Should().BeTrue();
            status.BaseUrl.Should().Be("https://providarr.deprived.dev");
            status.Warning.Should().Contain("3 days");
            status.Warning.Should().Contain("7 days");
            status.Warning.Should().Contain("2/s");
            status.Warning.Should().Contain("Host your own");
        }

        [Test]
        public void should_not_warn_for_a_self_hosted_providarr()
        {
            GivenMetadata(new MetadataOptions { ProvidarrBaseUrl = "https://providarr.internal" });

            var status = Subject.GetStatus();

            status.IsSharedPublic.Should().BeFalse();
            status.BaseUrl.Should().Be("https://providarr.internal");
            status.Warning.Should().BeNull();
        }

        [Test]
        public void should_flag_shared_public_without_a_warning_when_the_policy_is_unavailable()
        {
            GivenMetadata(new MetadataOptions());
            GivenPolicy("not-json");

            var status = Subject.GetStatus();

            status.IsSharedPublic.Should().BeTrue();
            status.Warning.Should().BeNull();
        }
    }
}
