using System;

namespace NzbDrone.Common.EnvironmentInfo
{
    // MOVIES-domain build metadata. The merged binary ships a single assembly, but
    // Sonarr.Http.Subsystem.SubsystemInfo reflects over this type so the movie key's
    // /system/status reports the Radarr domain (appName) and the movie build path.
    // Version/BuildDateTime intentionally mirror BuildInfo because both domains run the
    // same binary; the indirection keeps the API contract stable if they ever diverge.
    public static class MovieBuildInfo
    {
        public static string AppName { get; } = "Radarr";

        public static Version Version { get; } = BuildInfo.Version;

        public static DateTime BuildDateTime => BuildInfo.BuildDateTime;
    }
}
