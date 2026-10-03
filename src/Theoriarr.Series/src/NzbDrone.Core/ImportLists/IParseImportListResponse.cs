using System.Collections.Generic;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.ImportLists
{
    // SERIES pipeline parser (canonical name kept from the series tree).
    public interface IParseImportListResponse
    {
        IList<ImportListItemInfo> ParseResponse(ImportListResponse importListResponse);
    }
}
