using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class CustomFormatAllowedbyProfileSpecification : DownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public CustomFormatAllowedbyProfileSpecification(Logger logger)
        {
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
            var minScore = subject.Series.QualityProfile.Value.MinFormatScore;
            var score = subject.CustomFormatScore;

            if (score < minScore)
            {
                return DownloadSpecDecision.Reject(DownloadRejectionReason.CustomFormatMinimumScore, "Custom Formats {0} have score {1} below Series profile minimum {2}", subject.CustomFormats.ConcatToString(), score, minScore);
            }

            _logger.Trace("Custom Format Score of {0} [{1}] above Series profile minimum {2}", score, subject.CustomFormats.ConcatToString(), minScore);

            return DownloadSpecDecision.Accept();
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            var minScore = subject.Movie.QualityProfile.MinFormatScore;
            var score = subject.CustomFormatScore;

            if (score < minScore)
            {
                return DownloadSpecDecision.Reject(DownloadRejectionReason.CustomFormatMinimumScore, "Custom Formats {0} have score {1} below Movie's profile minimum {2}", subject.CustomFormats.ConcatToString(), score, minScore);
            }

            _logger.Trace("Custom Format Score of {0} [{1}] above Movie's profile minimum {2}", score, subject.CustomFormats.ConcatToString(), minScore);

            return DownloadSpecDecision.Accept();
        }
    }
}
