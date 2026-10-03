using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications.RssSync
{
    public class IndexerTagSpecification : DownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;
        private readonly IIndexerFactory _indexerFactory;

        public IndexerTagSpecification(Logger logger, IIndexerFactory indexerFactory)
        {
            _logger = logger;
            _indexerFactory = indexerFactory;
        }

        public override SpecificationPriority Priority => SpecificationPriority.Default;
        public override RejectionType Type => RejectionType.Permanent;

        public override bool AppliesTo(IRemoteSubject subject)
        {
            return subject is RemoteEpisode or RemoteMovie;
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            if (subject.Release == null || subject.Series?.Tags == null || subject.Release.IndexerId == 0)
            {
                return DownloadSpecDecision.Accept();
            }

            IndexerDefinition indexer;
            try
            {
                indexer = _indexerFactory.Get(subject.Release.IndexerId);
            }
            catch (ModelNotFoundException)
            {
                _logger.Debug("Indexer with id {0} does not exist, skipping indexer tags check", subject.Release.IndexerId);
                return DownloadSpecDecision.Accept();
            }

            // If indexer has tags, check that at least one of them is present on the series
            var indexerTags = indexer.Tags;

            if (indexerTags.Any() && indexerTags.Intersect(subject.Series.Tags).Empty())
            {
                _logger.Debug("Indexer {0} has tags. None of these are present on series {1}. Rejecting", subject.Release.Indexer, subject.Series);

                return DownloadSpecDecision.Reject(DownloadRejectionReason.NoMatchingTag, "Series tags do not match any of the indexer tags");
            }

            return DownloadSpecDecision.Accept();
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            if (subject.Release == null || subject.Movie?.Tags == null || subject.Release.IndexerId == 0)
            {
                return DownloadSpecDecision.Accept();
            }

            IndexerDefinition indexer;
            try
            {
                indexer = _indexerFactory.Get(subject.Release.IndexerId);
            }
            catch (ModelNotFoundException)
            {
                _logger.Debug("Indexer with id {0} does not exist, skipping indexer tags check", subject.Release.IndexerId);
                return DownloadSpecDecision.Accept();
            }

            // If indexer has tags, check that at least one of them is present on the movie
            var indexerTags = indexer.Tags;

            if (indexerTags.Any() && indexerTags.Intersect(subject.Movie.Tags).Empty())
            {
                _logger.Debug("Indexer {0} has tags. None of these are present on movie {1}. Rejecting", subject.Release.Indexer, subject.Movie);

                return DownloadSpecDecision.Reject(DownloadRejectionReason.NoMatchingTag, "Movie tags do not match any of the indexer tags");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
