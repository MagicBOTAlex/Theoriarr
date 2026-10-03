namespace NzbDrone.Common.Options;

public class AuthOptions
{
    public string ApiKey { get; set; }
    public string MovieApiKey { get; set; }
    public bool? Enabled { get; set; }

    // Force authentication off (no login page) regardless of the stored config. Bound from
    // Theoriarr:Auth:Disabled (env THEORIARR__AUTH__DISABLED); the flat THEORIARR_DISABLE_AUTH
    // alias is handled in ConfigFileProvider.
    public bool? Disabled { get; set; }
    public string Method { get; set; }
    public string Required { get; set; }
    public bool? TrustCgnatIpAddresses { get; set; }
    public string OidcAuthority { get; set; }
    public string OidcClientId { get; set; }
    public string OidcClientSecret { get; set; }
    public string OidcUserIdentifier { get; set; }
    public string OidcScopes { get; set; }
}
