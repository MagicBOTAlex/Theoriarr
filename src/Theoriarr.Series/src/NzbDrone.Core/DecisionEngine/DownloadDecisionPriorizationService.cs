using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.DecisionEngine
{
    public interface IPrioritizeDownloadDecision
    {
        List<DownloadDecision> PrioritizeDecisions(List<DownloadDecision> decisions);
        List<DownloadDecision> PrioritizeDecisionsForMovies(List<DownloadDecision> decisions);
    }

    public class DownloadDecisionPriorizationService : IPrioritizeDownloadDecision
    {
        private readonly IConfigService _configService;
        private readonly IDelayProfileService _delayProfileService;
        private readonly IQualityDefinitionService _qualityDefinitionService;

        public DownloadDecisionPriorizationService(IConfigService configService, IDelayProfileService delayProfileService, IQualityDefinitionService qualityDefinitionService)
        {
            _configService = configService;
            _delayProfileService = delayProfileService;
            _qualityDefinitionService = qualityDefinitionService;
        }

        public List<DownloadDecision> PrioritizeDecisions(List<DownloadDecision> decisions)
        {
            var seriesDecisions = decisions.Where(c => c.RemoteEpisode != null);
            var otherDecisions = decisions.Where(c => c.RemoteEpisode == null);

            return seriesDecisions.Where(c => c.RemoteEpisode.Series != null)
                            .GroupBy(c => c.RemoteEpisode.Series.Id, (seriesId, downloadDecisions) =>
                                {
                                    return downloadDecisions.OrderByDescending(decision => decision, new DownloadDecisionComparer(_configService, _delayProfileService, _qualityDefinitionService));
                                })
                            .SelectMany(c => c)
                            .Union(seriesDecisions.Where(c => c.RemoteEpisode.Series == null))
                            .Union(otherDecisions)
                            .ToList();
        }

        public List<DownloadDecision> PrioritizeDecisionsForMovies(List<DownloadDecision> decisions)
        {
            var comparer = new DownloadDecisionComparer(_configService, _delayProfileService, _qualityDefinitionService);
            var movieDecisions = decisions.Where(c => c.RemoteMovie != null);
            var otherDecisions = decisions.Where(c => c.RemoteMovie == null);

            return movieDecisions.Where(c => c.RemoteMovie.Movie != null)
                            .GroupBy(c => c.RemoteMovie.Movie.Id, (movieId, downloadDecisions) =>
                                {
                                    return downloadDecisions.OrderByDescending(decision => decision, comparer);
                                })
                            .SelectMany(c => c)
                            .Union(movieDecisions.Where(c => c.RemoteMovie.Movie == null))
                            .Union(otherDecisions)
                            .ToList();
        }
    }
}
