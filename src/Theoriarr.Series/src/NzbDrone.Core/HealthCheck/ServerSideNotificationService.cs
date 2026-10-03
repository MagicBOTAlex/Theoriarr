using NzbDrone.Core.Localization;

namespace NzbDrone.Core.HealthCheck
{
    public class ServerSideNotificationService : HealthCheckBase
    {
        public ServerSideNotificationService(ILocalizationService localizationService)
            : base(localizationService)
        {
        }

        public override HealthCheck Check()
        {
            return new HealthCheck(GetType());
        }
    }

    public class ServerNotificationResponse
    {
        public HealthCheckResult Type { get; set; }
        public string Message { get; set; }
        public string WikiUrl { get; set; }
    }
}
