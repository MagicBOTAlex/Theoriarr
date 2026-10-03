using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace NzbDrone.Core.Notifications.Webhook
{
    // TODO: In v4 this will likely be changed to the default camel case.
    // Union of SERIES and MOVIES event names. Values are not persisted; the
    // converter emits the enum *name*, so SERIES values are kept as-is and the
    // MOVIES-only names are appended.
    [JsonConverter(typeof(StringEnumConverter), converterParameters: typeof(DefaultNamingStrategy))]
    public enum WebhookEventType
    {
        Test,
        Grab,
        Download,
        Rename,
        SeriesAdd,
        SeriesDelete,
        EpisodeFileDelete,
        Health,
        ApplicationUpdate,
        HealthRestored,
        ManualInteractionRequired,
        MovieAdded,
        MovieDelete,
        MovieFileDelete
    }
}
