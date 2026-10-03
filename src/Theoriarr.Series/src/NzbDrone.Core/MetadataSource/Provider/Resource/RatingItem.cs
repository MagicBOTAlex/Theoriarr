namespace NzbDrone.Core.MetadataSource.Provider.Resource
{
    public class RatingItem
    {
        public int Count { get; set; }
        public decimal Value { get; set; }
        public string Origin { get; set; }
        public string Type { get; set; }
    }
}
