using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NzbDrone.Common.EnvironmentInfo
{
    public static class BuildInfo
    {
        static BuildInfo()
        {
            var assembly = Assembly.GetExecutingAssembly();

            Version = assembly.GetName().Version;

            var attributes = assembly.GetCustomAttributes(true);

            Branch = "unknown";

            var config = attributes.OfType<AssemblyConfigurationAttribute>().FirstOrDefault();
            if (config != null)
            {
                Branch = config.Configuration;
            }

            // The assembly configuration is stamped as "<Configuration>-dev" (see
            // Directory.Build.props); expose the plain configuration as well.
            BuildConfiguration = Branch != null && Branch.EndsWith("-dev", StringComparison.OrdinalIgnoreCase)
                ? Branch.Substring(0, Branch.Length - 4)
                : Branch;

            Release = $"{Version}-{Branch}";

            Commit = Metadata(assembly, "GitCommit");
            CommitBranch = Metadata(assembly, "GitBranch");
            CommitDate = Metadata(assembly, "GitCommitDate");
            HasUncommittedChanges = Metadata(assembly, "GitDirty") == "true";
        }

        public static string AppName { get; } = "Theoriarr";

        public static Version Version { get; }
        public static string Branch { get; }
        public static string BuildConfiguration { get; }
        public static string Release { get; }

        /// <summary>
        /// Git commit the build was made from, stamped at build time; null when the build
        /// did not have git available.
        /// </summary>
        public static string Commit { get; }

        public static string CommitBranch { get; }

        /// <summary>ISO-8601 commit date, stamped at build time.</summary>
        public static string CommitDate { get; }

        public static bool HasUncommittedChanges { get; }

        public static DateTime BuildDateTime
        {
            get
            {
                var fileLocation = Assembly.GetCallingAssembly().Location;
                return new FileInfo(fileLocation).LastWriteTimeUtc;
            }
        }

        public static bool IsDebug
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }

        private static string Metadata(Assembly assembly, string key)
        {
            var value = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value;

            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
