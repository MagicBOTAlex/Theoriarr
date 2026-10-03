using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using NLog;
using NzbDrone.Common.Processes;

namespace NzbDrone.Common.EnvironmentInfo
{
    public class RuntimeInfo : IRuntimeInfo
    {
        private readonly Logger _logger;
        private readonly IOsInfo _osInfo;
        private readonly DateTime _startTime = DateTime.UtcNow;

        public RuntimeInfo(Logger logger, IOsInfo osInfo, IHostLifetime hostLifetime = null)
        {
            _logger = logger;
            _osInfo = osInfo;

            IsWindowsService = hostLifetime is WindowsServiceLifetime;
            IsStarting = true;

            // net6.0 will return Sonarr.dll for entry assembly, we need the actual
            // executable name (Sonarr on linux).  On mono this will return the location of
            // the mono executable itself, which is not what we want.
            var entry = Process.GetCurrentProcess().MainModule;

            if (entry != null)
            {
                ExecutingApplication = entry.FileName;
                IsWindowsTray = OsInfo.IsWindows && entry.ModuleName == $"{ProcessProvider.SONARR_PROCESS_NAME}.exe";
            }
        }

        static RuntimeInfo()
        {
            var officialBuild = InternalIsOfficialBuild();

            // An build running inside of the testing environment. (Analytics disabled)
            IsTesting = InternalIsTesting();

            // An official build running outside of the testing environment. (Analytics configurable)
            IsProduction = !IsTesting && officialBuild;

            // An unofficial build running outside of the testing environment. (Analytics enabled)
            IsDevelopment = !IsTesting && !officialBuild && !InternalIsDebug();
        }

        public DateTime StartTime
        {
            get
            {
                return _startTime;
            }
        }

        public static bool IsUserInteractive => Environment.UserInteractive;

        bool IRuntimeInfo.IsUserInteractive => IsUserInteractive;

        public bool IsAdmin
        {
            get
            {
                if (OsInfo.IsNotWindows)
                {
                    return false;
                }

                try
                {
                    var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Error checking if the current user is an administrator.");
                    return false;
                }
            }
        }

        public bool IsWindowsService { get; private set; }

        public bool IsContainerized => _osInfo.IsContainerized;

        public bool IsSystemdService
        {
            get
            {
                if (!OsInfo.IsLinux)
                {
                    return false;
                }

                try
                {
                    var invocationId = Environment.GetEnvironmentVariable("INVOCATION_ID");
                    return !string.IsNullOrEmpty(invocationId);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Error checking if system is running under systemd");
                    return false;
                }
            }
        }

        public bool IsStarting { get; set; }
        public bool IsExiting { get; set; }
        public bool IsTray
        {
            get
            {
                if (OsInfo.IsWindows)
                {
                    return IsUserInteractive && Process.GetCurrentProcess().ProcessName.Equals(ProcessProvider.SONARR_PROCESS_NAME, StringComparison.InvariantCultureIgnoreCase);
                }

                return false;
            }
        }

        public RuntimeMode Mode
        {
            get
            {
                if (IsWindowsService)
                {
                    return RuntimeMode.Service;
                }

                if (IsTray)
                {
                    return RuntimeMode.Tray;
                }

                return RuntimeMode.Console;
            }
        }

        public bool RestartPending { get; set; }
        public string ExecutingApplication { get; }

        public static bool IsTesting { get; }
        public static bool IsProduction { get; }
        public static bool IsDevelopment { get; }

        private static bool InternalIsTesting()
        {
            try
            {
                var lowerProcessName = Process.GetCurrentProcess().ProcessName.ToLower();

                if (lowerProcessName.Contains("vshost"))
                {
                    return true;
                }

                if (lowerProcessName.Contains("nunit"))
                {
                    return true;
                }

                if (lowerProcessName.Contains("jetbrain"))
                {
                    return true;
                }

                if (lowerProcessName.Contains("resharper"))
                {
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                // Test assemblies are emitted to _tests (see Directory.Build.props).
                // The app itself runs from _output in BOTH development and shipped
                // builds, so _output is deliberately NOT treated as testing here;
                // otherwise release/Docker artifacts could never be production.
                var currentAssemblyLocation = typeof(RuntimeInfo).Assembly.Location;
                if (currentAssemblyLocation.ToLower().Contains("_tests"))
                {
                    return true;
                }
            }
            catch
            {
            }

            var lowerCurrentDir = Directory.GetCurrentDirectory().ToLower();
            if (lowerCurrentDir.Contains("vsts"))
            {
                return true;
            }

            if (lowerCurrentDir.Contains("buildagent"))
            {
                return true;
            }

            if (lowerCurrentDir.Contains("_tests"))
            {
                return true;
            }

            return false;
        }

        private static bool InternalIsDebug()
        {
            if (BuildInfo.IsDebug || Debugger.IsAttached)
            {
                return true;
            }

            return false;
        }

        private static bool InternalIsOfficialBuild()
        {
            // Shipped builds are stamped by the packaging script with a plain
            // "<Configuration>" AssemblyConfiguration; developer and test builds
            // keep the "<Configuration>-dev" suffix (see Directory.Build.props).
            // The historic "Version.Major >= 10 || Revision > 10000" heuristic is
            // not usable here: Theoriarr's own AssemblyVersion major is 10, which
            // would classify every build (including releases) as unofficial.
            if (BuildInfo.Branch != null &&
                BuildInfo.Branch.EndsWith("-dev", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        public bool IsWindowsTray { get; private set; }
    }
}
