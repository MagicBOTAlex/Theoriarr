using NzbDrone.Core.Datastore;
using NzbDrone.Core.ImportLists;

namespace NzbDrone.Core.Qualities
{
    public class QualityDefinition : ModelBase
    {
        public Quality Quality { get; set; }

        public string Title { get; set; }

        public string GroupName { get; set; }
        public int Weight { get; set; }

        // D3: sizes are movie-domain columns (SERIES moved them to profile items in 207).
        public double? MinSize { get; set; }
        public double? MaxSize { get; set; }
        public double? PreferredSize { get; set; }

        // D3 superset discriminator.
        public MediaType MediaType { get; set; }

        public QualityDefinition()
        {
        }

        public QualityDefinition(Quality quality)
        {
            Quality = quality;
            Title = quality.Name;
        }

        public override string ToString()
        {
            return Quality.Name;
        }
    }
}
