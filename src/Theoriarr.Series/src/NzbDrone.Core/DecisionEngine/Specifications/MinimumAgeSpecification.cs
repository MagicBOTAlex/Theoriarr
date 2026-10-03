using System;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class MinimumAgeSpecification : DownloadDecisionEngineSpecification
    {
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public MinimumAgeSpecification(IConfigService configService, Logger logger)
        {
            _configService = configService;
            _logger = logger;
        }

        public override SpecificationPriority Priority => SpecificationPriority.Default;
        public override RejectionType Type => RejectionType.Temporary;

        public override bool AppliesTo(IRemoteSubject subject)
        {
            return subject is RemoteEpisode || subject is RemoteMovie;
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            return CheckMinimumAge(subject.Release);
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            return CheckMinimumAge(subject.Release);
        }

        private DownloadSpecDecision CheckMinimumAge(ReleaseInfo release)
        {
            if (release.DownloadProtocol != Indexers.DownloadProtocol.Usenet)
            {
                _logger.Debug("Not checking minimum age requirement for non-usenet report");
                return DownloadSpecDecision.Accept();
            }

            var age = release.AgeMinutes;
            var minimumAge = _configService.MinimumAge;
            var ageRounded = Math.Round(age, 1);

            if (minimumAge == 0)
            {
                _logger.Debug("Minimum age is not set.");
                return DownloadSpecDecision.Accept();
            }

            _logger.Debug("Checking if report meets minimum age requirements. {0}", ageRounded);

            if (age < minimumAge)
            {
                _logger.Debug("Only {0} minutes old, minimum age is {1} minutes", ageRounded, minimumAge);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.MinimumAge, "Only {0} minutes old, minimum age is {1} minutes", ageRounded, minimumAge);
            }

            _logger.Debug("Release is {0} minutes old, greater than minimum age of {1} minutes", ageRounded, minimumAge);

            return DownloadSpecDecision.Accept();
        }
    }
}
