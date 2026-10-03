using NLog;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class SameEpisodesGrabSpecification : DownloadDecisionEngineSpecification
    {
        private readonly SameEpisodesSpecification _sameEpisodesSpecification;
        private readonly Logger _logger;

        public SameEpisodesGrabSpecification(SameEpisodesSpecification sameEpisodesSpecification, Logger logger)
        {
            _sameEpisodesSpecification = sameEpisodesSpecification;
            _logger = logger;
        }

        public override SpecificationPriority Priority => SpecificationPriority.Default;
        public override RejectionType Type => RejectionType.Permanent;

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            if (_sameEpisodesSpecification.IsSatisfiedBy(subject.Episodes))
            {
                return DownloadSpecDecision.Accept();
            }

            _logger.Debug("Episode file on disk contains more episodes than this release contains");
            return DownloadSpecDecision.Reject(DownloadRejectionReason.ExistingFileHasMoreEpisodes, "Episode file on disk contains more episodes than this release contains");
        }
    }
}
