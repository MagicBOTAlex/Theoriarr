using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.MediaFiles.Transcoding;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.MediaCompression
{
    public class TranscodeJobResource : RestResource
    {
        public string MediaType { get; set; }
        public int? EpisodeFileId { get; set; }
        public int? MovieFileId { get; set; }
        public int? SeriesId { get; set; }
        public int? MovieId { get; set; }
        public string SeriesTitle { get; set; }
        public int? SeasonNumber { get; set; }
        public string MovieTitle { get; set; }
        public string SourcePath { get; set; }
        public string OutputPath { get; set; }
        public string Status { get; set; }
        public string Mode { get; set; }
        public string VideoCodec { get; set; }
        public string RateControl { get; set; }
        public string Preset { get; set; }
        public string DeviceId { get; set; }
        public long? TargetSize { get; set; }
        public int? TargetPercent { get; set; }
        public int? QualityValue { get; set; }
        public int? MaxHeight { get; set; }
        public string Tag { get; set; }
        public bool PreferEnglishAudio { get; set; }
        public int? ProfileId { get; set; }
        public string Container { get; set; }
        public int? RequeuedJobId { get; set; }
        public int Priority { get; set; }
        public long? SourceSize { get; set; }
        public long? OutputSize { get; set; }
        public double Progress { get; set; }
        public string Speed { get; set; }
        public string Fps { get; set; }
        public string Eta { get; set; }
        public string Error { get; set; }
        public string Message { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
    }

    public static class TranscodeJobResourceMapper
    {
        public static TranscodeJobResource ToResource(this TranscodeJob model)
        {
            if (model == null)
            {
                return null;
            }

            return new TranscodeJobResource
            {
                Id = model.Id,
                MediaType = model.MediaType.ToString(),
                EpisodeFileId = model.EpisodeFileId,
                MovieFileId = model.MovieFileId,
                SeriesId = model.SeriesId,
                MovieId = model.MovieId,
                SourcePath = model.SourcePath,
                OutputPath = model.OutputPath,
                Status = model.Status.ToString(),
                Mode = model.Mode.ToString(),
                VideoCodec = model.VideoCodec,
                RateControl = model.RateControl,
                Preset = model.Preset,
                DeviceId = model.DeviceId,
                TargetSize = model.TargetSize,
                TargetPercent = model.TargetPercent,
                QualityValue = model.QualityValue,
                MaxHeight = model.MaxHeight,
                Tag = model.Tag,
                PreferEnglishAudio = model.PreferEnglishAudio,
                ProfileId = model.ProfileId,
                Container = model.Container,
                RequeuedJobId = model.RequeuedJobId,
                Priority = model.Priority,
                SourceSize = model.SourceSize,
                OutputSize = model.OutputSize,
                Progress = model.Progress,
                Speed = model.Speed,
                Fps = model.Fps,
                Eta = model.Eta,
                Error = model.Error,
                Message = model.Message,
                StartedAt = model.StartedAt,
                EndedAt = model.EndedAt,
                LastUpdatedAt = model.LastUpdatedAt
            };
        }

        public static List<TranscodeJobResource> ToResource(this IEnumerable<TranscodeJob> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
