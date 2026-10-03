using System;

namespace Sonarr.Api.V3.Indexers;

public class ReleaseHistoryResource
{
    public DateTime? Grabbed { get; set; }
    public DateTime? Failed { get; set; }
}
