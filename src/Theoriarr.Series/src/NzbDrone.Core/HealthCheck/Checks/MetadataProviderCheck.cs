using NzbDrone.Common.Extensions;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MetadataSource;

namespace NzbDrone.Core.HealthCheck.Checks
{
    /// <summary>
    /// Warns when Theoriarr is pointed at the shared public Providarr instance
    /// (providarr.deprived.dev), reporting that server's cache TTLs and inbound
    /// rate limit (see <see cref="IMetadataProviderStatusService"/>).
    /// </summary>
    public class MetadataProviderCheck : HealthCheckBase
    {
        private readonly IMetadataProviderStatusService _statusService;

        public MetadataProviderCheck(IMetadataProviderStatusService statusService, ILocalizationService localizationService)
            : base(localizationService)
        {
            _statusService = statusService;
        }

        public override HealthCheck Check()
        {
            var status = _statusService.GetStatus();

            if (!status.IsSharedPublic || status.Warning.IsNullOrWhiteSpace())
            {
                // Self-hosted instance: nothing to warn about.
                return new HealthCheck(GetType());
            }

            return new HealthCheck(
                GetType(),
                HealthCheckResult.Warning,
                HealthCheckReason.MetadataCacheAggressive,
                status.Warning,
                "#metadata-provider");
        }
    }
}
