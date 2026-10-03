using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class BlockedIndexerSpecification : DownloadDecisionEngineSpecification
    {
        private readonly IIndexerStatusService _indexerStatusService;
        private readonly Logger _logger;

        private readonly ICachedDictionary<IndexerStatus> _blockedIndexerCache;

        public BlockedIndexerSpecification(IIndexerStatusService indexerStatusService, ICacheManager cacheManager, Logger logger)
        {
            _indexerStatusService = indexerStatusService;
            _logger = logger;

            _blockedIndexerCache = cacheManager.GetCacheDictionary(GetType(), "blocked", FetchBlockedIndexer, TimeSpan.FromSeconds(15));
        }

        public override SpecificationPriority Priority => SpecificationPriority.Database;
        public override RejectionType Type => RejectionType.Temporary;

        public override bool AppliesTo(IRemoteSubject subject)
        {
            return subject is RemoteEpisode || subject is RemoteMovie;
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            return CheckBlocked(subject.Release);
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            return CheckBlocked(subject.Release);
        }

        private DownloadSpecDecision CheckBlocked(ReleaseInfo release)
        {
            var status = _blockedIndexerCache.Find(release.IndexerId.ToString());
            if (status != null)
            {
                return DownloadSpecDecision.Reject(DownloadRejectionReason.IndexerDisabled, $"Indexer {release.Indexer} is blocked till {status.DisabledTill} due to failures, cannot grab release.");
            }

            return DownloadSpecDecision.Accept();
        }

        private IDictionary<string, IndexerStatus> FetchBlockedIndexer()
        {
            return _indexerStatusService.GetBlockedProviders().ToDictionary(v => v.ProviderId.ToString());
        }
    }
}
