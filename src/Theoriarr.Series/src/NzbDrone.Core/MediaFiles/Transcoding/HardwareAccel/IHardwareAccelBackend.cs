using System.Collections.Generic;

namespace NzbDrone.Core.MediaFiles.Transcoding.HardwareAccel
{
    public interface IHardwareAccelBackend
    {
        TranscodeDeviceKind Kind { get; }

        // A cheap, probe-free identity of the environment this backend depends on (used to decide
        // whether a previous capability snapshot is still valid).
        string GetFingerprint();

        // Enumerate devices and validate each encoder with a tiny probe encode.
        List<DeviceCapability> Detect();
    }
}
