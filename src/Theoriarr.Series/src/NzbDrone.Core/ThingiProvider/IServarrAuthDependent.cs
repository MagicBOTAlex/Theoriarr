namespace NzbDrone.Core.ThingiProvider
{
    /// <summary>
    /// Marks a provider whose OAuth/authorization is brokered by Servarr-operated
    /// servers (<c>auth.servarr.com</c> or <c>services.sonarr.tv</c>).
    ///
    /// Theoriarr must not depend on upstream Sonarr/Radarr servers, so these providers
    /// are filtered out of their factories while <see cref="ServarrAuthDependencies.Enabled"/>
    /// is <c>false</c>. The code is intentionally retained so the feature can be
    /// re-enabled once a self-hosted auth broker is configured.
    /// </summary>
    public interface IServarrAuthDependent
    {
    }

    public static class ServarrAuthDependencies
    {
        /// <summary>
        /// When <c>false</c> (the default), providers implementing
        /// <see cref="IServarrAuthDependent"/> are not registered as available.
        /// Set to <c>true</c> after self-hosting the OAuth broker and repointing the
        /// broker URLs.
        /// </summary>
        public const bool Enabled = false;
    }
}
