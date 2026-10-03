using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Http.Authentication;
using NzbDrone.Test.Common;
using Sonarr.Http.Authentication;

namespace NzbDrone.Api.Test.Authentication
{
    [TestFixture]
    public class BootstrapKeyAccessAuthorizationHandlerFixture : TestBase<BootstrapKeyAccessAuthorizationHandler>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IConfigFileProvider>()
                  .SetupGet(c => c.TrustCgnatIpAddresses)
                  .Returns(false);
        }

        private static HttpContext GetHttpContext(string remoteIp)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);

            return httpContext;
        }

        private static ClaimsPrincipal AuthenticatedPrincipal(string authenticationType)
        {
            var identity = new ClaimsIdentity(
                new List<Claim> { new("user", "someone") },
                authenticationType);

            return new ClaimsPrincipal(identity);
        }

        private bool IsAuthorized(HttpContext httpContext, ClaimsPrincipal user)
        {
            var requirement = new BootstrapKeyAccessRequirement();
            var context = new AuthorizationHandlerContext(new[] { requirement }, user, httpContext);

            Subject.HandleAsync(context).GetAwaiter().GetResult();

            return context.HasSucceeded;
        }

        [Test]
        public void should_allow_real_authentication_from_a_public_address()
        {
            IsAuthorized(GetHttpContext("203.0.113.66"), AuthenticatedPrincipal("Forms")).Should().BeTrue();
        }

        [Test]
        public void should_allow_local_client_when_authentication_is_disabled()
        {
            var noAuth = AuthenticatedPrincipal(NoAuthenticationHandler.AuthenticationTypeName);

            IsAuthorized(GetHttpContext("127.0.0.1"), noAuth).Should().BeTrue();
            IsAuthorized(GetHttpContext("192.168.1.50"), noAuth).Should().BeTrue();
        }

        [Test]
        public void should_allow_local_client_when_anonymous()
        {
            IsAuthorized(GetHttpContext("127.0.0.1"), new ClaimsPrincipal()).Should().BeTrue();
        }

        [Test]
        public void should_refuse_public_client_when_authentication_is_disabled()
        {
            var noAuth = AuthenticatedPrincipal(NoAuthenticationHandler.AuthenticationTypeName);

            IsAuthorized(GetHttpContext("203.0.113.66"), noAuth).Should().BeFalse();
        }

        [Test]
        public void should_refuse_anonymous_public_client()
        {
            IsAuthorized(GetHttpContext("203.0.113.66"), new ClaimsPrincipal()).Should().BeFalse();
        }

        [Test]
        public void should_allow_trusted_cgnat_address_when_enabled()
        {
            Mocker.GetMock<IConfigFileProvider>()
                  .SetupGet(c => c.TrustCgnatIpAddresses)
                  .Returns(true);

            IsAuthorized(GetHttpContext("100.64.0.1"), new ClaimsPrincipal()).Should().BeTrue();
        }

        [Test]
        public void should_refuse_cgnat_address_when_not_enabled()
        {
            IsAuthorized(GetHttpContext("100.64.0.1"), new ClaimsPrincipal()).Should().BeFalse();
        }
    }
}
