using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    // D7 base for decision specs. Gives every spec an AppliesTo gate and lets it expose
    // typed per-domain bodies while the interface stays IRemoteSubject based. A spec that
    // supports both RemoteMovie and RemoteEpisode overrides AppliesTo and the matching
    // protected overload. Specs that only override the RemoteEpisode body remain
    // series-only (the movie overload defaults to Accept and AppliesTo rejects movies).
    public abstract class DownloadDecisionEngineSpecification : IDownloadDecisionEngineSpecification
    {
        public abstract RejectionType Type { get; }

        public abstract SpecificationPriority Priority { get; }

        public virtual bool AppliesTo(IRemoteSubject subject)
        {
            return subject is RemoteEpisode;
        }

        public DownloadSpecDecision IsSatisfiedBy(IRemoteSubject subject, ReleaseDecisionInformation information)
        {
            return subject switch
            {
                RemoteEpisode episode => IsSatisfiedBy(episode, information),
                RemoteMovie movie => IsSatisfiedBy(movie, information),
                _ => DownloadSpecDecision.Accept()
            };
        }

        protected virtual DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            return DownloadSpecDecision.Accept();
        }

        protected virtual DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            return DownloadSpecDecision.Accept();
        }
    }
}
