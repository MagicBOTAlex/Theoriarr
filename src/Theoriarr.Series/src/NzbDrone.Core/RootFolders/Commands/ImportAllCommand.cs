using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.RootFolders.Commands
{
    public class ImportAllCommand : Command
    {
        public override bool SendUpdatesToClient => true;
        public override bool RequiresDiskAccess => true;
        public override bool IsLongRunning => true;
    }
}
