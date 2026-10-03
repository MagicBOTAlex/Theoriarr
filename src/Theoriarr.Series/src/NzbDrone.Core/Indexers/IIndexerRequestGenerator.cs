using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers
{
    public interface IIndexerRequestGenerator
    {
        IndexerPageableRequestChain GetRecentRequests();

        // Movie request generators only need to implement the overload(s) they support.
        // Default implementations return an empty chain so per-indexer files need no stubs.
        IndexerPageableRequestChain GetSearchRequests(MovieSearchCriteria searchCriteria) => new IndexerPageableRequestChain();

        IndexerPageableRequestChain GetSearchRequests(SingleEpisodeSearchCriteria searchCriteria) => new IndexerPageableRequestChain();

        IndexerPageableRequestChain GetSearchRequests(SeasonSearchCriteria searchCriteria) => new IndexerPageableRequestChain();

        IndexerPageableRequestChain GetSearchRequests(DailyEpisodeSearchCriteria searchCriteria) => new IndexerPageableRequestChain();

        IndexerPageableRequestChain GetSearchRequests(DailySeasonSearchCriteria searchCriteria) => new IndexerPageableRequestChain();

        IndexerPageableRequestChain GetSearchRequests(AnimeEpisodeSearchCriteria searchCriteria) => new IndexerPageableRequestChain();

        IndexerPageableRequestChain GetSearchRequests(AnimeSeasonSearchCriteria searchCriteria) => new IndexerPageableRequestChain();

        IndexerPageableRequestChain GetSearchRequests(SpecialEpisodeSearchCriteria searchCriteria) => new IndexerPageableRequestChain();

        // The cookie subsystem has been dropped project-wide (see D8).
    }
}
