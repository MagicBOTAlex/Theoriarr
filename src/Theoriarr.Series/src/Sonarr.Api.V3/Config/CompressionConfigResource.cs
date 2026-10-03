using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Transcoding;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.Config
{
    public class CompressionConfigResource : RestResource
    {
        public bool MediaCompressionEnabled { get; set; }
        public string TranscodeTempFolder { get; set; }
        public int MaxConcurrentJobs { get; set; }
        public string DefaultVideoCodec { get; set; }
        public TranscodeMode DefaultRateControlMode { get; set; }
        public int DefaultTargetEpisodeSizeMB { get; set; }
        public int DefaultTargetMovieSizeMB { get; set; }
        public int DefaultQualityValue { get; set; }
        public string DefaultPreset { get; set; }
        public int DefaultReducePercent { get; set; }
        public TranscodeReviewAction TranscodeReviewDefault { get; set; }
        public bool PreferHardware { get; set; }
        public bool TranscodeEasiestJobsFirst { get; set; }
        public string FfmpegPath { get; set; }
        public int TranscodeNice { get; set; }
    }

    public static class CompressionConfigResourceMapper
    {
        public static CompressionConfigResource ToResource(IConfigService model)
        {
            return new CompressionConfigResource
            {
                MediaCompressionEnabled = model.MediaCompressionEnabled,
                TranscodeTempFolder = model.TranscodeTempFolder,
                MaxConcurrentJobs = model.MaxConcurrentJobs,
                DefaultVideoCodec = model.DefaultVideoCodec,
                DefaultRateControlMode = model.DefaultRateControlMode,
                DefaultTargetEpisodeSizeMB = model.DefaultTargetEpisodeSizeMB,
                DefaultTargetMovieSizeMB = model.DefaultTargetMovieSizeMB,
                DefaultQualityValue = model.DefaultQualityValue,
                DefaultPreset = model.DefaultPreset,
                DefaultReducePercent = model.DefaultReducePercent,
                TranscodeReviewDefault = model.TranscodeReviewDefault,
                PreferHardware = model.PreferHardware,
                TranscodeEasiestJobsFirst = model.TranscodeEasiestJobsFirst,
                FfmpegPath = model.FFmpegPath,
                TranscodeNice = model.TranscodeNice
            };
        }
    }
}
