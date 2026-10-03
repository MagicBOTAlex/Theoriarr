using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MediaFiles.Transcoding;

namespace NzbDrone.Core.HealthCheck.Checks
{
    public class TranscodeDeviceHealthCheck : HealthCheckBase
    {
        private readonly IGpuCapabilityService _gpuCapabilityService;

        public TranscodeDeviceHealthCheck(IGpuCapabilityService gpuCapabilityService, ILocalizationService localizationService)
            : base(localizationService)
        {
            _gpuCapabilityService = gpuCapabilityService;
        }

        public override HealthCheck Check()
        {
            var broken = _gpuCapabilityService.GetDevices()
                .FirstOrDefault(device => device.Enabled && (!device.Supported || device.Unavailable));

            if (broken == null)
            {
                return new HealthCheck(GetType());
            }

            var reason = broken.Unavailable ? "device no longer detected" : "no validated encoder";

            return new HealthCheck(GetType(),
                HealthCheckResult.Error,
                HealthCheckReason.TranscodeDeviceUnavailable,
                _localizationService.GetLocalizedString("TranscodeDeviceUnavailableHealthCheckMessage", new Dictionary<string, object>
                {
                    { "device", broken.Name.IsNullOrWhiteSpace() ? broken.Id : broken.Name },
                    { "reason", reason }
                }),
                "#transcode-device-unavailable");
        }
    }
}
