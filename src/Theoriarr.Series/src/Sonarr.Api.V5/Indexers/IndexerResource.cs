using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using Sonarr.Api.V5.Provider;

namespace Sonarr.Api.V5.Indexers;

public class IndexerResource : ProviderResource<IndexerResource>
{
    public bool EnableRss { get; set; }
    public bool EnableAutomaticSearch { get; set; }
    public bool EnableInteractiveSearch { get; set; }
    public bool SupportsRss { get; set; }
    public bool SupportsSearch { get; set; }
    public DownloadProtocol Protocol { get; set; }
    public int Priority { get; set; }

    // Nullable so a Prowlarr Radarr-shaped update that omits the field does not reset an
    // existing value to 0.
    public int? SeasonSearchMaximumSingleEpisodeAge { get; set; }
    public int DownloadClientId { get; set; }
}

public class IndexerResourceMapper : ProviderResourceMapper<IndexerResource, IndexerDefinition>
{
    public override IndexerResource ToResource(IndexerDefinition definition)
    {
        var resource = base.ToResource(definition);

        resource.EnableRss = definition.EnableRss;
        resource.EnableAutomaticSearch = definition.EnableAutomaticSearch;
        resource.EnableInteractiveSearch = definition.EnableInteractiveSearch;
        resource.SupportsRss = definition.SupportsRss;
        resource.SupportsSearch = definition.SupportsSearch;
        resource.Protocol = definition.Protocol;
        resource.Priority = definition.Priority;
        resource.SeasonSearchMaximumSingleEpisodeAge = definition.SeasonSearchMaximumSingleEpisodeAge;
        resource.DownloadClientId = definition.DownloadClientId;

        return resource;
    }

    public override IndexerDefinition ToModel(IndexerResource resource, IndexerDefinition? existingDefinition)
    {
        var definition = base.ToModel(resource, existingDefinition);

        MergeScopedCategories(resource, definition, existingDefinition);

        definition.EnableRss = resource.EnableRss;
        definition.EnableAutomaticSearch = resource.EnableAutomaticSearch;
        definition.EnableInteractiveSearch = resource.EnableInteractiveSearch;
        definition.Priority = resource.Priority;

        if (resource.SeasonSearchMaximumSingleEpisodeAge.HasValue)
        {
            definition.SeasonSearchMaximumSingleEpisodeAge = resource.SeasonSearchMaximumSingleEpisodeAge.Value;
        }
        else if (existingDefinition != null)
        {
            definition.SeasonSearchMaximumSingleEpisodeAge = existingDefinition.SeasonSearchMaximumSingleEpisodeAge;
        }

        definition.DownloadClientId = resource.DownloadClientId;

        return definition;
    }

    // Prowlarr writes indexers through both its Sonarr and Radarr clients, which each own only one
    // media type but round-trip the same shared `categories` field. Keep the other media type's
    // range so the two syncs converge on a union instead of the last write winning.
    private static void MergeScopedCategories(IndexerResource resource, IndexerDefinition definition, IndexerDefinition? existingDefinition)
    {
        if (definition.Settings is not NewznabSettings settings ||
            existingDefinition?.Settings is not NewznabSettings existingSettings)
        {
            return;
        }

        var hasAnimeCategories = resource.Fields?.Any(f => f.Name.Equals("animeCategories", StringComparison.OrdinalIgnoreCase)) ?? false;
        var hasMovieCategories = resource.Fields?.Any(f => f.Name.Equals("movieCategories", StringComparison.OrdinalIgnoreCase)) ?? false;

        // A write carrying both scoped fields (the unscoped/session API) owns everything; a write
        // without animeCategories is movies-shaped (Prowlarr's Radarr client sends neither scoped
        // field, only the shared one).
        if (hasAnimeCategories && hasMovieCategories)
        {
            return;
        }

        settings.Categories = NewznabSettings.MergeScopedCategories(
            settings.Categories,
            existingSettings.Categories,
            incomingOwnsMovieRange: !hasAnimeCategories);
    }
}
