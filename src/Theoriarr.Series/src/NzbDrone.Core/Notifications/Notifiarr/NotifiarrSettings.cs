using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Notifications.Notifiarr
{
    public class NotifiarrSettingsValidator : AbstractValidator<NotifiarrSettings>
    {
        public NotifiarrSettingsValidator()
        {
            RuleFor(c => c).Custom((c, context) =>
            {
                if (string.IsNullOrWhiteSpace(c.ApiKey) && string.IsNullOrWhiteSpace(c.APIKey))
                {
                    context.AddFailure("ApiKey", "An API key is required");
                }
            });
        }
    }

    public class NotifiarrSettings : NotificationSettingsBase<NotifiarrSettings>
    {
        private static readonly NotifiarrSettingsValidator Validator = new();

        [FieldDefinition(0, Label = "ApiKey", Privacy = PrivacyLevel.ApiKey, HelpText = "NotificationsNotifiarrSettingsApiKeyHelpText", HelpLink = "https://notifiarr.com")]
        public string ApiKey { get; set; }

        [FieldDefinition(1, Label = "ApiKey", Privacy = PrivacyLevel.ApiKey, HelpText = "NotificationsNotifiarrSettingsApiKeyHelpText", HelpLink = "https://notifiarr.com")]
        public string APIKey { get; set; }

        public override NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
