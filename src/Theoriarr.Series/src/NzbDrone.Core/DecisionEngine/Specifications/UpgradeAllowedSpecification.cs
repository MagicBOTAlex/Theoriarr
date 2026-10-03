using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class UpgradeAllowedSpecification : DownloadDecisionEngineSpecification
    {
        private readonly UpgradableSpecification _upgradableSpecification;
        private readonly ICustomFormatCalculationService _formatService;
        private readonly Logger _logger;

        public UpgradeAllowedSpecification(UpgradableSpecification upgradableSpecification,
                                           ICustomFormatCalculationService formatService,
                                           Logger logger)
        {
            _upgradableSpecification = upgradableSpecification;
            _formatService = formatService;
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
            var qualityProfile = subject.Series.QualityProfile.Value;

            foreach (var file in subject.Episodes.Where(c => c.EpisodeFileId != 0).Select(c => c.EpisodeFile.Value))
            {
                if (file == null)
                {
                    _logger.Debug("File is no longer available, skipping this file.");
                    continue;
                }

                var fileCustomFormats = _formatService.ParseCustomFormat(file, subject.Series);

                _logger.Debug("Comparing file quality with report. Existing file is {0}", file.Quality);

                if (!_upgradableSpecification.IsUpgradeAllowed(qualityProfile,
                                                               file.Quality,
                                                               fileCustomFormats,
                                                               subject.ParsedEpisodeInfo.Quality,
                                                               subject.CustomFormats))
                {
                    _logger.Debug("Upgrading is not allowed by the quality profile");

                    return DownloadSpecDecision.Reject(DownloadRejectionReason.QualityUpgradesDisabled, "Existing file and the Quality profile does not allow upgrades");
                }
            }

            return DownloadSpecDecision.Accept();
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            var qualityProfile = subject.Movie.QualityProfile;

            if (subject.Movie.MovieFileId != 0)
            {
                var file = subject.Movie.MovieFile;

                if (file == null)
                {
                    _logger.Debug("File is no longer available, skipping this file.");
                    return DownloadSpecDecision.Accept();
                }

                file.Movie = subject.Movie;
                var customFormats = _formatService.ParseCustomFormat(file);
                _logger.Debug("Comparing file quality with report. Existing file is {0} [{1}]", file.Quality, customFormats.ConcatToString());

                if (!_upgradableSpecification.IsUpgradeAllowed(qualityProfile,
                                                               file.Quality,
                                                               customFormats,
                                                               subject.ParsedMovieInfo.Quality,
                                                               subject.CustomFormats))
                {
                    _logger.Debug("Upgrading is not allowed by the quality profile");

                    return DownloadSpecDecision.Reject(DownloadRejectionReason.QualityUpgradesDisabled, "Existing file and the Quality profile does not allow upgrades");
                }
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
