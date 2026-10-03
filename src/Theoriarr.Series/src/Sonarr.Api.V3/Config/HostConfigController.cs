using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Network;
using NzbDrone.Core.Authentication;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Update;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;
using Sonarr.Http.Subsystem;
using Sonarr.Http.Validation;

namespace Sonarr.Api.V3.Config
{
    [V3ApiController("config/host")]
    [AppSubsystem(AppSubsystem.Series)]
    [AppSubsystem(AppSubsystem.Movies)]
    public class HostConfigController : RestController<HostConfigResource>
    {
        private const string PrivateValue = "********";

        // Host authentication and the series API key are owned by the host (series) domain.
        // A change to any of these requires a genuine administrator session: the series key
        // or a cookie/OIDC/Forms login -- never the movie key.
        private static readonly HashSet<string> HostOwnedSensitiveFields = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(HostConfigResource.AuthenticationMethod),
            nameof(HostConfigResource.AuthenticationRequired),
            nameof(HostConfigResource.Username),
            nameof(HostConfigResource.Password),
            nameof(HostConfigResource.PasswordConfirmation),
            nameof(HostConfigResource.OidcAuthority),
            nameof(HostConfigResource.OidcClientId),
            nameof(HostConfigResource.OidcClientSecret),
            nameof(HostConfigResource.OidcUserIdentifier),
            nameof(HostConfigResource.OidcScopes),
            nameof(HostConfigResource.SslCertPassword),
            nameof(HostConfigResource.ProxyPassword),
            nameof(HostConfigResource.ApiKey)
        };

        private readonly IConfigFileProvider _configFileProvider;
        private readonly IConfigService _configService;
        private readonly IUserService _userService;
        private readonly ISubsystemAccessor _subsystemAccessor;

