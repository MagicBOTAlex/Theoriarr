using NzbDrone.Core.Configuration;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.Config
{
    public class IndexerConfigResource : RestResource
    {
        public int MinimumAge { get; set; }
        public int Retention { get; set; }
        public int MaximumSize { get; set; }
        public int RssSyncInterval { get; set; }

        // Movies (MOVIES union)
        public bool PreferIndexerFlags { get; set; }
        public int AvailabilityDelay { get; set; }
        public bool AllowHardcodedSubs { get; set; }
        public string WhitelistedHardcodedSubs { get; set; }
    }

    public static class IndexerConfigResourceMapper
    {
        public static IndexerConfigResource ToResource(IConfigService model)
        {
            return new IndexerConfigResource
            {
                MinimumAge = model.MinimumAge,
                Retention = model.Retention,
                MaximumSize = model.MaximumSize,
                RssSyncInterval = model.RssSyncInterval,
                PreferIndexerFlags = model.PreferIndexerFlags,
                AvailabilityDelay = model.AvailabilityDelay,
                AllowHardcodedSubs = model.AllowHardcodedSubs,
                WhitelistedHardcodedSubs = model.WhitelistedHardcodedSubs
            };
        }
    }
}
