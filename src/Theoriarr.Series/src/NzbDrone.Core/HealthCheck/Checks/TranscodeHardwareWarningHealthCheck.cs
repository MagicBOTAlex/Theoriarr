using System.Linq;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MediaFiles.Transcoding;

namespace NzbDrone.Core.HealthCheck.Checks
{
    public class TranscodeHardwareWarningHealthCheck : HealthCheckBase
    {
        private readonly IGpuCapabilityService _gpuCapabilityService;

        public TranscodeHardwareWarningHealthCheck(IGpuCapabilityService gpuCapabilityService, ILocalizationService localizationService)
            : base(localizationService)
        {
            _gpuCapabilityService = gpuCapabilityService;
        }

        public override HealthCheck Check()
        {
            var hasEnabledHardware = _gpuCapabilityService.GetDevices()
                .Any(device => device.Kind != TranscodeDeviceKind.Software && device.Enabled);

            if (hasEnabledHardware)
            {
                return new HealthCheck(GetType(),
                    HealthCheckResult.Notice,
                    HealthCheckReason.TranscodeHardwareEnabled,
                    _localizationService.GetLocalizedString("TranscodeHardwareEnabledHealthCheckMessage"),
                    "#hardware-transcoding-is-enabled-by-default");
            }

            return new HealthCheck(GetType());
        }
    }
}
