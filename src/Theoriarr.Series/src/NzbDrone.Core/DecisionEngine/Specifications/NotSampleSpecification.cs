using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class NotSampleSpecification : DownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public override SpecificationPriority Priority => SpecificationPriority.Default;
        public override RejectionType Type => RejectionType.Permanent;

        public NotSampleSpecification(Logger logger)
        {
            _logger = logger;
        }

        public override bool AppliesTo(IRemoteSubject subject)
        {
            return subject is RemoteEpisode || subject is RemoteMovie;
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            return CheckSample(subject.Release);
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            return CheckSample(subject.Release);
        }

        private DownloadSpecDecision CheckSample(ReleaseInfo release)
        {
            if (release.Title.ToLower().Contains("sample") && release.Size < 70.Megabytes())
            {
                _logger.Debug("Sample release, rejecting.");
                return DownloadSpecDecision.Reject(DownloadRejectionReason.Sample, "Sample");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