        public HostConfigController(IConfigFileProvider configFileProvider,
                                    IConfigService configService,
                                    IUserService userService,
                                    IDiskProvider diskProvider,
                                    OidcAuthorityValidator oidcAuthorityValidator,
                                    ISubsystemAccessor subsystemAccessor)
        {
            _configFileProvider = configFileProvider;
            _configService = configService;
            _userService = userService;
            _subsystemAccessor = subsystemAccessor;

            SharedValidator.RuleFor(c => c.BindAddress)
                           .ValidIpAddress()
                           .When(c => c.BindAddress != "*" && c.BindAddress != "localhost");

            SharedValidator.RuleFor(c => c.Port).ValidPort();

            SharedValidator.RuleFor(c => c.AllowedHosts).NotNull();

            SharedValidator.RuleFor(c => c.AllowedHosts)
                           .Must(h => AllowedHostsParser.Parse(h).Any())
                           .When(c => c.AuthenticationRequired != AuthenticationRequiredType.Enabled)
                           .WithMessage("Allowed Hosts is required when 'Authentication Required' is not 'Enabled'");

            SharedValidator.RuleFor(c => c.AllowedHosts)
                           .ValidHosts()
                           .When(c => c.AllowedHosts.IsNotNullOrWhiteSpace());

            SharedValidator.RuleFor(c => c.UrlBase).ValidUrlBase();
            SharedValidator.RuleFor(c => c.TrustedNetworks).ValidIpNetworks();
            SharedValidator.RuleFor(c => c.InstanceName).StartsOrEndsWithSonarr();

            SharedValidator.RuleFor(c => c.Username).NotEmpty().When(c => c.AuthenticationMethod == AuthenticationType.Forms);
            SharedValidator.RuleFor(c => c.Password).NotEmpty().When(c => c.AuthenticationMethod == AuthenticationType.Forms);

            SharedValidator.RuleFor(c => c.AuthenticationMethod)
#pragma warning disable CS0618 // Type or member is obsolete
                .NotEqual(AuthenticationType.Basic)
#pragma warning restore CS0618 // Type or member is obsolete
                .WithMessage("'Basic' is no longer supported, switch to 'Forms' instead.");

            SharedValidator.RuleFor(c => c.PasswordConfirmation)
                .Must((resource, p) => IsMatchingPassword(resource)).WithMessage("Must match Password");

            SharedValidator.RuleFor(c => c.OidcAuthority)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(c => c is not null && c.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                .WithMessage("OIDC Authority must start with 'https://'")
                .When(c => c.AuthenticationMethod == AuthenticationType.Oidc)
                .WithName("OIDC Authority");

            SharedValidator.RuleFor(c => c.OidcAuthority)
                .SetValidator(oidcAuthorityValidator)
                .When(c => c.AuthenticationMethod == AuthenticationType.Oidc &&
                           c.OidcAuthority != _configFileProvider.OidcAuthority)
                .WithName("OIDC Authority");

            SharedValidator.RuleFor(c => c.OidcClientId).NotEmpty()
                .When(c => c.AuthenticationMethod == AuthenticationType.Oidc)
                .WithName("OIDC Client ID");

            SharedValidator.RuleFor(c => c.OidcClientSecret).NotEmpty()
                .When(c => c.AuthenticationMethod == AuthenticationType.Oidc)
                .WithName("OIDC Client Secret");

            SharedValidator.RuleFor(c => c.OidcUserIdentifier).NotEmpty()
                .When(c => c.AuthenticationMethod == AuthenticationType.Oidc)
                .WithName("OIDC User");

            SharedValidator.RuleFor(c => c.OidcScopes)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(c => AuthenticationConfigurationExtensions.GetOidcScopes(c).Contains("openid"))
                .WithMessage("OIDC Scopes must include 'openid'")
                .When(c => c.AuthenticationMethod == AuthenticationType.Oidc)
                .WithName("OIDC Scopes");

            SharedValidator.RuleFor(c => c.SslPort).ValidPort().When(c => c.EnableSsl);
            SharedValidator.RuleFor(c => c.SslPort).NotEqual(c => c.Port).When(c => c.EnableSsl);

            SharedValidator.RuleFor(c => c.SslCertPath)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .IsValidPath()
                .SetValidator(new FileExistsValidator(diskProvider))
                .IsValidCertificate()
                .When(c => c.EnableSsl);

            SharedValidator.RuleFor(c => c.SslKeyPath)
                .NotEmpty()
                .IsValidPath()
                .SetValidator(new FileExistsValidator(diskProvider))
                .When(c => c.SslKeyPath.IsNotNullOrWhiteSpace());

            SharedValidator.RuleFor(c => c.LogSizeLimit).InclusiveBetween(1, 10);

            SharedValidator.RuleFor(c => c.Branch).NotEmpty().WithMessage("Branch name is required, 'main' is the default");
            SharedValidator.RuleFor(c => c.UpdateScriptPath).IsValidPath().When(c => c.UpdateMechanism == UpdateMechanism.Script);

            SharedValidator.RuleFor(c => c.BackupFolder).IsValidPath().When(c => Path.IsPathRooted(c.BackupFolder));
            SharedValidator.RuleFor(c => c.BackupInterval).InclusiveBetween(1, 7);
            SharedValidator.RuleFor(c => c.BackupRetention).InclusiveBetween(1, 90);
        }

        private bool IsMatchingPassword(HostConfigResource resource)
        {
            // A masked password means the caller never saw (and cannot change) it; treat the
            // confirmation as satisfied so a non-sensitive update is not rejected.
            if (resource.Password == PrivateValue)
            {
                return true;
            }

            var user = _userService.FindUser();

            if (user != null && user.Password == resource.Password)
            {
                return true;
            }

            if (resource.Password == resource.PasswordConfirmation)
            {
                return true;
            }

            return false;
        }

        protected override HostConfigResource GetResourceById(int id)
        {
            return GetHostConfig();
        }

        [HttpGet]
        public HostConfigResource GetHostConfig()
        {
            var oidcClientSecret = _configFileProvider.OidcClientSecret;
            var resource = _configFileProvider.ToResource(_configService);
            var user = _userService.FindUser();

            resource.Id = 1;
            resource.Username = user?.Username ?? string.Empty;
            resource.Password = user?.Password ?? string.Empty;
            resource.PasswordConfirmation = string.Empty;

            // Prevent the OIDC client secret from being exposed
            resource.OidcClientSecret = oidcClientSecret.IsNullOrWhiteSpace() ? string.Empty : PrivateValue;

            ScopeSecretsToSubsystem(resource);

            return resource;
        }

        // A request must never receive the other subsystem's secrets. For the movie key that
        // means the series API key plus every host-owned credential (the admin password hash,
        // the SSL certificate password, the proxy password and the OIDC client secret); the
        // movie key only ever sees its own key. The series key sees the host secrets but never
        // the movie key.
        private void ScopeSecretsToSubsystem(HostConfigResource resource)
        {
            if (_subsystemAccessor.Subsystem == AppSubsystem.Movies)
            {
                resource.ApiKey = PrivateValue;
                resource.Password = PrivateValue;
                resource.SslCertPassword = PrivateValue;
                resource.ProxyPassword = PrivateValue;

                if (resource.OidcClientSecret.IsNotNullOrWhiteSpace())
                {
                    resource.OidcClientSecret = PrivateValue;
                }
            }
            else
            {
                resource.MovieApiKey = PrivateValue;
            }
        }

        [RestPutById]
        public ActionResult<HostConfigResource> SaveHostConfig([FromBody] HostConfigResource resource)
        {
            resource.TrustedNetworks = IPNetworkParser.NormalizeList(resource.TrustedNetworks);

            // A masked value means "leave the stored secret unchanged" (the caller could not
            // read it, either because it was redacted on GET or because it is not theirs).
            NormalizePlaceholders(resource);

            EnsureAdminSessionForSensitiveChanges(resource);

            var dictionary = resource.GetType()
                                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                     .ToDictionary(prop => prop.Name, prop => prop.GetValue(resource, null));

            _configFileProvider.SaveConfigDictionary(dictionary);
            _configService.SaveConfigDictionary(dictionary);

            if (resource.Username.IsNotNullOrWhiteSpace() && resource.Password.IsNotNullOrWhiteSpace())
            {
                _userService.Upsert(resource.Username, resource.Password);
            }

            return Accepted(resource.Id);
        }

        private void NormalizePlaceholders(HostConfigResource resource)
        {
            var user = _userService.FindUser();

            if (resource.ApiKey == PrivateValue)
            {
                resource.ApiKey = _configFileProvider.ApiKey;
            }

            if (resource.MovieApiKey == PrivateValue)
            {
                resource.MovieApiKey = _configFileProvider.MovieApiKey;
            }

            if (resource.Password == PrivateValue)
            {
                resource.Password = user?.Password ?? string.Empty;
            }

            if (resource.SslCertPassword == PrivateValue)
            {
                resource.SslCertPassword = _configFileProvider.SslCertPassword;
            }

            if (resource.ProxyPassword == PrivateValue)
            {
                resource.ProxyPassword = _configService.ProxyPassword;
            }

            if (resource.OidcClientSecret == PrivateValue)
            {
                resource.OidcClientSecret = _configFileProvider.OidcClientSecret;
            }
        }

        // Changing host authentication or an API key requires a genuine administrator session,
        // not merely a cross-domain API key: the movie key must not be able to flip auth or
        // rotate/read the series key and upsert the admin user. The series key and any
        // cookie/OIDC/Forms login qualify; the movie key may only rotate its own key.
        private void EnsureAdminSessionForSensitiveChanges(HostConfigResource resource)
        {
            var isApiKeyRequest = HttpContext?.User?.HasClaim(claim => claim.Type == "ApiKey") == true;

            if (!isApiKeyRequest || _subsystemAccessor.Subsystem == AppSubsystem.Series)
            {
                return;
            }

            if (HasHostOwnedSensitiveChanges(resource))
            {
                throw new ForbiddenException("Changing host authentication or API key settings requires an administrator session.");
            }
        }

        private bool HasHostOwnedSensitiveChanges(HostConfigResource resource)
        {
            var user = _userService.FindUser();

            var stored = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                [nameof(HostConfigResource.AuthenticationMethod)] = _configFileProvider.AuthenticationMethod,
                [nameof(HostConfigResource.AuthenticationRequired)] = _configFileProvider.AuthenticationRequired,
                [nameof(HostConfigResource.Username)] = user?.Username ?? string.Empty,
                [nameof(HostConfigResource.Password)] = user?.Password ?? string.Empty,
                [nameof(HostConfigResource.PasswordConfirmation)] = string.Empty,
                [nameof(HostConfigResource.OidcAuthority)] = _configFileProvider.OidcAuthority ?? string.Empty,
                [nameof(HostConfigResource.OidcClientId)] = _configFileProvider.OidcClientId ?? string.Empty,
                [nameof(HostConfigResource.OidcClientSecret)] = _configFileProvider.OidcClientSecret ?? string.Empty,
                [nameof(HostConfigResource.OidcUserIdentifier)] = _configFileProvider.OidcUserIdentifier ?? string.Empty,
                [nameof(HostConfigResource.OidcScopes)] = _configFileProvider.OidcScopes ?? string.Empty,
                [nameof(HostConfigResource.SslCertPassword)] = _configFileProvider.SslCertPassword ?? string.Empty,
                [nameof(HostConfigResource.ProxyPassword)] = _configService.ProxyPassword ?? string.Empty,
                [nameof(HostConfigResource.ApiKey)] = _configFileProvider.ApiKey ?? string.Empty
            };

            var properties = typeof(HostConfigResource).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var field in HostOwnedSensitiveFields)
            {
                if (properties.TryGetValue(field, out var property) &&
                    !SensitiveValuesEqual(property.GetValue(resource), stored[field]))
                {
                    return true;
                }
            }

            return false;
        }

        // A missing string is equivalent to an empty one; anything else must match exactly.
        private static bool SensitiveValuesEqual(object left, object right)
        {
            left ??= string.Empty;
            right ??= string.Empty;

            if (left is string leftString && right is string rightString)
            {
                return string.Equals(leftString, rightString, StringComparison.Ordinal);
            }

            return left.Equals(right);
        }
    }
}
