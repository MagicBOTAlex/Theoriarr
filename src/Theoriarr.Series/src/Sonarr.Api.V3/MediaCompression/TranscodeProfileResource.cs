using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.MediaFiles.Transcoding;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.MediaCompression
{
    public class TranscodeProfileResource : RestResource
    {
        public string Name { get; set; }
        public string Codec { get; set; }
        public string Mode { get; set; }
        public int QualityValue { get; set; }
        public string Preset { get; set; }
        public int? TargetSizeMB { get; set; }
        public int? TargetPercent { get; set; }
        public int? MaxHeight { get; set; }
        public string Tag { get; set; }
        public bool PreferEnglishAudio { get; set; }
        public string DeviceId { get; set; }
        public string Container { get; set; }
        public bool IsDefault { get; set; }
    }

    public static class TranscodeProfileResourceMapper
    {
        public static TranscodeProfileResource ToResource(this TranscodeProfile model)
        {
            if (model == null)
            {
                return null;
            }

            return new TranscodeProfileResource
            {
                Id = model.Id,
                Name = model.Name,
                Codec = model.Codec,
                Mode = model.Mode.ToString(),
                QualityValue = model.QualityValue,
                Preset = model.Preset,
                TargetSizeMB = model.TargetSizeMB,
                TargetPercent = model.TargetPercent,
                MaxHeight = model.MaxHeight,
                Tag = model.Tag,
                PreferEnglishAudio = model.PreferEnglishAudio,
                DeviceId = model.DeviceId,
                Container = model.Container,
                IsDefault = model.IsDefault
            };
        }

        public static List<TranscodeProfileResource> ToResource(this IEnumerable<TranscodeProfile> models)
        {
            return models.Select(ToResource).ToList();
        }

        public static TranscodeProfile ToModel(this TranscodeProfileResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new TranscodeProfile
            {
                Id = resource.Id,
                Name = resource.Name,
                Codec = resource.Codec,
                Mode = ParseMode(resource.Mode),
                QualityValue = resource.QualityValue,
                Preset = resource.Preset,
                TargetSizeMB = resource.TargetSizeMB,
                TargetPercent = resource.TargetPercent,
                MaxHeight = resource.MaxHeight,
                Tag = resource.Tag,
                PreferEnglishAudio = resource.PreferEnglishAudio,
                DeviceId = resource.DeviceId,
                Container = resource.Container,
                IsDefault = resource.IsDefault
            };
        }

        public static TranscodeMode ParseMode(string mode)
        {
            // Tolerant by design: a persisted/omitted profile mode falls back to Quality. The
            // one-off job request path instead calls TryParseMode and rejects an unknown value.
            return TryParseMode(mode, out var parsed) ? parsed : TranscodeMode.Quality;
        }

        public static bool TryParseMode(string mode, out TranscodeMode parsed)
        {
            switch (mode?.Trim().ToLowerInvariant())
            {
                case "quality":
                    parsed = TranscodeMode.Quality;
                    return true;
                case "targetsize":
                    parsed = TranscodeMode.TargetSize;
                    return true;
                case "percentagereduction":
                    parsed = TranscodeMode.PercentageReduction;
                    return true;
                case "remux":
                    parsed = TranscodeMode.Remux;
                    return true;
                default:
                    parsed = TranscodeMode.Quality;
                    return false;
            }
        }
    }
}
