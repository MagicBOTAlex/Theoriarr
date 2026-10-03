using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Authentication;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Authentication
{
    [TestFixture]
    public class OidcDiscoveryServiceFixture : CoreTest<OidcDiscoveryService>
    {
        private const string ValidDocument = "{\"authorization_endpoint\":\"https://idp.example.com/auth\",\"token_endpoint\":\"https://idp.example.com/token\"}";

        private void GivenDiscoveryDocument()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(c => c.Get<OidcDiscoveryDocument>(It.IsAny<HttpRequest>()))
                  .Returns<HttpRequest>(request =>
                      new HttpResponse<OidcDiscoveryDocument>(
                          new HttpResponse(request, new HttpHeader(), ValidDocument)));
        }

        [TestCase("http://127.0.0.1:8080")]
        [TestCase("https://10.1.2.3")]
        [TestCase("https://172.16.0.10")]
        [TestCase("https://192.168.1.50")]
        [TestCase("https://169.254.169.254")]
        [TestCase("https://100.100.1.1")]
        [TestCase("https://[::1]")]
        [TestCase("https://[fd00::1]")]
        [TestCase("https://localhost")]
        [TestCase("https://something.local")]
        public void should_refuse_non_public_authorities(string authority)
        {
            Subject.IsDiscoverable(authority).Should().BeFalse();

            Mocker.GetMock<IHttpClient>()
                  .Verify(c => c.Get<OidcDiscoveryDocument>(It.IsAny<HttpRequest>()), Times.Never);
        }

        [Test]
        public void should_allow_public_authority()
        {
            GivenDiscoveryDocument();

            Subject.IsDiscoverable("https://8.8.8.8").Should().BeTrue();

            Mocker.GetMock<IHttpClient>()
                  .Verify(c => c.Get<OidcDiscoveryDocument>(It.IsAny<HttpRequest>()), Times.Once);
        }

        [Test]
        public void should_reject_non_http_schemes()
        {
            Subject.IsDiscoverable("file:///etc/passwd").Should().BeFalse();

            Mocker.GetMock<IHttpClient>()
                  .Verify(c => c.Get<OidcDiscoveryDocument>(It.IsAny<HttpRequest>()), Times.Never);
        }
    }
}
