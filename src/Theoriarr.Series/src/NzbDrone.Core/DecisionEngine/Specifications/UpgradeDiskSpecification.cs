using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class UpgradeDiskSpecification : DownloadDecisionEngineSpecification
    {
        private readonly UpgradableSpecification _upgradableSpecification;
        private readonly IConfigService _configService;
        private readonly ICustomFormatCalculationService _formatService;
        private readonly Logger _logger;

        public UpgradeDiskSpecification(UpgradableSpecification upgradableSpecification,
                                        IConfigService configService,
                                        ICustomFormatCalculationService formatService,
                                        Logger logger)
        {
            _configService = configService;
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

            if (subject.ParsedEpisodeInfo.FullSeason)
            {
                var totalEpisodesInPack = subject.Episodes.Count;

                if (totalEpisodesInPack == 0)
                {
                    // Should not happen, but good to guard against it.
                    return DownloadSpecDecision.Accept();
                }

                // Count missing episodes as upgradable
                var missingEpisodesCount = subject.Episodes.Count(c => c.EpisodeFileId == 0);
                var upgradedCount = missingEpisodesCount;
                _logger.Debug("{0} episodes are missing from disk and are considered upgradable.", upgradedCount);

                // Filter for episodes that already exist on disk to check for quality upgrades
                var existingEpisodeFiles = subject.Episodes.Where(c => c.EpisodeFileId != 0)
                                                           .Select(c => c.EpisodeFile.Value)
                                                           .ToList();

                // If all episodes in the pack are missing, accept it immediately.
                if (!existingEpisodeFiles.Any())
                {
                    _logger.Debug("All episodes in season pack are missing, accepting.");
                    return DownloadSpecDecision.Accept();
                }

                // Check if any of the existing files can also be upgraded
                foreach (var file in existingEpisodeFiles)
                {
                    _logger.Debug("Comparing file quality with report. Existing file is {0}.", file.Quality);

                    if (!_upgradableSpecification.CutoffNotMet(qualityProfile,
                            file.Quality,
                            _formatService.ParseCustomFormat(file),
                            subject.ParsedEpisodeInfo.Quality))
                    {
                        _logger.Debug("Cutoff already met for existing file, not an upgrade.");
                        continue;
                    }

                    var customFormats = _formatService.ParseCustomFormat(file);

                    var upgradeableRejectReason = _upgradableSpecification.IsUpgradable(qualityProfile,
                        file.Quality,
                        customFormats,
                        subject.ParsedEpisodeInfo.Quality,
                        subject.CustomFormats);

                    if (upgradeableRejectReason == UpgradeableRejectReason.None)
                    {
                        _logger.Debug("Existing episode is upgradable.");
                        upgradedCount++;
                    }
                }

                var seasonPackUpgrade = _configService.SeasonPackUpgrade;
                var seasonPackUpgradeThreshold = _configService.SeasonPackUpgradeThreshold;
                _logger.Debug("Total upgradable episodes: {0} out of {1}. Season import setting: {2}, Threshold: {3}%", upgradedCount, totalEpisodesInPack, seasonPackUpgrade, seasonPackUpgradeThreshold);
                var upgradablePercentage = (double)upgradedCount / totalEpisodesInPack * 100;
                if (seasonPackUpgrade == SeasonPackUpgradeType.Any)
                {
                    if (upgradedCount > 0)
                    {
                        return DownloadSpecDecision.Accept();
                    }
                }
                else
                {
                    var threshold = seasonPackUpgrade == SeasonPackUpgradeType.All
                        ? 100.0
                        : _configService.SeasonPackUpgradeThreshold;
                    if (upgradablePercentage >= threshold)
                    {
                        return DownloadSpecDecision.Accept();
                    }
                }

                return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskNotUpgrade, $"Season pack does not meet the upgrade criteria. Upgradable: {upgradedCount}/{totalEpisodesInPack} ({upgradablePercentage:0.##}%), Mode: {seasonPackUpgrade}, Threshold: {seasonPackUpgradeThreshold}%");
            }

            foreach (var file in subject.Episodes.Where(c => c.EpisodeFileId != 0).Select(c => c.EpisodeFile.Value))
            {
                var decision = CheckUpgradeSpecification(file, qualityProfile, subject);
                if (decision != null)
                {
                    return decision;
                }
            }

            return DownloadSpecDecision.Accept();
        }

        private DownloadSpecDecision CheckUpgradeSpecification(NzbDrone.Core.MediaFiles.EpisodeFile file, NzbDrone.Core.Profiles.Qualities.QualityProfile qualityProfile, RemoteEpisode subject)
        {
            if (file == null)
            {
                _logger.Debug("File is no longer available, skipping this file.");
                return null;
            }

            _logger.Debug("Comparing file quality with report. Existing file is {0}.", file.Quality);

            if (!_upgradableSpecification.CutoffNotMet(qualityProfile,
                    file.Quality,
                    _formatService.ParseCustomFormat(file),
                    subject.ParsedEpisodeInfo.Quality))
            {
                _logger.Debug("Cutoff already met, rejecting.");
                var cutoff = qualityProfile.UpgradeAllowed ? qualityProfile.Cutoff : qualityProfile.FirststAllowedQuality().Id;
                var qualityCutoff = qualityProfile.Items[qualityProfile.GetIndex(cutoff).Index];
                return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskCutoffMet, "Existing file meets cutoff: {0}", qualityCutoff);
            }

            var customFormats = _formatService.ParseCustomFormat(file);
            var upgradeableRejectReason = _upgradableSpecification.IsUpgradable(qualityProfile,
                file.Quality,
                customFormats,
                subject.ParsedEpisodeInfo.Quality,
                subject.CustomFormats);

            switch (upgradeableRejectReason)
            {
                case UpgradeableRejectReason.None:
                    return null;

                case UpgradeableRejectReason.BetterQuality:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskHigherPreference, "Existing file on disk is of equal or higher preference: {0}", file.Quality);

                case UpgradeableRejectReason.BetterRevision:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskHigherRevision, "Existing file on disk is of equal or higher revision: {0}", file.Quality.Revision);

                case UpgradeableRejectReason.QualityCutoff:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskCutoffMet, "Existing file on disk meets quality cutoff: {0}", qualityProfile.Items[qualityProfile.GetIndex(qualityProfile.Cutoff).Index]);

                case UpgradeableRejectReason.CustomFormatCutoff:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskCustomFormatCutoffMet, "Existing file on disk meets Custom Format cutoff: {0}", qualityProfile.CutoffFormatScore);

                case UpgradeableRejectReason.CustomFormatScore:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskCustomFormatScore, "Existing file on disk has a equal or higher Custom Format score: {0}", qualityProfile.CalculateCustomFormatScore(customFormats));

                case UpgradeableRejectReason.MinCustomFormatScore:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskCustomFormatScoreIncrement, "Existing file on disk has Custom Format score within Custom Format score increment: {0}", qualityProfile.MinUpgradeFormatScore);

                case UpgradeableRejectReason.UpgradesNotAllowed:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskUpgradesNotAllowed, "Existing file on disk and Quality Profile '{0}' does not allow upgrades", qualityProfile.Name);
            }

            return null;
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            var qualityProfile = subject.Movie.QualityProfile;
            var file = subject.Movie.MovieFile;

            if (file == null)
            {
                _logger.Debug("File is no longer available, skipping this file.");
                return DownloadSpecDecision.Accept();
            }

            file.Movie = subject.Movie;

            var customFormats = _formatService.ParseCustomFormat(file);

            _logger.Debug("Comparing file quality with report. Existing file is {0} [{1}].", file.Quality, customFormats.ConcatToString());

            if (!_upgradableSpecification.CutoffNotMet(qualityProfile,
                    file.Quality,
                    customFormats,
                    subject.ParsedMovieInfo.Quality))
            {
                _logger.Debug("Cutoff already met, rejecting.");
                var cutoff = qualityProfile.UpgradeAllowed ? qualityProfile.Cutoff : qualityProfile.FirststAllowedQuality().Id;
                var qualityCutoff = qualityProfile.Items[qualityProfile.GetIndex(cutoff).Index];
                return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskCutoffMet, "Existing file meets cutoff: {0} [{1}]", qualityCutoff, customFormats.ConcatToString());
            }

            var upgradeableRejectReason = _upgradableSpecification.IsUpgradable(qualityProfile,
                file.Quality,
                customFormats,
                subject.ParsedMovieInfo.Quality,
                subject.CustomFormats);

            switch (upgradeableRejectReason)
            {
                case UpgradeableRejectReason.None:
                    return DownloadSpecDecision.Accept();

                case UpgradeableRejectReason.BetterQuality:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskHigherPreference, "Existing file on disk is of equal or higher preference: {0}", file.Quality);

                case UpgradeableRejectReason.BetterRevision:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskHigherRevision, "Existing file on disk is of equal or higher revision: {0}", file.Quality.Revision);

                case UpgradeableRejectReason.QualityCutoff:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskCutoffMet, "Existing file on disk meets quality cutoff: {0}", qualityProfile.Items[qualityProfile.GetIndex(qualityProfile.Cutoff).Index]);

                case UpgradeableRejectReason.CustomFormatCutoff:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskCustomFormatCutoffMet, "Existing file on disk meets Custom Format cutoff: {0}", qualityProfile.CutoffFormatScore);

                case UpgradeableRejectReason.CustomFormatScore:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskCustomFormatScore, "Existing file on disk has a equal or higher Custom Format score: {0}", qualityProfile.CalculateCustomFormatScore(customFormats));

                case UpgradeableRejectReason.MinCustomFormatScore:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskCustomFormatScoreIncrement, "Existing file on disk has Custom Format score within Custom Format score increment: {0}", qualityProfile.MinUpgradeFormatScore);

                case UpgradeableRejectReason.UpgradesNotAllowed:
                    return DownloadSpecDecision.Reject(DownloadRejectionReason.DiskUpgradesNotAllowed, "Existing file on disk and Quality Profile '{0}' does not allow upgrades", qualityProfile.Name);
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
