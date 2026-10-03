namespace Sonarr.Http.Subsystem
{
    public enum AppSubsystem
    {
        Series = 0,
        Movies = 1
    }

    public static class AppSubsystemExtensions
    {
        public const string SeriesAppName = "Sonarr";
        public const string MoviesAppName = "Radarr";

        public static string ToAppName(this AppSubsystem subsystem)
        {
            return subsystem == AppSubsystem.Movies ? MoviesAppName : SeriesAppName;
        }
    }
}
