using NLog;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications.Search
{
    public class MovieSpecification : DownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public MovieSpecification(Logger logger)
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
            var searchCriteria = information.SearchCriteria;

            if (searchCriteria == null)
            {
                return DownloadSpecDecision.Accept();
            }

            _logger.Debug("Checking if movie matches searched movie");

            if (subject.Movie.Id != searchCriteria.Movie.Id)
            {
                _logger.Debug("Movie {0} does not match {1}", subject.Movie, searchCriteria.Movie);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongMovie, "Wrong movie");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
