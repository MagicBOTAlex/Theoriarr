namespace NzbDrone.Core.DiskSpace
{
    public class DiskSpaceContent
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public bool IsFile { get; set; }
        public long Size { get; set; }
    }
}
