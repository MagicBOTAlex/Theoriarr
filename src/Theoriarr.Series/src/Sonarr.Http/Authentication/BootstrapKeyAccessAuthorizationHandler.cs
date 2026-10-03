using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using Sonarr.Http.Authentication;
using Sonarr.Http.Extensions;

namespace NzbDrone.Http.Authentication
{
    // Marker requirement for the "Initialize" policy. The combined bootstrap carries the
    // long-lived API keys, so on top of the normal UI policy it is only released to a real
    // login or to a trusted local client.
    public class BootstrapKeyAccessRequirement : IAuthorizationRequirement
    {
    }

    public class BootstrapKeyAccessAuthorizationHandler : AuthorizationHandler<BootstrapKeyAccessRequirement>
    {
        private readonly IConfigFileProvider _configService;

        public BootstrapKeyAccessAuthorizationHandler(IConfigFileProvider configService)
        {
            _configService = configService;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, BootstrapKeyAccessRequirement requirement)
        {
            // A real scheme (Forms/OIDC/External) signed the user in. When authentication is
            // disabled the "no auth" scheme also produces an authenticated principal, so
            // distinguish it by authentication type rather than by IsAuthenticated.
            if (context.User.Identities.Any(identity =>
                    identity.IsAuthenticated &&
                    identity.AuthenticationType != NoAuthenticationHandler.AuthenticationTypeName))
            {
                context.Succeed(requirement);

                return Task.CompletedTask;
            }

            // Authentication disabled: keep the bootstrap working for the operator on the
            // same host/LAN, but never hand the keys to a public client.
            if (context.Resource is HttpContext httpContext &&
                IPAddress.TryParse(httpContext.GetRemoteIP(), out var ipAddress) &&
                (ipAddress.IsLocalAddress() ||
                 (_configService.TrustCgnatIpAddresses && ipAddress.IsCgnatIpAddress())))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
