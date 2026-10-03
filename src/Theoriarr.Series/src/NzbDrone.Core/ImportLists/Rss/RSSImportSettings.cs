using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.ImportLists.Rss
{
    public class RSSImportSettingsValidator : AbstractValidator<RSSImportSettings>
    {
        public RSSImportSettingsValidator()
        {
            RuleFor(c => c.Link).ValidRootUrl();
        }
    }

    // Consolidated RSS provider settings (was MOVIES `RSSImport.RSSImportSettings`). Derives
    // from the shared RssImportBaseSettings so the shared request generator is reused; the
    // Radarr V3 `Link` field name is preserved and `Url` proxies it.
    public class RSSImportSettings : RssImportBaseSettings<RSSImportSettings>
    {
        private static readonly RSSImportSettingsValidator Validator = new();

        public RSSImportSettings()
        {
            Link = "https://rss.yoursite.com";
        }

        [FieldDefinition(0, Label = "RSS Link", HelpText = "Link to the rss feed of movies.")]
        public string Link { get; set; }

        public override string Url
        {
            get => Link;
            set => Link = value;
        }

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
