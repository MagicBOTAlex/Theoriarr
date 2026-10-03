using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Download.Clients.UTorrent
{
    public class UTorrentSettingsValidator : AbstractValidator<UTorrentSettings>
    {
        public UTorrentSettingsValidator()
        {
            RuleFor(c => c.Host).ValidHost();
            RuleFor(c => c.Port).InclusiveBetween(1, 65535);
            RuleFor(c => c.UrlBase).ValidUrlBase().When(c => c.UrlBase.IsNotNullOrWhiteSpace());
            RuleFor(c => c.TvCategory).NotEmpty();
            RuleFor(c => c.MovieCategory).NotEmpty();
        }
    }

    public class UTorrentSettings : DownloadClientSettingsBase<UTorrentSettings>, IMovieDownloadClientSettings
    {
        private static readonly UTorrentSettingsValidator Validator = new();

        public UTorrentSettings()
        {
            Host = "localhost";
            Port = 8080;
            TvCategory = DownloadClientSettingsDefaults.Category;
        }

        [FieldDefinition(0, Label = "Host", Type = FieldType.Textbox)]
        public string Host { get; set; }

        [FieldDefinition(1, Label = "Port", Type = FieldType.Textbox)]
        public int Port { get; set; }

        [FieldDefinition(2, Label = "UseSsl", Type = FieldType.Checkbox, HelpText = "DownloadClientSettingsUseSslHelpText")]
        [FieldToken(TokenField.HelpText, "UseSsl", "clientName", "uTorrent")]
        public bool UseSsl { get; set; }

        [FieldDefinition(3, Label = "UrlBase", Type = FieldType.Textbox, Advanced = true, HelpText = "DownloadClientSettingsUrlBaseHelpText")]
        [FieldToken(TokenField.HelpText, "UrlBase", "clientName", "uTorrent")]
        [FieldToken(TokenField.HelpText, "UrlBase", "url", "http://[host]:[port]/[urlBase]/api")]
        public string UrlBase { get; set; }

        [FieldDefinition(4, Label = "Username", Type = FieldType.Textbox, Privacy = PrivacyLevel.UserName)]
        public string Username { get; set; }

        [FieldDefinition(5, Label = "Password", Type = FieldType.Password, Privacy = PrivacyLevel.Password)]
        public string Password { get; set; }

        [FieldDefinition(6, Label = "Category", Type = FieldType.Textbox, Section = "Series", HelpText = "DownloadClientSettingsCategoryHelpText")]
        public string TvCategory { get; set; }

        [FieldDefinition(7, Label = "PostImportCategory", Type = FieldType.Textbox, Section = "Series", Advanced = true, HelpText = "DownloadClientSettingsPostImportCategoryHelpText")]
        public string TvImportedCategory { get; set; }

        [FieldDefinition(8, Label = "DownloadClientSettingsRecentPriority", Type = FieldType.Select, SelectOptions = typeof(UTorrentPriority), Section = "Series", HelpText = "DownloadClientSettingsRecentPriorityEpisodeHelpText")]
        public int RecentTvPriority { get; set; }

        [FieldDefinition(9, Label = "DownloadClientSettingsOlderPriority", Type = FieldType.Select, SelectOptions = typeof(UTorrentPriority), Section = "Series", HelpText = "DownloadClientSettingsOlderPriorityEpisodeHelpText")]
        public int OlderTvPriority { get; set; }

        [FieldDefinition(10, Label = "DownloadClientSettingsInitialState", Type = FieldType.Select, SelectOptions = typeof(UTorrentState), HelpText = "DownloadClientSettingsInitialStateHelpText")]
        [FieldToken(TokenField.HelpText, "DownloadClientSettingsInitialState", "clientName", "uTorrent")]
        public int IntialState { get; set; }

        [FieldDefinition(11, Label = "Category", Type = FieldType.Textbox, Section = "Movies", Hidden = HiddenType.Hidden, HelpText = "DownloadClientSettingsCategoryHelpText")]
        public string MovieCategory { get; set; }

        [FieldDefinition(12, Label = "PostImportCategory", Type = FieldType.Textbox, Section = "Movies", Hidden = HiddenType.Hidden, Advanced = true, HelpText = "DownloadClientSettingsPostImportCategoryHelpText")]
        public string MovieImportedCategory { get; set; }

        [FieldDefinition(13, Label = "DownloadClientSettingsRecentPriority", Type = FieldType.Select, SelectOptions = typeof(UTorrentPriority), Section = "Movies", HelpText = "DownloadClientSettingsRecentPriorityMovieHelpText")]
        public int RecentMoviePriority { get; set; }

        [FieldDefinition(14, Label = "DownloadClientSettingsOlderPriority", Type = FieldType.Select, SelectOptions = typeof(UTorrentPriority), Section = "Movies", HelpText = "DownloadClientSettingsOlderPriorityMovieHelpText")]
        public int OlderMoviePriority { get; set; }

        string IDownloadClientCategorySettings.SeriesCategory => TvCategory;

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
