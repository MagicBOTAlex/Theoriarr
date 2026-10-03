using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class RetentionSpecification : DownloadDecisionEngineSpecification
    {
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public RetentionSpecification(IConfigService configService, Logger logger)
        {
            _configService = configService;
            _logger = logger;
        }

        public override SpecificationPriority Priority => SpecificationPriority.Default;
        public override RejectionType Type => RejectionType.Permanent;

        public override bool AppliesTo(IRemoteSubject subject)
        {
            return subject is RemoteEpisode || subject is RemoteMovie;
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            return CheckRetention(subject.Release);
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            return CheckRetention(subject.Release);
        }

        private DownloadSpecDecision CheckRetention(ReleaseInfo release)
        {
            if (release.DownloadProtocol != Indexers.DownloadProtocol.Usenet)
            {
                _logger.Debug("Not checking retention requirement for non-usenet report");
                return DownloadSpecDecision.Accept();
            }

            var age = release.Age;
            var retention = _configService.Retention;

            _logger.Debug("Checking if report meets retention requirements. {0}", age);
            if (retention > 0 && age > retention)
            {
                _logger.Debug("Report age: {0} rejected by user's retention limit", age);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.MaximumAge, "Older than configured retention");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
