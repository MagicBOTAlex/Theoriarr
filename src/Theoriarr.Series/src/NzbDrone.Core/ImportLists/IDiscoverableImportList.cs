namespace NzbDrone.Core.ImportLists
{
    // D5 capability marker. Replaces ImportListFactory.Discoverable()'s hardcoded
    // typeof(RadarrList.RadarrListImport) / typeof(TMDb.Popular.TMDbPopularImport)
    // so the SERIES base carries no compile dependency on folded movie classes.
    // RadarrListImport and TMDbPopularImport implement this.
    public interface IDiscoverableImportList
    {
    }
}
