using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using NzbDrone.Core.Authentication;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Http.Authentication
{
    public class UiAuthorizationPolicyProvider : IAuthorizationPolicyProvider
    {
        public const string UiPolicyName = "UI";
        public const string InitializePolicyName = "Initialize";

        private readonly IConfigFileProvider _config;

        public DefaultAuthorizationPolicyProvider FallbackPolicyProvider { get; }

        public UiAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options,
            IConfigFileProvider config)
        {
            FallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
            _config = config;
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => FallbackPolicyProvider.GetDefaultPolicyAsync();

        public Task<AuthorizationPolicy> GetFallbackPolicyAsync() => FallbackPolicyProvider.GetFallbackPolicyAsync();

        public Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
        {
            if (policyName.Equals(UiPolicyName, StringComparison.OrdinalIgnoreCase))
            {
                var policy = new AuthorizationPolicyBuilder(GetUiAuthenticationScheme())
                    .AddRequirements(new BypassableDenyAnonymousAuthorizationRequirement());

                return Task.FromResult(policy.Build());
            }

            if (policyName.Equals(InitializePolicyName, StringComparison.OrdinalIgnoreCase))
            {
                // The UI requirement decides who may reach the SPA at all; the bootstrap
                // additionally only releases the API keys to a real login or a local client.
                var policy = new AuthorizationPolicyBuilder(GetUiAuthenticationScheme())
                    .AddRequirements(new BypassableDenyAnonymousAuthorizationRequirement())
                    .AddRequirements(new BootstrapKeyAccessRequirement());

                return Task.FromResult(policy.Build());
            }

            return FallbackPolicyProvider.GetPolicyAsync(policyName);
        }

        private string GetUiAuthenticationScheme()
        {
            var authenticationMethod = _config.EffectiveAuthenticationMethod();

            // OIDC signs into the cookie scheme, so authenticate against the cookie.
            // Using the OIDC scheme would challenge the identity provider on every
            // unauthenticated request instead of redirecting to the login page.
            return authenticationMethod switch
            {
                AuthenticationType.Oidc => CookieAuthenticationDefaults.AuthenticationScheme,
                _ => authenticationMethod.ToString()
            };
        }
    }
}
