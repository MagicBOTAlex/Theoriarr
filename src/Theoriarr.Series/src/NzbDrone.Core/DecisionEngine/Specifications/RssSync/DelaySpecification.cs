using System.Linq;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.DecisionEngine.Specifications.RssSync
{
    public class DelaySpecification : DownloadDecisionEngineSpecification
    {
        private readonly IPendingReleaseService _pendingReleaseService;
        private readonly IUpgradableSpecification _upgradableSpecification;
        private readonly ICustomFormatCalculationService _formatService;
        private readonly IDelayProfileService _delayProfileService;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public DelaySpecification(IPendingReleaseService pendingReleaseService,
                                  IUpgradableSpecification upgradableSpecification,
                                  ICustomFormatCalculationService formatService,
                                  IDelayProfileService delayProfileService,
                                  IConfigService configService,
                                  Logger logger)
        {
            _pendingReleaseService = pendingReleaseService;
            _upgradableSpecification = upgradableSpecification;
            _formatService = formatService;
            _delayProfileService = delayProfileService;
            _configService = configService;
            _logger = logger;
        }

        public override SpecificationPriority Priority => SpecificationPriority.Database;
        public override RejectionType Type => RejectionType.Temporary;

        public override bool AppliesTo(IRemoteSubject subject)
        {
            return subject is RemoteEpisode || subject is RemoteMovie;
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteEpisode subject, ReleaseDecisionInformation information)
        {
            if (information.SearchCriteria is { UserInvokedSearch: true })
            {
                _logger.Debug("Ignoring delay for user invoked search");
                return DownloadSpecDecision.Accept();
            }

            var qualityProfile = subject.Series.QualityProfile.Value;
            var delayProfile = _delayProfileService.BestForTags(subject.Series.Tags);
            var delay = delayProfile.GetProtocolDelay(subject.Release.DownloadProtocol);
            var isPreferredProtocol = subject.Release.DownloadProtocol == delayProfile.PreferredProtocol;
            var preferPropersAndRepacks = _configService.DownloadPropersAndRepacks == ProperDownloadTypes.PreferAndUpgrade;

            if (delay == 0)
            {
                _logger.Debug("Delay Profile does not require a waiting period before download for {0}.", subject.Release.DownloadProtocol);
                return DownloadSpecDecision.Accept();
            }

            _logger.Debug("Delay Profile requires a waiting period of {0} minutes for {1}", delay, subject.Release.DownloadProtocol);

            var qualityComparer = new QualityModelComparer(qualityProfile);

            if (isPreferredProtocol && preferPropersAndRepacks)
            {
                foreach (var file in subject.Episodes.Where(c => c.EpisodeFileId != 0).Select(c => c.EpisodeFile.Value))
                {
                    var currentQuality = file.Quality;
                    var newQuality = subject.ParsedEpisodeInfo.Quality;
                    var qualityCompare = qualityComparer.Compare(newQuality?.Quality, currentQuality.Quality);

                    if (qualityCompare == 0 && newQuality?.Revision.CompareTo(currentQuality.Revision) > 0)
                    {
                        _logger.Debug("New quality is a better revision for existing quality, skipping delay");
                        return DownloadSpecDecision.Accept();
                    }
                }
            }

            // If quality meets or exceeds the best allowed quality in the profile accept it immediately
            if (delayProfile.BypassIfHighestQuality)
            {
                var bestQualityInProfile = qualityProfile.LastAllowedQuality();
                var isBestInProfile = qualityComparer.Compare(subject.ParsedEpisodeInfo.Quality.Quality, bestQualityInProfile) >= 0;

                if (isBestInProfile && isPreferredProtocol)
                {
                    _logger.Debug("Quality is highest in profile for preferred protocol, will not delay");
                    return DownloadSpecDecision.Accept();
                }
            }

            // If quality meets or exceeds the best allowed quality in the profile accept it immediately
            if (delayProfile.BypassIfAboveCustomFormatScore)
            {
                var score = subject.CustomFormatScore;
                var minimum = delayProfile.MinimumCustomFormatScore;

                if (score >= minimum && isPreferredProtocol)
                {
                    _logger.Debug("Custom format score ({0}) meets minimum ({1}) for preferred protocol, will not delay", score, minimum);
                    return DownloadSpecDecision.Accept();
                }
            }

            var episodeIds = subject.Episodes.Select(e => e.Id);

            var oldest = _pendingReleaseService.OldestPendingRelease(subject.Series.Id, episodeIds.ToArray());

            if (oldest != null && oldest.Release.AgeMinutes > delay)
            {
                _logger.Debug("Oldest pending release {0} has been delayed for {1}, longer than the set delay of {2}. Release will be accepted", oldest.Release.Title, oldest.Release.AgeMinutes, delay);
                return DownloadSpecDecision.Accept();
            }

            if (subject.Release.AgeMinutes < delay)
            {
                _logger.Debug("Waiting for better quality release, There is a {0} minute delay on {1}", delay, subject.Release.DownloadProtocol);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.MinimumAgeDelay, "Waiting for better quality release");
            }

            return DownloadSpecDecision.Accept();
        }

        protected override DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            if (information.SearchCriteria is { UserInvokedSearch: true })
            {
                _logger.Debug("Ignoring delay for user invoked search");
                return DownloadSpecDecision.Accept();
            }

            var profile = subject.Movie.QualityProfile;
            var delayProfile = _delayProfileService.BestForTags(subject.Movie.Tags);
            var delay = delayProfile.GetProtocolDelay(subject.Release.DownloadProtocol);
            var isPreferredProtocol = subject.Release.DownloadProtocol == delayProfile.PreferredProtocol;

            if (delay == 0)
            {
                _logger.Debug("Delay Profile does not require a waiting period before download for {0}.", subject.Release.DownloadProtocol);
                return DownloadSpecDecision.Accept();
            }

            _logger.Debug("Delay Profile requires a waiting period of {0} minutes for {1}", delay, subject.Release.DownloadProtocol);

            var qualityComparer = new QualityModelComparer(profile);

            var file = subject.Movie.MovieFile;

            if (isPreferredProtocol && subject.Movie.MovieFileId != 0 && file != null)
            {
                var customFormats = _formatService.ParseCustomFormat(file);
                var upgradeableRejectReason = _upgradableSpecification.IsUpgradable(profile,
                    file.Quality,
                    customFormats,
                    subject.ParsedMovieInfo.Quality,
                    subject.CustomFormats);

                if (upgradeableRejectReason == UpgradeableRejectReason.None &&
                    _upgradableSpecification.IsRevisionUpgrade(file.Quality, subject.ParsedMovieInfo.Quality))
                {
                    _logger.Debug("New quality is a better revision for existing quality, skipping delay");
                    return DownloadSpecDecision.Accept();
                }
            }

            // If quality meets or exceeds the best allowed quality in the profile accept it immediately
            if (delayProfile.BypassIfHighestQuality)
            {
                var bestQualityInProfile = profile.LastAllowedQuality();
                var isBestInProfile = qualityComparer.Compare(subject.ParsedMovieInfo.Quality.Quality, bestQualityInProfile) >= 0;

                if (isBestInProfile && isPreferredProtocol)
                {
                    _logger.Debug("Quality is highest in profile for preferred protocol, will not delay.");
                    return DownloadSpecDecision.Accept();
                }
            }

            // If quality meets or exceeds the best allowed quality in the profile accept it immediately
            if (delayProfile.BypassIfAboveCustomFormatScore)
            {
                var score = subject.CustomFormatScore;
                var minimum = delayProfile.MinimumCustomFormatScore;

                if (score >= minimum && isPreferredProtocol)
                {
                    _logger.Debug("Custom format score ({0}) meets minimum ({1}) for preferred protocol, will not delay", score, minimum);
                    return DownloadSpecDecision.Accept();
                }
            }

            var oldest = _pendingReleaseService.OldestPendingRelease(subject.Movie.Id);

            if (oldest != null && oldest.Release.AgeMinutes > delay)
            {
                _logger.Debug("Oldest pending release {0} has been delayed for {1}, longer than the set delay of {2}. Release will be accepted", oldest.Release.Title, oldest.Release.AgeMinutes, delay);
                return DownloadSpecDecision.Accept();
            }

            if (subject.Release.AgeMinutes < delay)
            {
                _logger.Debug("Waiting for better quality release, There is a {0} minute delay on {1}", delay, subject.Release.DownloadProtocol);
                return DownloadSpecDecision.Reject(DownloadRejectionReason.MinimumAgeDelay, "Waiting for better quality release");
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
