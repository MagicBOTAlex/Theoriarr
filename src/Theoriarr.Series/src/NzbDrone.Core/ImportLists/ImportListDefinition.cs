using System;
using Equ;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Tv;
using MovieMonitorTypes = NzbDrone.Core.Movies.MonitorTypes;
using MovieStatusType = NzbDrone.Core.Movies.MovieStatusType;

namespace NzbDrone.Core.ImportLists
{
    // D5/§2: one ImportLists table holds the SERIES∪MOVIES column superset plus a MediaType
    // discriminator. The two API vocabularies are preserved by exposing both property names;
    // where a concept is identical it is a single backing field (EnableAutomaticAdd <-> Enabled).
    public class ImportListDefinition : ProviderDefinition, IEquatable<ImportListDefinition>
    {
        private static readonly MemberwiseEqualityComparer<ImportListDefinition> Comparer = MemberwiseEqualityComparer<ImportListDefinition>.ByProperties;

        // Active list. Radarr V3 serializes "enabled"; Sonarr V5 serializes "enableAutomaticAdd".
        public bool Enabled { get; set; }

        public bool EnableAutomaticAdd
        {
            get => Enabled;
            set => Enabled = value;
        }

        // Auto-add. Radarr V3 "enableAuto"; Sonarr series lists are inherently auto-add.
        public bool EnableAuto { get; set; }

        // MOVIES: monitoring + minimum availability (Radarr V3 "monitor"/"minimumAvailability").
        public MovieMonitorTypes Monitor { get; set; }
        public MovieStatusType MinimumAvailability { get; set; }

        // SERIES: monitoring + availability (Sonarr V5 "shouldMonitor"/"monitorNewItems").
        public MonitorTypes ShouldMonitor { get; set; }
        public NewItemMonitorTypes MonitorNewItems { get; set; }

        // Shared add options.
        public int QualityProfileId { get; set; }
        public string RootFolderPath { get; set; }

        // MOVIES: "searchOnAdd"; SERIES: "searchForMissingEpisodes".
        public bool SearchOnAdd { get; set; }
        public bool SearchForMissingEpisodes { get; set; }

        // SERIES-only.
        public SeriesTypes SeriesType { get; set; }
        public bool SeasonFolder { get; set; }
        public bool TagExisting { get; set; }

        // D5 discriminator: which item pipeline this definition drives. Derived from the
        // provider, not user-editable, so excluded from memberwise equality.
        [MemberwiseEqualityIgnore]
        public MediaType MediaType { get; set; }

        [MemberwiseEqualityIgnore]
        public override bool Enable => Enabled;

        [MemberwiseEqualityIgnore]
        public ImportListStatus Status { get; set; }

        [MemberwiseEqualityIgnore]
        public ImportListType ListType { get; set; }

        [MemberwiseEqualityIgnore]
        public TimeSpan MinRefreshInterval { get; set; }

        public bool Equals(ImportListDefinition other)
        {
            return Comparer.Equals(this, other);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ImportListDefinition);
        }

        public override int GetHashCode()
        {
            return Comparer.GetHashCode(this);
        }
    }
}
