namespace Sonarr.Api.V5.DiskSpace;

public class DiskSpaceContentResource
{
    public string? Path { get; set; }
    public string? Name { get; set; }
    public bool IsFile { get; set; }
    public long Size { get; set; }
}

public static class DiskSpaceContentResourceMapper
{
    public static DiskSpaceContentResource MapToResource(this NzbDrone.Core.DiskSpace.DiskSpaceContent model)
    {
        return new DiskSpaceContentResource
        {
            Path = model.Path,
            Name = model.Name,
            IsFile = model.IsFile,
            Size = model.Size
        };
    }
}
