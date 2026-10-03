using System.Collections.Generic;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MediaFiles.Transcoding;

namespace NzbDrone.Core.HealthCheck.Checks
{
    public class FFmpegMissingHealthCheck : HealthCheckBase
    {
        private readonly IFFmpegProvider _ffmpegProvider;

        public FFmpegMissingHealthCheck(IFFmpegProvider ffmpegProvider, ILocalizationService localizationService)
            : base(localizationService)
        {
            _ffmpegProvider = ffmpegProvider;
        }

        public override HealthCheck Check()
        {
            var path = _ffmpegProvider.GetFFmpegPath();

            if (path.IsNullOrWhiteSpace())
            {
                return new HealthCheck(GetType(),
                    HealthCheckResult.Error,
                    HealthCheckReason.FFmpegMissing,
                    _localizationService.GetLocalizedString("FFmpegMissingHealthCheckMessage"),
                    "#ffmpeg-not-found");
            }

            if (_ffmpegProvider.GetVersion(path).IsNullOrWhiteSpace())
            {
                return new HealthCheck(GetType(),
                    HealthCheckResult.Error,
                    HealthCheckReason.FFmpegNotExecutable,
                    _localizationService.GetLocalizedString("FFmpegNotExecutableHealthCheckMessage", new Dictionary<string, object>
                    {
                        { "path", path }
                    }),
                    "#ffmpeg-not-found");
            }

            return new HealthCheck(GetType());
        }
    }
}
