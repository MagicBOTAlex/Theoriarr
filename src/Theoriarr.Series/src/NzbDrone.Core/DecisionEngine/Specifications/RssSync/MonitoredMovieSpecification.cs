using NLog;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications.RssSync
{
    public class MonitoredMovieSpecification : DownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public MonitoredMovieSpecification(Logger logger)
        {
            _logger = logger;
        }

        public override SpecificationPriority Priority => SpecificationPriority.Default;
        public override RejectionType Type => RejectionType.Permanent;

        public override bool AppliesTo(IRemoteSubject subject)
        {
            return subject is RemoteMovie;
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            if (information.SearchCriteria is { UserInvokedSearch: true })
            {
                _logger.Debug("Skipping monitored check during search");
                return DownloadSpecDecision.Accept();
            }

            if (!subject.Movie.Monitored)
            {
                _logger.Debug("{0} is present in the DB but not tracked. Rejecting", subject.Movie);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.MovieNotMonitored, "Movie is not monitored");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
