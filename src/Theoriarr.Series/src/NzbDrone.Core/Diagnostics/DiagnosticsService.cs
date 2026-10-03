using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Options;
using NLog;
using NzbDrone.Common.Cloud;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Options;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Queue;
using NzbDrone.Core.ThingiProvider;
using HealthCheckModel = NzbDrone.Core.HealthCheck.HealthCheck;
using QueueModel = NzbDrone.Core.Queue.Queue;

namespace NzbDrone.Core.Diagnostics
{
    public interface IDiagnosticsService
    {
        string CreateDiagnosticsFile(string frontendState = null);
        string GetDiagnosticsFile(string fileName);
    }

    /// <summary>
    /// Builds a single, self-contained text file that captures the current state of the
    /// running server: build/runtime status, configuration (secrets redacted), health,
    /// scheduled tasks, the command queue, the download queue, the media-compression
    /// device registry and jobs, the scripts database contents and the log files.
    /// Only reachable from debug builds.
    /// </summary>
    public class DiagnosticsService : IDiagnosticsService
    {
        private const string FilePrefix = "theoriarr-diagnostics_";
        private const string Redacted = "********";
        private const int SummaryTopCount = 30;

        // Serialises dump creation: two concurrent requests would otherwise have one
        // DeletePreviousFiles() remove the other's in-progress .body file.
        private static readonly object CreateLock = new();

        private static readonly HashSet<string> RedactedConfigFileKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "ApiKey",
            "MovieApiKey",
            "OidcClientSecret",
            "SslCertPassword",
            "PostgresPassword",
            "Password",
            "PasswordConfirmation",
            "ProxyPassword",
            "RijndaelPassphrase",
            "HmacPassphrase",
            "RijndaelSalt",
            "HmacSalt"
        };

