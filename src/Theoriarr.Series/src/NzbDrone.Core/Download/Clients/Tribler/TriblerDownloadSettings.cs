using System.Text.RegularExpressions;
using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Download.Clients.Tribler
{
    public class TriblerSettingsValidator : AbstractValidator<TriblerDownloadSettings>
    {
        public TriblerSettingsValidator()
        {
            RuleFor(c => c.Host).ValidHost();
            RuleFor(c => c.Port).InclusiveBetween(1, 65535);
            RuleFor(c => c.UrlBase).ValidUrlBase();
            RuleFor(c => c.ApiKey).NotEmpty();
            RuleFor(c => c.TvCategory).Matches(@"^\.?[-a-z]*$", RegexOptions.IgnoreCase).WithMessage("Allowed characters a-z and -");
            RuleFor(c => c.TvCategory).Empty()
                .When(c => c.TvDirectory.IsNotNullOrWhiteSpace())
                .WithMessage("Cannot use Category and Directory");
            RuleFor(c => c.MovieCategory).Matches(@"^\.?[-a-z]*$", RegexOptions.IgnoreCase).WithMessage("Allowed characters a-z and -");
            RuleFor(c => c.MovieCategory).Empty()
                .When(c => c.MovieDirectory.IsNotNullOrWhiteSpace())
                .WithMessage("Cannot use Category and Directory");
            RuleFor(c => c.AnonymityLevel).GreaterThanOrEqualTo(0);
        }
    }

    public class TriblerDownloadSettings : IProviderConfig, IMovieDownloadClientSettings, IDownloadClientMediaTypeSettings
    {
        private static readonly TriblerSettingsValidator Validator = new TriblerSettingsValidator();

        public TriblerDownloadSettings()
        {
            Host = "localhost";
            Port = 20100;
            UrlBase = "";
            AnonymityLevel = 1;
            SafeSeeding = true;
            TvCategory = DownloadClientSettingsDefaults.Category;
        }

        [FieldDefinition(1, Label = "Host", Type = FieldType.Textbox)]
        public string Host { get; set; }

        [FieldDefinition(2, Label = "Port", Type = FieldType.Textbox)]
        public int Port { get; set; }

        [FieldDefinition(3, Label = "UseSsl", Type = FieldType.Checkbox, HelpText = "DownloadClientSettingsUseSslHelpText")]
        public bool UseSsl { get; set; }

        [FieldDefinition(4, Label = "UrlBase", Type = FieldType.Textbox, Advanced = true, HelpText = "DownloadClientSettingsUrlBaseHelpText")]
        [FieldToken(TokenField.HelpText, "UrlBase", "clientName", "Tribler")]
        [FieldToken(TokenField.HelpText, "UrlBase", "url", "http://[host]:[port]/[urlBase]")]
        public string UrlBase { get; set; }

        [FieldDefinition(5, Label = "ApiKey", Type = FieldType.Textbox, Privacy = PrivacyLevel.ApiKey, HelpText = "DownloadClientTriblerSettingsApiKeyHelpText")]
        public string ApiKey { get; set; }

        [FieldDefinition(6, Label = "Category", Type = FieldType.Textbox, Section = "Series", HelpText = "DownloadClientSettingsCategoryHelpText")]
        public string TvCategory { get; set; }

        [FieldDefinition(7, Label = "Directory", Type = FieldType.Textbox, Section = "Series", Advanced = true, HelpText = "DownloadClientTriblerSettingsDirectoryHelpText")]
        public string TvDirectory { get; set; }

        [FieldDefinition(8, Label = "DownloadClientTriblerSettingsAnonymityLevel", Type = FieldType.Number, HelpText = "DownloadClientTriblerSettingsAnonymityLevelHelpText")]
        [FieldToken(TokenField.HelpText, "DownloadClientTriblerSettingsAnonymityLevel", "url", "https://www.tribler.org/anonymity.html")]
        public int AnonymityLevel { get; set; }

        [FieldDefinition(9, Label = "DownloadClientTriblerSettingsSafeSeeding", Type = FieldType.Checkbox, HelpText = "DownloadClientTriblerSettingsSafeSeedingHelpText")]
        public bool SafeSeeding { get; set; }

        [FieldDefinition(10, Label = "Category", Type = FieldType.Textbox, Section = "Movies", Hidden = HiddenType.Hidden, HelpText = "DownloadClientSettingsCategoryHelpText")]
        public string MovieCategory { get; set; }

        [FieldDefinition(11, Label = "Directory", Type = FieldType.Textbox, Section = "Movies", Advanced = true, HelpText = "DownloadClientTriblerSettingsDirectoryHelpText")]
        public string MovieDirectory { get; set; }

        string IDownloadClientCategorySettings.SeriesCategory => TvCategory;

        [FieldDefinition(100, Label = "Series", Type = FieldType.Checkbox, Section = "MediaTypes", HelpText = "DownloadClientMediaTypesSeriesHelpText")]
        public bool DownloadSeries { get; set; } = true;

        [FieldDefinition(101, Label = "Movies", Type = FieldType.Checkbox, Section = "MediaTypes", HelpText = "DownloadClientMediaTypesMoviesHelpText")]
        public bool DownloadMovies { get; set; } = true;

        [FieldDefinition(102, Label = "Anime", Type = FieldType.Checkbox, Section = "MediaTypes", HelpText = "DownloadClientMediaTypesAnimeHelpText")]
        public bool DownloadAnime { get; set; } = true;

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
