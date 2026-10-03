using System;
using System.Reflection;
using NzbDrone.Common.EnvironmentInfo;

namespace Sonarr.Http.Subsystem
{
    public interface ISubsystemInfo
    {
        string GetAppName(AppSubsystem subsystem);
        Version GetVersion(AppSubsystem subsystem);
        DateTime GetBuildTime(AppSubsystem subsystem);
        string GetRuntimeName(AppSubsystem subsystem);
        Version GetRuntimeVersion(AppSubsystem subsystem);
    }

    public class SubsystemInfo : ISubsystemInfo
    {
        // Contract with the Core workstream: NzbDrone.Common.EnvironmentInfo.MovieBuildInfo
        // exposes public static AppName/Version/BuildDateTime properties for the movie domain.
        // Reflection is kept so this layer does not take a hard dependency on one build shape;
        // it falls back to the literal app name and the unified assembly version if absent.
        private const string MovieBuildInfoTypeName = "NzbDrone.Common.EnvironmentInfo.MovieBuildInfo";

        private static readonly Type MovieBuildInfoType =
            typeof(BuildInfo).Assembly.GetType(MovieBuildInfoTypeName);

        public string GetAppName(AppSubsystem subsystem)
        {
            return subsystem == AppSubsystem.Movies
                ? GetMovieValue<string>("AppName") ?? AppSubsystemExtensions.MoviesAppName
                : AppSubsystemExtensions.SeriesAppName;
        }

        public Version GetVersion(AppSubsystem subsystem)
        {
            return subsystem == AppSubsystem.Movies
                ? GetMovieValue<Version>("Version") ?? BuildInfo.Version
                : BuildInfo.Version;
        }

        public DateTime GetBuildTime(AppSubsystem subsystem)
        {
            return subsystem == AppSubsystem.Movies
                ? GetMovieValue<DateTime?>("BuildDateTime") ?? BuildInfo.BuildDateTime
                : BuildInfo.BuildDateTime;
        }

        public string GetRuntimeName(AppSubsystem subsystem)
        {
            return PlatformInfo.PlatformName;
        }

        public Version GetRuntimeVersion(AppSubsystem subsystem)
        {
            return PlatformInfo.GetVersion();
        }

        private static T GetMovieValue<T>(string propertyName)
        {
            var property = MovieBuildInfoType?.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);

            if (property == null)
            {
                return default;
            }

            return (T)property.GetValue(null);
        }
    }
}
