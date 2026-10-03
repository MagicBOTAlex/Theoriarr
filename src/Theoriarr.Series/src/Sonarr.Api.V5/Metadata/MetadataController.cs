using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Extras.Metadata;
using NzbDrone.SignalR;
using Sonarr.Api.V5.Provider;
using Sonarr.Http;
using Sonarr.Http.ClientSchema;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V5.Metadata;

[V5ApiController]
[AppSubsystem(AppSubsystem.Series)]
public class MetadataController : ProviderControllerBase<MetadataResource, MetadataBulkResource, IMetadata, MetadataDefinition>
{
    public static readonly MetadataResourceMapper ResourceMapper = new();
    public static readonly MetadataBulkResourceMapper BulkResourceMapper = new();

    public MetadataController(IBroadcastSignalRMessage signalRBroadcaster, IMetadataFactory metadataFactory)
        : base(signalRBroadcaster, metadataFactory, "metadata", ResourceMapper, BulkResourceMapper)
    {
    }

    protected override bool IsFieldVisible(Field field, AppSubsystem subsystem)
    {
        return ProviderFieldScoping.IsFieldVisible(field.Name, subsystem, ProviderFieldScoping.MetadataFields.SeriesOnly, ProviderFieldScoping.MetadataFields.MovieOnly);
    }

    [NonAction]
    public override Results<Ok<IEnumerable<MetadataResource>>, BadRequest> UpdateProvider([FromBody] MetadataBulkResource providerResource)
    {
        throw new NotImplementedException();
    }

    [NonAction]
    public override NoContent DeleteProviders([FromBody] MetadataBulkResource resource)
    {
        throw new NotImplementedException();
    }
}
