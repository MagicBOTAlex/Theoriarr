using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class RawDiskSpecification : DownloadDecisionEngineSpecification
    {
        private static readonly Regex[] DiscRegex = new[]
                                                    {
                                                        new Regex(@"(?:dis[ck])(?:[-_. ]\d+[-_. ])(?:(?:(?:480|720|1080|2160)[ip]|)[-_. ])?(?:Blu\-?ray)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                                                        new Regex(@"(?:(?:480|720|1080|2160)[ip]|)[-_. ](?:full)[-_. ](?:Blu\-?ray)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
                                                        new Regex(@"(?:\d?x?M?DVD-?[R59])(?:[ ._]|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase)
                                                    };

        private static readonly string[] _dvdContainerTypes = new[] { "vob", "iso" };
        private static readonly string[] _blurayContainerTypes = new[] { "m2ts" };

        private readonly Logger _logger;

        public RawDiskSpecification(Logger logger)
        {
            _logger = logger;
        }

        public override SpecificationPriority Priority => SpecificationPriority.Default;
        public override RejectionType Type => RejectionType.Permanent;

        public override bool AppliesTo(IRemoteSubject subject)
        {
            return subject is RemoteEpisode || subject is RemoteMovie;
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            return CheckRawDisk(subject.Release);
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            return CheckRawDisk(subject.Release);
        }

        private DownloadSpecDecision CheckRawDisk(ReleaseInfo release)
        {
            if (release == null)
            {
                return DownloadSpecDecision.Accept();
            }

            foreach (var regex in DiscRegex)
            {
                if (regex.IsMatch(release.Title))
                {
                    _logger.Debug("Release contains raw Bluray/DVD, rejecting.");
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.Raw, "Raw Bluray/DVD release");
                }
            }

            if (release.Container.IsNullOrWhiteSpace())
            {
                return DownloadSpecDecision.Accept();
            }

            if (_dvdContainerTypes.Contains(release.Container.ToLower()))
            {
                _logger.Debug("Release contains raw DVD, rejecting.");
                return DownloadSpecDecision.Reject(DownloadRejectionReason.Raw, "Raw DVD release");
            }

            if (_blurayContainerTypes.Contains(release.Container.ToLower()))
            {
                _logger.Debug("Release contains raw Bluray, rejecting.");
                return DownloadSpecDecision.Reject(DownloadRejectionReason.Raw, "Raw Bluray release");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
