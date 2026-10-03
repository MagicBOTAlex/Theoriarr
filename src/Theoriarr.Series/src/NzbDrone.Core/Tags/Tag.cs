using NzbDrone.Core.Datastore;
using NzbDrone.Core.ImportLists;

namespace NzbDrone.Core.Tags
{
    public class Tag : ModelBase
    {
        public string Label { get; set; }

        // D3 discriminating domain. 0 (Series) is also the shared/legacy value handed out
        // by SubsystemDomainScope when the tag is unused or referenced by both domains.
        public MediaType MediaType { get; set; }
    }
}
