using System;
using System.Threading;

namespace NzbDrone.Core.IndexerSearch
{
    // Ambient state set by the release controllers for interactive searches. The search services
    // read it to publish progress events for the right request and to abort the indexer requests
    // when the client goes away, without threading parameters through every search signature.
    public static class ReleaseSearchProgressContext
    {
        private static readonly AsyncLocal<ScopeState> _current = new AsyncLocal<ScopeState>();

        public static string SearchId => _current.Value?.SearchId;

        public static CancellationToken Token => _current.Value?.Token ?? CancellationToken.None;

        public static IDisposable Begin(string searchId, CancellationToken token = default)
        {
            var previous = _current.Value;
            _current.Value = new ScopeState(searchId, token);

            return new Scope(previous);
        }

        private sealed class ScopeState
        {
            public ScopeState(string searchId, CancellationToken token)
            {
                SearchId = searchId;
                Token = token;
            }

            public string SearchId { get; }
            public CancellationToken Token { get; }
        }

        private sealed class Scope : IDisposable
        {
            private readonly ScopeState _previous;

            public Scope(ScopeState previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                _current.Value = _previous;
            }
        }
    }
}
