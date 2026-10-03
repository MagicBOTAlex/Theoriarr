using NzbDrone.Core.Datastore.Events;

namespace NzbDrone.SignalR
{
    public class SignalRMessage
    {
        public object Body { get; set; }
        public string Name { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public ModelAction Action { get; set; }

        public int? Version { get; set; }

        // Server-side routing target. JsonIgnore keeps the wire payload identical to before;
        // the subsystem is implied by which hub/connection the message arrived on.
        [System.Text.Json.Serialization.JsonIgnore]
        public SignalRSubsystem Subsystem { get; set; } = SignalRSubsystem.Shared;
    }
}
