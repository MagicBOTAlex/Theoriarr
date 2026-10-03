namespace NzbDrone.SignalR
{
    // Routing hint attached to a SignalRMessage so the broadcaster can deliver it to the
    // connection(s) belonging to the matching application subsystem without relying on the
    // client filtering payloads. Shared messages (command, health, queue, ...) go to both.
    public enum SignalRSubsystem
    {
        Shared = 0,
        Series = 1,
        Movies = 2
    }
}
