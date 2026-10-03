using NLog;
using NzbDrone.Core.Blocklisting;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class BlocklistSpecification : DownloadDecisionEngineSpecification
    {
        private readonly IBlocklistService _blocklistService;
        private readonly Logger _logger;

        public BlocklistSpecification(IBlocklistService blocklistService, Logger logger)
        {
            _blocklistService = blocklistService;
            _logger = logger;
        }

        public override SpecificationPriority Priority => SpecificationPriority.Database;
        public override RejectionType Type => RejectionType.Permanent;

        public override bool AppliesTo(IRemoteSubject subject)
        {
            return subject is RemoteEpisode or RemoteMovie;
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            if (_blocklistService.Blocklisted(subject.Series.Id, subject.Release))
            {
                _logger.Debug("{0} is blocklisted, rejecting.", subject.Release.Title);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.Blocklisted, "Release is blocklisted");
            }

            return DownloadSpecDecision.Accept();
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            if (_blocklistService.BlocklistedMovie(subject.Movie.Id, subject.Release))
            {
                _logger.Debug("{0} is blocklisted, rejecting.", subject.Release.Title);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.Blocklisted, "Release is blocklisted");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
