using System.Collections.Generic;
using NzbDrone.Core.ImportLists.ImportListMovies;

namespace NzbDrone.Core.ImportLists
{
    // MOVIES pipeline parser. Kept distinct from IParseImportListResponse so that both
    // item pipelines (D5) can coexist without collapsing ImportListMovie into ImportListItemInfo.
    public interface IParseImportListMovieResponse
    {
        IList<ImportListMovie> ParseResponse(ImportListResponse importListResponse);
    }
}
