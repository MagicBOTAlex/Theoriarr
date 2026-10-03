using System;
using System.Collections.Generic;
using NzbDrone.Core.MediaFiles.Transcoding;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.MediaCompression
{
    public class MediaCompressionCapabilitiesResource : RestResource
    {
        public string Fingerprint { get; set; }
        public DateTime? LastProbed { get; set; }
        public string FfmpegPath { get; set; }
        public string FfmpegVersion { get; set; }
        public List<TranscodeDeviceResource> Devices { get; set; }
    }

    public static class MediaCompressionCapabilitiesResourceMapper
    {
        public static MediaCompressionCapabilitiesResource ToResource(this MediaCompressionCapabilities model)
        {
            if (model == null)
            {
                return null;
            }

            return new MediaCompressionCapabilitiesResource
            {
                Fingerprint = model.Fingerprint,
                LastProbed = model.LastProbed,
                FfmpegPath = model.FFmpegPath,
                FfmpegVersion = model.FFmpegVersion,
                Devices = model.Devices.ToResource()
            };
        }
    }
}
