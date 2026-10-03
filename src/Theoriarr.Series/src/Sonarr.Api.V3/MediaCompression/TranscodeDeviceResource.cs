using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.MediaFiles.Transcoding;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.MediaCompression
{
    public class TranscodeDeviceResource : RestResource
    {
        public string DeviceId { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public string Label { get; set; }
        public bool Supported { get; set; }
        public bool Enabled { get; set; }
        public int MaxParallel { get; set; }
        public int Priority { get; set; }
        public int Weight { get; set; }
        public bool Unavailable { get; set; }
        public TranscodeDeviceOptions Options { get; set; }
        public List<TranscodeCodecSetting> Codecs { get; set; }
        public DeviceCapability Capabilities { get; set; }
    }

    public static class TranscodeDeviceResourceMapper
    {
        public static TranscodeDeviceResource ToResource(this TranscodeDevice model)
        {
            if (model == null)
            {
                return null;
            }

            return new TranscodeDeviceResource
            {
                DeviceId = model.Id,
                Kind = model.Kind.ToString(),
                Name = model.Name,
                Label = TranscodeDeviceLabel.Build(model),
                Supported = model.Supported,
                Enabled = model.Enabled,
                MaxParallel = model.MaxParallel,
                Priority = model.Priority,
                Weight = model.Weight,
                Unavailable = model.Unavailable,
                Options = model.Options,
                Codecs = model.Codecs,
                Capabilities = model.Capabilities
            };
        }

        public static List<TranscodeDeviceResource> ToResource(this IEnumerable<TranscodeDevice> models)
        {
            return models.Select(ToResource).ToList();
        }

        public static TranscodeDevice ToModel(this TranscodeDeviceResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new TranscodeDevice
            {
                Id = resource.DeviceId,
                Enabled = resource.Enabled,
                MaxParallel = resource.MaxParallel,
                Priority = resource.Priority,
                Weight = resource.Weight,
                Options = resource.Options,
                Codecs = resource.Codecs
            };
        }

        public static List<TranscodeDevice> ToModel(this IEnumerable<TranscodeDeviceResource> resources)
        {
            return resources.Select(ToModel).ToList();
        }
    }
}
