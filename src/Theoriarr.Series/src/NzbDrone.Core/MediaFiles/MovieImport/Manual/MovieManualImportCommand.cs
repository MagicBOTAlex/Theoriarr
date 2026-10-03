using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.MediaFiles.MovieImport.Manual
{
    // MOVIES manual import command. Renamed from the colliding `ManualImportCommand`
    // so the unified engine can tell it apart from the SERIES manual import command
    // (the API resolves commands by their class name minus the `Command` suffix).
    // The API name is therefore `MovieManualImport`.
    public class MovieManualImportCommand : Command
    {
        public List<ManualImportFile> Files { get; set; }

        public override bool SendUpdatesToClient => true;
        public override bool RequiresDiskAccess => true;

        public ImportMode ImportMode { get; set; }
    }
}
