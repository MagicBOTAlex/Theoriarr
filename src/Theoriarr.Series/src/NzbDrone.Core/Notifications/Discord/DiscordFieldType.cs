namespace NzbDrone.Core.Notifications.Discord
{
    public enum DiscordGrabFieldType
    {
        Overview,
        Rating,
        Genres,
        Quality,
        Group,
        Size,
        Links,
        Release,
        Poster,
        Fanart,
        Indexer,
        CustomFormats,
        CustomFormatScore,

        // MOVIES-only field, appended to preserve existing SERIES persisted values.
        Tags
    }

    public enum DiscordImportFieldType
    {
        Overview,
        Rating,
        Genres,
        Quality,
        Codecs,
        Group,
        Size,
        Languages,
        Subtitles,
        Links,
        Release,
        Poster,
        Fanart,
        CustomFormats,
        CustomFormatScore,

        // MOVIES-only field, appended to preserve existing SERIES persisted values.
        Tags
    }

    public enum DiscordManualInteractionFieldType
    {
        Overview,
        Rating,
        Genres,
        Quality,
        Group,
        Size,
        Links,
        DownloadTitle,
        Poster,
        Fanart,

        // MOVIES-only field, appended to preserve existing SERIES persisted values.
        Tags
    }
}
