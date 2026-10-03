using System;
using Equ;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Download.Clients
{
    public abstract class DownloadClientSettingsBase<TSettings> : IProviderConfig, IEquatable<TSettings>, IDownloadClientMediaTypeSettings
        where TSettings : DownloadClientSettingsBase<TSettings>
    {
        private static readonly MemberwiseEqualityComparer<TSettings> Comparer = MemberwiseEqualityComparer<TSettings>.ByProperties;

        // The unified app may use a single download client for every media type. These toggles
        // let a client be limited to specific types; all default on so existing clients keep
        // receiving everything.
        [FieldDefinition(100, Label = "Series", Type = FieldType.Checkbox, Section = "MediaTypes", HelpText = "DownloadClientMediaTypesSeriesHelpText")]
        public bool DownloadSeries { get; set; } = true;

        [FieldDefinition(101, Label = "Movies", Type = FieldType.Checkbox, Section = "MediaTypes", HelpText = "DownloadClientMediaTypesMoviesHelpText")]
        public bool DownloadMovies { get; set; } = true;

        [FieldDefinition(102, Label = "Anime", Type = FieldType.Checkbox, Section = "MediaTypes", HelpText = "DownloadClientMediaTypesAnimeHelpText")]
        public bool DownloadAnime { get; set; } = true;

        public abstract NzbDroneValidationResult Validate();

        public bool Equals(TSettings other)
        {
            return Comparer.Equals(this as TSettings, other);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TSettings);
        }

        public override int GetHashCode()
        {
            return Comparer.GetHashCode(this as TSettings);
        }
    }
}
