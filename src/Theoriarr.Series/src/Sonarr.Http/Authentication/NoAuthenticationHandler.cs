using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NzbDrone.Core.Authentication;

namespace Sonarr.Http.Authentication
{
    public class NoAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        // Identity authentication type used when authentication is disabled. Handlers that
        // need to distinguish a real login from the anonymous "no auth" principal match on
        // this value rather than on ClaimsPrincipal.Identity.IsAuthenticated, which is true
        // for both.
        public const string AuthenticationTypeName = "NoAuth";

        public NoAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new Claim("user", "Anonymous"),
                new Claim("AuthType", AuthenticationType.None.ToString())
            };

            var identity = new ClaimsIdentity(claims, AuthenticationTypeName, "user", "identifier");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, AuthenticationTypeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