        private static readonly HashSet<string> RedactedDatabaseConfigKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "RijndaelPassphrase",
            "HmacPassphrase",
            "RijndaelSalt",
            "HmacSalt",
            "ProxyPassword"
        };

        // Provider rows (Indexers/DownloadClients/Notifications/ImportLists) keep their
        // credentials inside a Settings JSON blob. A key is masked when its name looks like a
        // credential, so both current and future fields are covered.
        private static readonly HashSet<string> RedactedJsonKeyTokens = new(StringComparer.OrdinalIgnoreCase)
        {
            "apikey",
            "password",
            "passwd",
            "pwd",
            "secret",
            "clientsecret",
            "token",
            "accesstoken",
            "refreshtoken",
            "passphrase",
            "rsskey"
        };

        // Matches "key = value" / "key: value" credential assignments in free text (command,
        // health and queue messages/exceptions). Quoted and unquoted values are supported.
        private static readonly Regex CredentialTextRegex = new(
            @"(?<key>(?i:\b(?:api[_-]?key|apikey|password|passwd|pwd|secret|token|access[_-]?token|refresh[_-]?token|client[_-]?secret|passphrase|rsskey)\b)\s*[:=]\s*)(?<value>""[^""]*""|'[^']*'|[^\s,;&""']+)",
            RegexOptions.Compiled);

        private static readonly Regex BearerTokenRegex = new(
            @"(?i)\bBearer\s+[A-Za-z0-9\-._~+/]+=*",
            RegexOptions.Compiled);

        private static readonly Regex UrlCredentialRegex = new(
            @"(?<scheme>(?i:[a-z][a-z0-9+.\-]*://[^/\s:@]+:))(?<password>[^@/\s]+)(?=@)",
            RegexOptions.Compiled);

        private static readonly JsonSerializerOptions RowSerializerOptions = new()
        {
            WriteIndented = false
        };

        private static readonly JsonSerializerOptions IndentedSerializerOptions = new()
        {
            WriteIndented = true
        };

        private static readonly Regex SectionLineRegex = new(@"^={10,} (.+?) ={10,}$", RegexOptions.Compiled);

        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IDiskProvider _diskProvider;
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IRuntimeInfo _runtimeInfo;
        private readonly IOsInfo _osInfo;
        private readonly IMainDatabase _mainDatabase;
        private readonly ILogDatabase _logDatabase;
        private readonly IHealthCheckService _healthCheckService;
        private readonly ITaskManager _taskManager;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IQueueService _queueService;
        private readonly IDeploymentInfoProvider _deploymentInfoProvider;
        private readonly IConfigService _configService;
        private readonly ITranscodeJobRepository _transcodeJobRepository;
        private readonly ITranscodeProfileRepository _transcodeProfileRepository;
        private readonly IOptions<MetadataOptions> _metadataOptions;
        private readonly IRadarrCloudRequestBuilder _radarrCloudRequestBuilder;
        private readonly ISonarrCloudRequestBuilder _sonarrCloudRequestBuilder;
        private readonly IHttpClient _httpClient;

        public DiagnosticsService(IAppFolderInfo appFolderInfo,
                                  IDiskProvider diskProvider,
                                  IConfigFileProvider configFileProvider,
                                  IRuntimeInfo runtimeInfo,
                                  IOsInfo osInfo,
                                  IMainDatabase mainDatabase,
                                  ILogDatabase logDatabase,
                                  IHealthCheckService healthCheckService,
                                  ITaskManager taskManager,
                                  IManageCommandQueue commandQueueManager,
                                  IQueueService queueService,
                                  IDeploymentInfoProvider deploymentInfoProvider,
                                  IConfigService configService,
                                  ITranscodeJobRepository transcodeJobRepository,
                                  ITranscodeProfileRepository transcodeProfileRepository,
                                  IOptions<MetadataOptions> metadataOptions,
                                  IRadarrCloudRequestBuilder radarrCloudRequestBuilder,
                                  ISonarrCloudRequestBuilder sonarrCloudRequestBuilder,
                                  IHttpClient httpClient)
        {
            _appFolderInfo = appFolderInfo;
            _diskProvider = diskProvider;
            _configFileProvider = configFileProvider;
            _runtimeInfo = runtimeInfo;
            _osInfo = osInfo;
            _mainDatabase = mainDatabase;
            _logDatabase = logDatabase;
            _healthCheckService = healthCheckService;
            _taskManager = taskManager;
            _commandQueueManager = commandQueueManager;
            _queueService = queueService;
            _deploymentInfoProvider = deploymentInfoProvider;
            _configService = configService;
            _transcodeJobRepository = transcodeJobRepository;
            _transcodeProfileRepository = transcodeProfileRepository;
            _metadataOptions = metadataOptions;
            _radarrCloudRequestBuilder = radarrCloudRequestBuilder;
            _sonarrCloudRequestBuilder = sonarrCloudRequestBuilder;
            _httpClient = httpClient;
        }

        public string CreateDiagnosticsFile(string frontendState = null)
        {
            lock (CreateLock)
            {
                return CreateDiagnosticsFileInternal(frontendState);
            }
        }

        private string CreateDiagnosticsFileInternal(string frontendState)
        {
            var tempFolder = _appFolderInfo.TempFolder;
            _diskProvider.EnsureFolder(tempFolder);

            DeletePreviousFiles(tempFolder);

            var fileName = $"{FilePrefix}{BuildInfo.Version}_{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
            var path = Path.Combine(tempFolder, fileName);

            // The body is written first so the section index at the top can carry the
            // final line numbers (the index itself shifts everything below it).
            var bodyPath = path + $".{Guid.NewGuid():N}.body";

            using (var stream = _diskProvider.OpenWriteStream(bodyPath))
            using (var writer = new StreamWriter(stream))
            {
                WriteSystemStatus(writer);
                WriteStorage(writer);
                WriteCounts(writer);
                WriteLogSummary(writer);
                WriteFrontend(writer, frontendState);
                WriteConfiguration(writer);
                WriteMetadataProvider(writer);
                WriteUpstreamIntegrations(writer);
                WriteLogging(writer);
                WriteHealth(writer);
                WriteTasks(writer);
                WriteCommands(writer);
                WriteQueue(writer);
                WriteMediaCompression(writer);
                WriteDatabases(writer);
                WriteLogFiles(writer);
            }

            var sections = IndexSections(bodyPath);
            var prefixLines = CountLines(BuildPrefix(sections, 0));

            using (var stream = _diskProvider.OpenWriteStream(path))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(BuildPrefix(sections, prefixLines));
                writer.Flush();

                using (var body = _diskProvider.OpenReadStream(bodyPath))
                {
                    body.CopyTo(stream);
                }
            }

            _diskProvider.DeleteFile(bodyPath);

            return path;
        }

        public string GetDiagnosticsFile(string fileName)
        {
            if (fileName.IsNullOrWhiteSpace() ||
                Path.GetFileName(fileName) != fileName ||
                !fileName.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase) ||
                !fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var path = Path.Combine(_appFolderInfo.TempFolder, fileName);

            return _diskProvider.FileExists(path) ? path : null;
        }

        private void DeletePreviousFiles(string tempFolder)
        {
            try
            {
                var previous = _diskProvider.GetFiles(tempFolder, false)
                    .Where(f => Path.GetFileName(f).StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase));

                foreach (var file in previous)
                {
                    _diskProvider.DeleteFile(file);
                }
            }
            catch (Exception)
            {
                // A stale dump is not worth failing the request over.
            }
        }

        private static void WriteSectionHeader(TextWriter writer, string title)
        {
            writer.WriteLine();
            writer.WriteLine(BuildSectionLine(title));
        }

        private static string BuildSectionLine(string title)
        {
            const int width = 80;
            var label = $" {title} ";
            var remaining = Math.Max(width - label.Length, 20);
            var left = remaining / 2;

            return new string('=', left) + label + new string('=', remaining - left);
        }

        private static void WriteKeyValue(TextWriter writer, string key, object value)
        {
            writer.WriteLine($"{key,-26}: {value ?? "<null>"}");
        }

        private static void WriteJson(TextWriter writer, object value)
        {
            writer.WriteLine(STJson.ToJson(value));
        }

        private void WriteHeader(TextWriter writer)
        {
            writer.WriteLine(BuildSectionLine("THEORIARR DIAGNOSTICS DUMP"));
            WriteKeyValue(writer, "Generated (UTC)", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            WriteKeyValue(writer, "App Version", BuildInfo.Version);
            WriteKeyValue(writer, "Build Config", BuildInfo.BuildConfiguration);
            WriteKeyValue(writer, "Revision", FormatRevision());
            WriteKeyValue(writer, "Instance", _configFileProvider.InstanceName);
        }

        private static string FormatRevision()
        {
            if (BuildInfo.Commit.IsNullOrWhiteSpace())
            {
                return "unknown";
            }

            var revision = BuildInfo.Commit;

            if (BuildInfo.CommitBranch.IsNotNullOrWhiteSpace())
            {
                revision += $" ({BuildInfo.CommitBranch})";
            }

            if (BuildInfo.HasUncommittedChanges)
            {
                revision += " + uncommitted changes";
            }

            return revision;
        }

        private void WriteSystemStatus(TextWriter writer)
        {
            WriteSectionHeader(writer, "SYSTEM STATUS");

            WriteKeyValue(writer, "App Name", BuildInfo.AppName);
            WriteKeyValue(writer, "App Version", BuildInfo.Version);
            WriteKeyValue(writer, "Build Config", BuildInfo.BuildConfiguration);
            WriteKeyValue(writer, "Release", BuildInfo.Release);
            WriteKeyValue(writer, "Git Commit", BuildInfo.Commit ?? "unknown");
            WriteKeyValue(writer, "Git Branch", BuildInfo.CommitBranch ?? "unknown");
            WriteKeyValue(writer, "Git Commit Date", BuildInfo.CommitDate ?? "unknown");
            WriteKeyValue(writer, "Git Dirty", BuildInfo.HasUncommittedChanges);
            WriteKeyValue(writer, "Build Date", BuildInfo.BuildDateTime);
            WriteKeyValue(writer, "Debug Build", BuildInfo.IsDebug);
            WriteKeyValue(writer, "OS", $"{_osInfo.Name} {_osInfo.Version}");
            WriteKeyValue(writer, "Runtime", RuntimeInformation.FrameworkDescription);
            WriteKeyValue(writer, "Startup Folder", _appFolderInfo.StartUpFolder);
            WriteKeyValue(writer, "App Data Folder", _appFolderInfo.GetAppDataPath());
            WriteKeyValue(writer, "Config File", _appFolderInfo.GetConfigPath());
            WriteKeyValue(writer, "Temp Folder", _appFolderInfo.TempFolder);
            WriteKeyValue(writer, "Log Folder", _appFolderInfo.GetLogFolder());
            WriteKeyValue(writer, "Log DB Enabled", _configFileProvider.LogDbEnabled);
            WriteKeyValue(writer, "ffprobe", FindBundledBinary("ffprobe"));
            WriteKeyValue(writer, "Start Time", _runtimeInfo.StartTime);
            WriteKeyValue(writer, "Mode", _runtimeInfo.Mode);
            WriteKeyValue(writer, "Admin", _runtimeInfo.IsAdmin);
            WriteKeyValue(writer, "Containerized", _osInfo.IsContainerized);
            WriteKeyValue(writer, "Docker", _osInfo.IsDocker);
            WriteKeyValue(writer, "Database Type", _mainDatabase.DatabaseType);
            WriteKeyValue(writer, "Database Version", _mainDatabase.Version);
            WriteKeyValue(writer, "Database Migration", _mainDatabase.Migration);
            WriteKeyValue(writer, "Listen Port", _configFileProvider.Port);
            WriteKeyValue(writer, "Url Base", _configFileProvider.UrlBase);
            WriteKeyValue(writer, "Authentication", _configFileProvider.AuthenticationMethod);
            WriteKeyValue(writer, "Package Version", _deploymentInfoProvider.PackageVersion);
            WriteKeyValue(writer, "Package Author", _deploymentInfoProvider.PackageAuthor);
            WriteKeyValue(writer, "Update Mechanism", _deploymentInfoProvider.PackageUpdateMechanism);
        }

        private string FindBundledBinary(string name)
        {
            var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new[] { $"{name}.exe", name }
                : new[] { name, $"{name}.exe" };

            foreach (var candidate in candidates)
            {
                var path = Path.Combine(_appFolderInfo.StartUpFolder, candidate);

                if (_diskProvider.FileExists(path))
                {
                    return path;
                }
            }

            return "not found";
        }

        private void WriteStorage(TextWriter writer)
        {
            WriteSectionHeader(writer, "STORAGE / DISK USAGE");
            writer.WriteLine(" Path                                              Total        Free    Used   Kind");

            var paths = new List<(string Path, string Kind)>();

            try
            {
                using var connection = _mainDatabase.OpenConnection();

                foreach (var folder in QueryRows(connection, "SELECT \"Path\" AS Path, \"MediaType\" AS MediaType FROM \"RootFolders\" ORDER BY \"Path\""))
                {
                    paths.Add((GetString(folder, "Path"), $"RootFolder ({DescribeMediaType(GetString(folder, "MediaType"))})"));
                }
            }
            catch (Exception ex)
            {
                writer.WriteLine($" (failed to list root folders: {ex.Message})");
            }

            paths.Add((_appFolderInfo.GetAppDataPath(), "AppData"));
            paths.Add((_appFolderInfo.GetLogFolder(), "Logs"));
            paths.Add((_appFolderInfo.TempFolder, "Temp"));

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (path, kind) in paths)
            {
                if (path.IsNullOrWhiteSpace() || !seen.Add(path))
                {
                    continue;
                }

                WriteStorageLine(writer, path, kind);
            }
        }

        private void WriteStorageLine(TextWriter writer, string path, string kind)
        {
            long? total = null;
            long? free = null;

            try
            {
                if (_diskProvider.FolderExists(path) || _diskProvider.FileExists(path))
                {
                    total = _diskProvider.GetTotalSize(path);
                    free = _diskProvider.GetAvailableSpace(path);
                }
            }
            catch (Exception)
            {
                // A missing or unreadable path is still worth listing with an unknown size.
            }

            var used = total.HasValue && free.HasValue && total.Value > 0
                ? $"{(total.Value - free.Value) * 100.0 / total.Value:0.0}%"
                : "n/a";

            writer.WriteLine($" {path,-48} {FormatSize(total),12} {FormatSize(free),11} {used,6}   {kind}");
        }

        private static string DescribeMediaType(string value)
        {
            switch (value)
            {
                case "0":
                    return "Series";
                case "1":
                    return "Movies";
                case "2":
                    return "Anime";
                default:
                    return $"MediaType {value}";
            }
        }

        private void WriteCounts(TextWriter writer)
        {
            WriteSectionHeader(writer, "LIBRARY & DATABASE COUNTS");

            var tables = new (string Label, string Table)[]
            {
                ("Series", "Series"),
                ("Movies", "Movies"),
                ("Episodes", "Episodes"),
                ("Episode Files", "EpisodeFiles"),
                ("Movie Files", "MovieFiles"),
                ("Extra Files", "ExtraFiles"),
                ("History", "History"),
                ("Download History", "DownloadHistory"),
                ("Collections", "Collections"),
                ("Indexers", "Indexers"),
                ("Root Folders", "RootFolders"),
                ("Quality Profiles", "QualityProfiles"),
                ("Custom Formats", "CustomFormats"),
                ("Import Lists", "ImportLists"),
                ("Notifications", "Notifications"),
                ("Tags", "Tags"),
                ("Transcode Jobs", "TranscodeJobs")
            };

            try
            {
                using var connection = _mainDatabase.OpenConnection();

                foreach (var (label, table) in tables)
                {
                    WriteKeyValue(writer, label, CountRows(connection, table));
                }

                var mainDb = _appFolderInfo.GetDatabase();
                WriteKeyValue(writer, "Database Size", $"{FormatSize(SafeFileSize(mainDb))} ({mainDb})");

                if (_configFileProvider.LogDbEnabled)
                {
                    var logDb = _appFolderInfo.GetLogDatabase();
                    WriteKeyValue(writer, "Log Database Size", $"{FormatSize(SafeFileSize(logDb))} ({logDb})");
                }
            }
            catch (Exception ex)
            {
                writer.WriteLine($"(failed to count database rows: {ex.Message})");
            }
        }

        private static object CountRows(DbConnection connection, string table)
        {
            try
            {
                return connection.QuerySingle<long>($"SELECT COUNT(*) FROM \"{table}\"");
            }
            catch (Exception ex)
            {
                return $"n/a ({ex.Message})";
            }
        }

        private long? SafeFileSize(string path)
        {
            try
            {
                return _diskProvider.FileExists(path) ? _diskProvider.GetFileSize(path) : (long?)null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void WriteLogSummary(TextWriter writer)
        {
            WriteSectionHeader(writer, "LOG SUMMARY");

            if (!_configFileProvider.LogDbEnabled)
            {
                writer.WriteLine("(log database disabled; see the APPLICATION LOG FILES section for the raw logs)");
                return;
            }

            try
            {
                using var connection = _logDatabase.OpenConnection();

                WriteKeyValue(writer, "Total Rows", connection.QuerySingle<long>("SELECT COUNT(*) FROM \"Logs\""));

                var first = QueryRows(connection, "SELECT \"Time\" AS Time FROM \"Logs\" ORDER BY \"Time\" ASC LIMIT 1").FirstOrDefault();
                var last = QueryRows(connection, "SELECT \"Time\" AS Time FROM \"Logs\" ORDER BY \"Time\" DESC LIMIT 1").FirstOrDefault();

                WriteKeyValue(writer, "First Entry", first == null ? "<none>" : GetString(first, "Time"));
                WriteKeyValue(writer, "Last Entry", last == null ? "<none>" : GetString(last, "Time"));

                writer.WriteLine();
                writer.WriteLine(" Rows by level:");
                foreach (var row in QueryRows(connection, "SELECT \"Level\" AS Level, COUNT(*) AS Count FROM \"Logs\" GROUP BY \"Level\" ORDER BY COUNT(*) DESC"))
                {
                    writer.WriteLine($"   {GetString(row, "Level"),-8} {GetString(row, "Count"),9}");
                }

                writer.WriteLine();
                writer.WriteLine($" Top {SummaryTopCount} loggers:");
                foreach (var row in QueryRows(connection, $"SELECT \"Logger\" AS Logger, COUNT(*) AS Count FROM \"Logs\" GROUP BY \"Logger\" ORDER BY COUNT(*) DESC LIMIT {SummaryTopCount}"))
                {
                    writer.WriteLine($"   {GetString(row, "Count"),9}  {GetString(row, "Logger")}");
                }

                WriteTopMessages(writer, connection, "Error/Fatal", "WHERE \"Level\" IN ('Error', 'Fatal')");
                WriteTopMessages(writer, connection, "Warn", "WHERE \"Level\" = 'Warn'");
            }
            catch (Exception ex)
            {
                writer.WriteLine($"(failed to summarise logs: {ex.Message})");
            }
        }

        private static void WriteTopMessages(TextWriter writer, DbConnection connection, string label, string where)
        {
            writer.WriteLine();
            writer.WriteLine($" Top {SummaryTopCount} {label} messages (count, first, last, message):");

            var sql = $"SELECT \"Message\" AS Message, COUNT(*) AS Count, MIN(\"Time\") AS First, MAX(\"Time\") AS Last FROM \"Logs\" {where} GROUP BY \"Message\" ORDER BY COUNT(*) DESC LIMIT {SummaryTopCount}";

            foreach (var row in QueryRows(connection, sql))
            {
                writer.WriteLine($"   {GetString(row, "Count"),6}  {GetString(row, "First"),19}  {GetString(row, "Last"),19}  {Trim(GetString(row, "Message"), 160)}");
            }
        }

        private static IEnumerable<IDictionary<string, object>> QueryRows(DbConnection connection, string sql)
        {
            return connection.Query(sql).Cast<IDictionary<string, object>>();
        }

        private static string GetString(IDictionary<string, object> row, string key)
        {
            var actualKey = row.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));

            return actualKey == null ? string.Empty : FormatDbValue(row[actualKey]);
        }

        private static string FormatDbValue(object value)
        {
            switch (value)
            {
                case null:
                    return "<null>";
                case DateTime dateTime:
                    return dateTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                case DateTimeOffset dateTimeOffset:
                    return dateTimeOffset.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private static string Trim(string value, int max)
        {
            if (value == null)
            {
                return string.Empty;
            }

            value = value.Replace('\r', ' ').Replace('\n', ' ');

            return value.Length <= max ? value : string.Concat(value.AsSpan(0, max), "...");
        }

        private static string FormatSize(long? bytes)
        {
            if (!bytes.HasValue)
            {
                return "n/a";
            }

            double value = bytes.Value;
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
            var unit = 0;

            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return $"{value.ToString("0.##", CultureInfo.InvariantCulture)} {units[unit]}";
        }

        private static void WriteFrontend(TextWriter writer, string frontendState)
        {
            WriteSectionHeader(writer, "FRONTEND / BROWSER STATE");

            if (frontendState.IsNullOrWhiteSpace())
            {
                writer.WriteLine("(not provided - the dump was requested without browser state)");
                return;
            }

            writer.WriteLine(PrettyJson(frontendState) ?? frontendState);
        }

        private static string PrettyJson(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);

                return JsonSerializer.Serialize(document.RootElement, IndentedSerializerOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private void WriteConfiguration(TextWriter writer)
        {
            WriteSectionHeader(writer, "CONFIGURATION (config.xml; secrets redacted)");

            var config = _configFileProvider.GetConfigDictionary();

            foreach (var pair in config.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                var value = RedactedConfigFileKeys.Contains(pair.Key) ? Redacted : pair.Value ?? "<null>";
                WriteKeyValue(writer, pair.Key, value);
            }
        }

        private void WriteMetadataProvider(TextWriter writer)
        {
            WriteSectionHeader(writer, "METADATA PROVIDER");

            var metadata = _metadataOptions?.Value ?? new MetadataOptions();

            WriteKeyValue(writer, "Backend", "Providarr");
            WriteKeyValue(writer, "Providarr Base URL", metadata.ProvidarrBaseUrl.IsNullOrWhiteSpace() ? $"(default) {MetadataOptions.DefaultProvidarrBaseUrl}" : metadata.ProvidarrBaseUrl);
            WriteKeyValue(writer, "Movie URL Override", metadata.MovieUrl.IsNullOrWhiteSpace() ? "(none)" : metadata.MovieUrl);
            WriteKeyValue(writer, "Series URL Override", metadata.SeriesUrl.IsNullOrWhiteSpace() ? "(none)" : metadata.SeriesUrl);
            WriteKeyValue(writer, "Services URL Override", metadata.ServicesUrl.IsNullOrWhiteSpace() ? "(none)" : metadata.ServicesUrl);
            WriteKeyValue(writer, "Movie URL (resolved)", ResolveFactoryUrl(_radarrCloudRequestBuilder?.RadarrMetadata));
            WriteKeyValue(writer, "Series URL (resolved)", ResolveFactoryUrl(_sonarrCloudRequestBuilder?.TvdbMetadata));
            WriteKeyValue(writer, "Services URL (resolved)", ResolveFactoryUrl(_sonarrCloudRequestBuilder?.Services));
            WriteKeyValue(writer, "TMDb API Key", metadata.TmdbApiKey.IsNullOrWhiteSpace() ? "(built-in fallback in use)" : "(set; redacted)");
            WriteKeyValue(writer, "Forced Retry Host", MetadataOptions.ForcedRetryHost);
            WriteKeyValue(writer, "Retry Alternative Providers", metadata.RetryAlternativeProviders);
            WriteKeyValue(writer, "Retry Max Retries", metadata.RetryMaxRetries);
            WriteKeyValue(writer, "Retry Base Delay (s)", metadata.RetryBaseDelaySeconds);
            WriteKeyValue(writer, "Retry Max Delay (s)", metadata.RetryMaxDelaySeconds);
            WriteKeyValue(writer, "Retry Jitter", metadata.RetryJitter);

            WriteResolvedRetryPolicy(writer, "Movie Retry Policy", metadata, ResolveFactoryHost(_radarrCloudRequestBuilder?.RadarrMetadata));
            WriteResolvedRetryPolicy(writer, "Series Retry Policy", metadata, ResolveFactoryHost(_sonarrCloudRequestBuilder?.TvdbMetadata));

            writer.WriteLine();
            WriteProvidarrPolicy(writer, metadata);
        }

        private static string ResolveFactoryUrl(IHttpRequestBuilderFactory factory)
        {
            if (factory == null)
            {
                return "(unknown)";
            }

            try
            {
                return factory.Create()?.BaseUrl?.FullUri ?? "(unknown)";
            }
            catch (Exception)
            {
                return "(unavailable)";
            }
        }

        private static string ResolveFactoryHost(IHttpRequestBuilderFactory factory)
        {
            if (factory == null)
            {
                return null;
            }

            try
            {
                return factory.Create()?.BaseUrl?.Host;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void WriteResolvedRetryPolicy(TextWriter writer, string label, MetadataOptions metadata, string host)
        {
            var policy = host.IsNullOrWhiteSpace() ? null : metadata.ResolveRetryPolicy(host);

            if (policy == null || !policy.Enabled)
            {
                WriteKeyValue(writer, label, "disabled");
                return;
            }

            WriteKeyValue(writer, label, $"enabled (maxRetries {policy.MaxRetries}, base {policy.BaseDelay.TotalMilliseconds:0}ms, max {policy.MaxDelay.TotalMilliseconds:0}ms, jitter {policy.Jitter:P0})");
        }

        private void WriteProvidarrPolicy(TextWriter writer, MetadataOptions metadata)
        {
            var baseUrl = metadata.ResolveBaseUrl();

            writer.WriteLine($" Providarr /v1/policy ({baseUrl}/v1/policy):");

            try
            {
                var request = new HttpRequest($"{baseUrl}/v1/policy")
                {
                    RequestTimeout = TimeSpan.FromSeconds(10)
                };

                var response = _httpClient.Get(request);
                var content = response?.Content;

                writer.WriteLine(content.IsNullOrWhiteSpace() ? "(no response)" : (PrettyJson(content) ?? content));
            }
            catch (Exception ex)
            {
                writer.WriteLine($" (failed to query: {ex.Message})");
            }
        }

        private void WriteUpstreamIntegrations(TextWriter writer)
        {
            WriteSectionHeader(writer, "UPSTREAM AUTH INTEGRATIONS");

            var servarrAuthEnabled = ServarrAuthDependencies.Enabled;
            WriteKeyValue(writer, "Servarr auth enabled", servarrAuthEnabled);

            if (servarrAuthEnabled)
            {
                writer.WriteLine("All upstream-brokered integrations are enabled.");
                return;
            }

            writer.WriteLine("These providers are hidden because their OAuth is brokered by auth.servarr.com / services.sonarr.tv:");

            var types = DiscoverServarrAuthDependentTypes();

            if (types.Count == 0)
            {
                writer.WriteLine(" (none discovered)");
                return;
            }

            foreach (var type in types)
            {
                writer.WriteLine($"   {type}");
            }
        }

        private static List<string> DiscoverServarrAuthDependentTypes()
        {
            var results = new List<string>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (type == null || !type.IsClass || type.IsAbstract)
                    {
                        continue;
                    }

                    if (typeof(IServarrAuthDependent).IsAssignableFrom(type))
                    {
                        results.Add(type.FullName);
                    }
                }
            }

            return results.Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void WriteLogging(TextWriter writer)
        {
            WriteSectionHeader(writer, "LOGGING");

            WriteKeyValue(writer, "Log Level", _configFileProvider.LogLevel);
            WriteKeyValue(writer, "Console Log Level", _configFileProvider.ConsoleLogLevel.IsNullOrWhiteSpace() ? "(default)" : _configFileProvider.ConsoleLogLevel);
            WriteKeyValue(writer, "Log SQL", _configFileProvider.LogSql);
            WriteKeyValue(writer, "Log DB Enabled", _configFileProvider.LogDbEnabled);
            WriteKeyValue(writer, "Log Rotate", _configFileProvider.LogRotate);
            WriteKeyValue(writer, "Log Size Limit (MB)", _configFileProvider.LogSizeLimit);
            WriteKeyValue(writer, "Syslog Server", _configFileProvider.SyslogServer.IsNullOrWhiteSpace() ? "(disabled)" : _configFileProvider.SyslogServer);
            WriteKeyValue(writer, "Syslog Port", _configFileProvider.SyslogPort);
            WriteKeyValue(writer, "Syslog Level", _configFileProvider.SyslogLevel);

            writer.WriteLine();
            writer.WriteLine(" Active NLog rules (logger -> targets [levels]):");

            try
            {
                var configuration = LogManager.Configuration;

                if (configuration == null)
                {
                    writer.WriteLine(" (no NLog configuration loaded)");
                    return;
                }

                foreach (var rule in configuration.LoggingRules)
                {
                    var targets = string.Join(",", rule.Targets.Select(t => t.Name));
                    var levels = string.Join(",", rule.Levels.Select(l => l.Name));

                    writer.WriteLine($"   {rule.LoggerNamePattern,-28} -> {targets,-30} [{levels}]");
                }
            }
            catch (Exception ex)
            {
                writer.WriteLine($" (failed to read NLog configuration: {ex.Message})");
            }
        }

        private void WriteHealth(TextWriter writer)
        {
            WriteSectionHeader(writer, "HEALTH");

            var health = (_healthCheckService.Results() ?? new List<HealthCheckModel>())
                .Select(h => new
                {
                    Source = h.Source?.Name,
                    Type = h.Type.ToString(),
                    Reason = h.Reason.ToString(),
                    Message = RedactCredentialText(h.Message)
                });

            WriteJson(writer, health);
        }

        private void WriteTasks(TextWriter writer)
        {
            WriteSectionHeader(writer, "SCHEDULED TASKS");

            var tasks = (_taskManager.GetAll() ?? new List<ScheduledTask>())
                .OrderBy(t => t.TypeName)
                .Select(t => new
                {
                    Name = t.TypeName,
                    t.Interval,
                    t.LastExecution,
                    t.LastStartTime
                });

            WriteJson(writer, tasks);
        }

        private void WriteCommands(TextWriter writer)
        {
            WriteSectionHeader(writer, "COMMAND QUEUE");

            var commands = (_commandQueueManager.All() ?? new List<CommandModel>())
                .OrderBy(c => c.Id)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    Status = c.Status.ToString(),
                    Priority = c.Priority.ToString(),
                    c.QueuedAt,
                    c.StartedAt,
                    c.EndedAt,
                    c.Duration,
                    Message = RedactCredentialText(c.Message),
                    Exception = RedactCredentialText(c.Exception)
                })
                .ToList();

            writer.WriteLine($"Summary: {commands.Count} item(s){SummarizeBy(commands.Select(c => c.Status))}");
            WriteJson(writer, commands);
        }

        private void WriteQueue(TextWriter writer)
        {
            WriteSectionHeader(writer, "DOWNLOAD QUEUE");

            var queue = (_queueService.GetQueue() ?? new List<QueueModel>())
                .OrderBy(q => q.Id)
                .Select(q => new
                {
                    q.Id,
                    q.Title,
                    Status = q.Status.ToString(),
                    TrackedDownloadStatus = q.TrackedDownloadStatus?.ToString(),
                    TrackedDownloadState = q.TrackedDownloadState?.ToString(),
                    StatusMessages = q.StatusMessages?.Select(m => new { Title = RedactCredentialText(m.Title), Messages = m.Messages?.Select(RedactCredentialText) }),
                    q.Size,
                    q.SizeLeft,
                    q.TimeLeft,
                    q.Added,
                    Series = q.Series?.Title,
                    Movie = q.Movie?.Title,
                    q.DownloadClient,
                    q.Indexer,
                    q.OutputPath,
                    ErrorMessage = RedactCredentialText(q.ErrorMessage)
                })
                .ToList();

            writer.WriteLine($"Summary: {queue.Count} item(s){SummarizeBy(queue.Select(q => q.TrackedDownloadState ?? "Unknown"))}");
            WriteJson(writer, queue);
        }

        private void WriteMediaCompression(TextWriter writer)
        {
            WriteSectionHeader(writer, "MEDIA COMPRESSION");

            writer.WriteLine("Settings:");
            WriteKeyValue(writer, "Enabled", _configService.MediaCompressionEnabled);
            WriteKeyValue(writer, "Temp Folder", _configService.TranscodeTempFolder.IsNullOrWhiteSpace() ? "(default: <app-data>/transcode)" : _configService.TranscodeTempFolder);
            WriteKeyValue(writer, "FFmpeg Path Override", _configService.FFmpegPath.IsNullOrWhiteSpace() ? "(auto-detect)" : _configService.FFmpegPath);
            WriteKeyValue(writer, "Max Concurrent Jobs", _configService.MaxConcurrentJobs);
            WriteKeyValue(writer, "Default Codec", _configService.DefaultVideoCodec);
            WriteKeyValue(writer, "Default Mode", _configService.DefaultRateControlMode);
            WriteKeyValue(writer, "Default Quality (CRF/QP)", _configService.DefaultQualityValue);
            WriteKeyValue(writer, "Default Preset", _configService.DefaultPreset);
            WriteKeyValue(writer, "Default Reduce Percent", _configService.DefaultReducePercent);
            WriteKeyValue(writer, "Default Episode Target (MB)", _configService.DefaultTargetEpisodeSizeMB);
            WriteKeyValue(writer, "Default Movie Target (MB)", _configService.DefaultTargetMovieSizeMB);
            WriteKeyValue(writer, "Review Default", _configService.TranscodeReviewDefault);
            WriteKeyValue(writer, "Prefer Hardware", _configService.PreferHardware);
            WriteKeyValue(writer, "CPU Nice", _configService.TranscodeNice);

            writer.WriteLine();
            WriteTranscodeProfiles(writer);

            writer.WriteLine();
            WriteDeviceRegistry(writer);

            writer.WriteLine();
            WriteTranscodeJobs(writer);
        }

        private void WriteTranscodeProfiles(TextWriter writer)
        {
            var profiles = (_transcodeProfileRepository.All() ?? new List<TranscodeProfile>())
                .OrderByDescending(profile => profile.IsDefault)
                .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            writer.WriteLine($"Profiles: {profiles.Count} total");

            WriteJson(writer, profiles.Select(profile => new
            {
                profile.Id,
                profile.Name,
                Codec = profile.Codec,
                Mode = profile.Mode.ToString(),
                profile.QualityValue,
                profile.Preset,
                profile.TargetSizeMB,
                profile.TargetPercent,
                profile.DeviceId,
                profile.Container,
                profile.MaxHeight,
                profile.Tag,
                profile.PreferEnglishAudio,
                profile.IsDefault
            }));
        }

        // Reads the persisted capability snapshot rather than calling IGpuCapabilityService, so
        // requesting a dump never kicks off a hardware probe encode.
        private void WriteDeviceRegistry(TextWriter writer)
        {
            MediaCompressionCapabilities capabilities = null;
            var json = _configService.TranscodeDevicesConfig;

            if (json.IsNotNullOrWhiteSpace())
            {
                try
                {
                    capabilities = Json.Deserialize<MediaCompressionCapabilities>(json);
                }
                catch (Exception ex)
                {
                    writer.WriteLine($"(stored device registry could not be parsed: {ex.Message})");
                }
            }

            if (capabilities == null)
            {
                writer.WriteLine("Device Registry: (no snapshot yet - open Settings > Compression or run Reprobe)");
                return;
            }

            writer.WriteLine("Device Registry (cached snapshot; Reprobe to refresh after a hardware/ffmpeg change):");
            WriteKeyValue(writer, "FFmpeg Path", capabilities.FFmpegPath);
            WriteKeyValue(writer, "FFmpeg Version", capabilities.FFmpegVersion);
            WriteKeyValue(writer, "Fingerprint", capabilities.Fingerprint);
            WriteKeyValue(writer, "Last Probed (UTC)", capabilities.LastProbed?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            var devices = (capabilities.Devices ?? new List<TranscodeDevice>())
                .OrderBy(device => device.Priority)
                .Select(device => new
                {
                    device.Id,
                    Kind = device.Kind.ToString(),
                    device.Name,
                    device.Supported,
                    device.Enabled,
                    device.Unavailable,
                    device.MaxParallel,
                    device.Priority,
                    device.Weight,
                    device.Codecs,
                    Encoders = device.Capabilities?.Encoders,
                    Decoders = device.Capabilities?.Decoders,
                    Filters = device.Capabilities?.Filters,
                    device.Capabilities?.Entrypoints,
                    device.Capabilities?.Tonemap,
                    device.Capabilities?.MaxSessions,
                    device.Capabilities?.VramMB,
                    device.Capabilities?.Driver
                })
                .ToList();

            writer.WriteLine($"{devices.Count} device(s)");
            WriteJson(writer, devices);
        }

        private void WriteTranscodeJobs(TextWriter writer)
        {
            var jobs = (_transcodeJobRepository.All() ?? new List<TranscodeJob>())
                .OrderByDescending(job => job.Id)
                .ToList();

            writer.WriteLine($"Jobs: {jobs.Count} total{SummarizeBy(jobs.Select(job => job.Status.ToString()))}");

            const int detailCount = 100;

            var recent = jobs.Take(detailCount).Select(job => new
            {
                job.Id,
                MediaType = job.MediaType.ToString(),
                Status = job.Status.ToString(),
                Mode = job.Mode.ToString(),
                job.VideoCodec,
                job.RateControl,
                job.DeviceId,
                job.ProfileId,
                job.Container,
                job.SourcePath,
                job.OutputPath,
                job.OriginalPath,
                job.SourceSize,
                job.OutputSize,
                job.TargetSize,
                job.TargetPercent,
                job.QualityValue,
                job.MaxHeight,
                job.Tag,
                job.PreferEnglishAudio,
                job.Preset,
                Progress = Math.Round(job.Progress, 1),
                job.Speed,
                job.Fps,
                job.Eta,
                job.StartedAt,
                job.EndedAt,
                job.Error,
                job.Message
            }).ToList();

            writer.WriteLine($"Showing the {recent.Count} most recent job(s) of {jobs.Count}.");
            WriteJson(writer, recent);
        }

        private static string SummarizeBy(IEnumerable<string> keys)
        {
            var text = string.Join(", ", keys
                .GroupBy(k => k)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Select(g => $"{g.Count()} {g.Key}"));

            return text.IsNullOrWhiteSpace() ? string.Empty : $" ({text})";
        }

        private void WriteDatabases(TextWriter writer)
        {
            WriteSectionHeader(writer, "DATABASE: theoriarr.db");
            WriteDatabase(writer, _mainDatabase);

            if (_configFileProvider.LogDbEnabled)
            {
                WriteSectionHeader(writer, "DATABASE: logs.db");
                WriteDatabase(writer, _logDatabase);
            }
        }

        private void WriteLogFiles(TextWriter writer)
        {
            WriteLogFolder(writer, "APPLICATION LOG FILES", _appFolderInfo.GetLogFolder());
            WriteLogFolder(writer, "UPDATE LOG FILES", _appFolderInfo.GetUpdateLogFolder());
        }

        private void WriteLogFolder(TextWriter writer, string title, string folder)
        {
            WriteSectionHeader(writer, title);

            if (!_diskProvider.FolderExists(folder))
            {
                writer.WriteLine($"(folder not found: {folder})");
                return;
            }

            var files = _diskProvider.GetFiles(folder, false)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (files.Count == 0)
            {
                writer.WriteLine($"(no log files in {folder})");
                return;
            }

            foreach (var file in files)
            {
                writer.WriteLine();
                writer.WriteLine($"===== FILE: {Path.GetFileName(file)} =====");

                try
                {
                    writer.WriteLine(_diskProvider.ReadAllText(file));
                }
                catch (Exception ex)
                {
                    writer.WriteLine($"(failed to read: {ex.Message})");
                }
            }
        }

        private static void WriteDatabase(TextWriter writer, IDatabase database)
        {
            try
            {
                using var connection = database.OpenConnection();

                foreach (var table in GetTableNames(connection, database.DatabaseType))
                {
                    WriteDatabaseTable(writer, connection, table);
                }
            }
            catch (Exception ex)
            {
                writer.WriteLine($"(failed to dump database: {ex.Message})");
            }
        }

        private static void WriteDatabaseTable(TextWriter writer, DbConnection connection, string table)
        {
            try
            {
                var rows = connection.Query($"SELECT * FROM \"{table}\"")
                    .Cast<IDictionary<string, object>>()
                    .ToList();

                writer.WriteLine();
                writer.WriteLine($"--- {table} ({rows.Count} rows) ---");

                foreach (var row in rows)
                {
                    RedactRow(table, row);
                    writer.WriteLine(JsonSerializer.Serialize(row, RowSerializerOptions));
                }
            }
            catch (Exception ex)
            {
                writer.WriteLine();
                writer.WriteLine($"--- {table} (failed: {ex.Message}) ---");
            }
        }

        private static IEnumerable<string> GetTableNames(DbConnection connection, DatabaseType databaseType)
        {
            if (databaseType == DatabaseType.PostgreSQL)
            {
                return connection.Query<string>("SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename");
            }

            return connection.Query<string>("SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name");
        }

        private static void RedactRow(string table, IDictionary<string, object> row)
        {
            if (table.Equals("Users", StringComparison.OrdinalIgnoreCase))
            {
                MaskValue(row, "Password");
                MaskValue(row, "Salt");
            }
            else if (table.Equals("Config", StringComparison.OrdinalIgnoreCase))
            {
                var key = FindKey(row, "Key");

                if (key != null && row[key] is string configKey && RedactedDatabaseConfigKeys.Contains(configKey))
                {
                    var value = FindKey(row, "Value");

                    if (value != null)
                    {
                        row[value] = Redacted;
                    }
                }
            }

            // Provider rows (Indexers/DownloadClients/Notifications/ImportLists) carry their
            // credentials inside a Settings JSON blob; mask credential-shaped keys there too.
            var settingsKey = FindKey(row, "Settings");

            if (settingsKey != null && row[settingsKey] is string settings && settings.IsNotNullOrWhiteSpace())
            {
                row[settingsKey] = RedactSettingsJson(settings);
            }
        }

        private static string RedactSettingsJson(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);

                return JsonSerializer.Serialize(RedactJsonElement(document.RootElement), RowSerializerOptions);
            }
            catch (JsonException)
            {
                return RedactCredentialText(json);
            }
        }

        private static object RedactJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                    foreach (var property in element.EnumerateObject())
                    {
                        result[property.Name] = IsCredentialJsonKey(property.Name)
                            ? Redacted
                            : RedactJsonElement(property.Value);
                    }

                    return result;
                case JsonValueKind.Array:
                    return element.EnumerateArray().Select(RedactJsonElement).ToList();
                case JsonValueKind.String:
                    return RedactCredentialText(element.GetString());
                case JsonValueKind.Number:
                    return element.GetDouble();
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();
                default:
                    return null;
            }
        }

        private static bool IsCredentialJsonKey(string key)
        {
            if (key.IsNullOrWhiteSpace())
            {
                return false;
            }

            var normalized = new string(key.Where(char.IsLetterOrDigit).ToArray());

            return RedactedJsonKeyTokens.Contains(normalized) ||
                   normalized.EndsWith("password", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("passwd", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("secret", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("token", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("apikey", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("passphrase", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("rsskey", StringComparison.OrdinalIgnoreCase);
        }

        // Removes credential-shaped values from free text (command/health/queue messages and
        // exception strings) while preserving enough context to be useful for troubleshooting.
        private static string RedactCredentialText(string value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return value;
            }

            value = CredentialTextRegex.Replace(value, "${key}********");
            value = BearerTokenRegex.Replace(value, "Bearer ********");
            value = UrlCredentialRegex.Replace(value, "${scheme}********");

            return value;
        }

        private static void MaskValue(IDictionary<string, object> row, string column)
        {
            var key = FindKey(row, column);

            if (key != null)
            {
                row[key] = Redacted;
            }
        }

        private static string FindKey(IDictionary<string, object> row, string column)
        {
            return row.Keys.FirstOrDefault(k => k.Equals(column, StringComparison.OrdinalIgnoreCase));
        }

        private string BuildPrefix(List<DiagnosticsSection> sections, int offset)
        {
            var builder = new StringBuilder();

            using (var writer = new StringWriter(builder))
            {
                WriteHeader(writer);
                WriteSectionIndex(writer, sections, offset);
            }

            return builder.ToString();
        }

        private static int CountLines(string text)
        {
            return text.Count(c => c == '\n');
        }

        private static void WriteSectionIndex(TextWriter writer, List<DiagnosticsSection> sections, int offset)
        {
            WriteSectionHeader(writer, "SECTION INDEX");
            writer.WriteLine(" Line  Lines  Section");

            foreach (var section in sections)
            {
                writer.WriteLine($"{section.Line + offset,5} {section.LineCount,6}  {section.Title}");
            }

            writer.WriteLine();
            writer.WriteLine(" Navigate with scripts/dump-nav.py:");
            writer.WriteLine("   dump-nav.py <dump> sections              list sections (line number + size)");
            writer.WriteLine("   dump-nav.py <dump> show <section>        print one section (--limit N)");
            writer.WriteLine("   dump-nav.py <dump> grep <section> <re>   search inside one section (-i)");
            writer.WriteLine("   dump-nav.py <dump> json <section>        pretty-print a JSON section");
            writer.WriteLine("   dump-nav.py <dump> tables                database tables (name + row count)");
            writer.WriteLine("   dump-nav.py <dump> table <name>          print one database table");
            writer.WriteLine("   dump-nav.py <dump> files <section>       log files listed in a section");
            writer.WriteLine(" SECTION is a case-insensitive regex, e.g. \"download queue\" or \"log summary\".");
        }

        private List<DiagnosticsSection> IndexSections(string bodyPath)
        {
            var sections = new List<DiagnosticsSection>();
            var lineNumber = 0;

            using (var stream = _diskProvider.OpenReadStream(bodyPath))
            using (var reader = new StreamReader(stream))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;

                    var match = SectionLineRegex.Match(line);

                    if (match.Success)
                    {
                        sections.Add(new DiagnosticsSection { Title = match.Groups[1].Value, Line = lineNumber });
                    }
                }
            }

            for (var i = 0; i < sections.Count; i++)
            {
                var end = i + 1 < sections.Count ? sections[i + 1].Line - 1 : lineNumber;
                sections[i].LineCount = end - sections[i].Line + 1;
            }

            return sections;
        }

        private sealed class DiagnosticsSection
        {
            public string Title { get; set; }
            public int Line { get; set; }
            public int LineCount { get; set; }
        }
    }
}
