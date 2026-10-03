using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.ImportLists;

namespace NzbDrone.Core.RootFolders
{
    public class RootFolder : ModelBase
    {
        public string Path { get; set; }
        public bool Accessible { get; set; }
        public bool IsEmpty { get; set; }
        public long? FreeSpace { get; set; }
        public long? TotalSpace { get; set; }

        // D3 discriminating domain. 0 (Series) is also the shared/legacy value handed out
        // by SubsystemDomainScope when the folder is unused or referenced by both domains.
        public MediaType MediaType { get; set; }

        public List<UnmappedFolder> UnmappedFolders { get; set; }
    }
}
