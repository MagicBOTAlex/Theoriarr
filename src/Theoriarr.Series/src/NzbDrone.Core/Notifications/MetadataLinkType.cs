using System;
using NzbDrone.Core.Annotations;

namespace NzbDrone.Core.Notifications
{
    public enum MetadataLinkType
    {
        [FieldOption(Label = "IMDb")]
        Imdb = 0,

        [FieldOption(Label = "TVDb")]
        Tvdb = 1,

        [FieldOption(Label = "TVMaze")]
        Tvmaze = 2,

        [FieldOption(Label = "Trakt")]
        Trakt = 3,

        [FieldOption(Label = "TMDb")]
        Tmdb = 4
    }

    // D4 persisted-int hook. SERIES values are canonical (D2):
    //   Imdb=0, Tvdb=1, Tvmaze=2, Trakt=3. TMDB is appended as 4.
    // MOVIES persisted Tmdb=0, Imdb=1, Trakt=2, so those rows must be
    // remapped on read. The synthesized provider-settings migration (D9)
    // is expected to invoke MigrateInt for definitions that came from
    // Radarr; this workstream does not author that migration.
    public static class MetadataLinkTypeMigration
    {
        public static MetadataLinkType Migrate(int persistedValue, string source)
        {
            if (string.Equals(source, "Radarr", StringComparison.OrdinalIgnoreCase))
            {
                switch (persistedValue)
                {
                    case 0:
                        return MetadataLinkType.Tmdb;
                    case 1:
                        return MetadataLinkType.Imdb;
                    case 2:
                        return MetadataLinkType.Trakt;
                }
            }

            return (MetadataLinkType)persistedValue;
        }

        public static int MigrateInt(int persistedValue, string source)
        {
            return (int)Migrate(persistedValue, source);
        }
    }
}
