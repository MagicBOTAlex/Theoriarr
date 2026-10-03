using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public interface IDownloadDecisionEngineSpecification
    {
        RejectionType Type { get; }

        SpecificationPriority Priority { get; }

        // D7 gate: replaces MOVIES' catch(NotImplementedException) skip. The maker only
        // evaluates a spec for subjects it declares it can handle.
        bool AppliesTo(IRemoteSubject subject);

        DownloadSpecDecision IsSatisfiedBy(IRemoteSubject subject, ReleaseDecisionInformation information);
    }
}
