using System;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.ImportLists
{
    public interface IImportList : IProvider
    {
        // MOVIES semantics; SERIES providers map Enabled from EnableAutomaticAdd.
        bool Enabled { get; }

        // Auto-add newly discovered items (MOVIES semantics; SERIES lists are auto-add).
        bool EnableAuto { get; }

        // D5 discriminator: selects the item pipeline (ImportListItemInfo vs ImportListMovie).
        MediaType MediaType { get; }

        ImportListType ListType { get; }
        TimeSpan MinRefreshInterval { get; }
        ImportListFetchResult Fetch();
    }
}
