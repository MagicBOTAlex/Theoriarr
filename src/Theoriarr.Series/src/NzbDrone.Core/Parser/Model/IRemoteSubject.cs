namespace NzbDrone.Core.Parser.Model
{
    // D7 cross-domain subject contract. Implemented by RemoteMovie and RemoteEpisode so
    // the decision specs, download clients and aggregation services can be written once.
    public interface IRemoteSubject
    {
        ReleaseInfo Release { get; set; }
        ReleaseSourceType ReleaseSource { get; set; }
    }

    public enum ReleaseSourceType
    {
        Unknown = 0,
        Rss = 1,
        Search = 2,
        UserInvokedSearch = 3,
        InteractiveSearch = 4,
        ReleasePush = 5
    }
}
