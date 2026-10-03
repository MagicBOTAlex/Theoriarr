namespace NzbDrone.Core.Indexers.Exceptions
{
    // The indexer API answered with a "gone"/unavailable error (for example Newznab/Torznab
    // <error code="410">), meaning the feed endpoint is no longer there rather than a
    // transient request failure.
    public class IndexerUnavailableException : IndexerException
    {
        public IndexerUnavailableException(IndexerResponse response, string message, params object[] args)
            : base(response, message, args)
        {
        }

        public IndexerUnavailableException(IndexerResponse response, string message)
            : base(response, message)
        {
        }
    }
}
