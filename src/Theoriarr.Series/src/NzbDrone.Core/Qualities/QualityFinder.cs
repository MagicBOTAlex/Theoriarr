using System;
using System.Linq;
using NLog;
using NzbDrone.Common.Instrumentation;

namespace NzbDrone.Core.Qualities
{
    public static class QualityFinder
    {
        private static readonly Logger Logger = NzbDroneLogger.GetLogger(typeof(QualityFinder));

        public static Quality FindBySourceAndResolution(QualitySource source, int resolution, Modifier modifier = Modifier.NONE)
        {
            // Exact 3-way match. SERIES call sites use the default Modifier.NONE, so this keeps
            // the previous SERIES behavior while disambiguating MOVIES modifier qualities that
            // share a source/resolution with a SERIES quality (e.g. DVDSCR/REGIONAL/DVDR vs DVD).
            var matchingQuality = Quality.All.SingleOrDefault(q => q.Source == source && q.Resolution == resolution && q.Modifier == modifier);

            if (matchingQuality != null)
            {
                return matchingQuality;
            }

            // Handle 576p releases that have a Television or Web source, so they don't get rolled up to Bluray 576p
            if (modifier == Modifier.NONE && resolution < 720)
            {
                switch (source)
                {
                    case QualitySource.Television:
                        return Quality.SDTV;
                    case QualitySource.Web:
                        return Quality.WEBDL480p;
                    case QualitySource.WebRip:
                        return Quality.WEBRip480p;
                }
            }

            // MOVIES pre-release qualities have an unknown (0) resolution and are matched by source + modifier.
            var matchingUnknownResolution = Quality.All
                .Where(q => q.Source == source && q.Resolution == 0 && q.Modifier == modifier && q != Quality.Unknown)
                .ToList();

            if (matchingUnknownResolution.Any())
            {
                return matchingUnknownResolution.First();
            }

            // Stay within the source family before crossing into another one: a source that has no
            // quality at the claimed resolution (e.g. a DVD/.iso release mislabelled 1080p) keeps its
            // source and takes the closest resolution it does have, rather than silently becoming a
            // Bluray/Web quality. Callers that only know the resolution (source Unknown) skip this.
            if (source != QualitySource.Unknown)
            {
                var nearestSameSource = Quality.All
                    .Where(q => q.Source == source && q.Modifier == modifier && q != Quality.Unknown && q.Resolution > 0)
                    .OrderBy(q => Math.Abs(q.Resolution - resolution))
                    .ThenBy(q => q.Resolution)
                    .FirstOrDefault();

                if (nearestSameSource != null)
                {
                    Logger.Debug("Unable to find exact quality for {0} and {1}. Using {2} (same source) as fallback", source, resolution, nearestSameSource);

                    return nearestSameSource;
                }
            }

            var matchingResolution = Quality.All.Where(q => q.Resolution == resolution)
                                            .OrderBy(q => q.Source)
                                            .ToList();

            var nearestQuality = Quality.Unknown;

            foreach (var quality in matchingResolution)
            {
                if (quality.Source >= source)
                {
                    nearestQuality = quality;
                    break;
                }
            }

            if (source == QualitySource.Unknown)
            {
                // Expected when nothing identified the source (common during bulk scans); the
                // resolution-based default is the intended result, not a problem to warn about.
                Logger.Debug("Unable to find exact quality for {0} and {1}. Using {2} as fallback", source, resolution, nearestQuality);
            }
            else
            {
                Logger.Warn("Unable to find exact quality for {0} and {1}. Using {2} as fallback", source, resolution, nearestQuality);
            }

            return nearestQuality;
        }
    }
}
