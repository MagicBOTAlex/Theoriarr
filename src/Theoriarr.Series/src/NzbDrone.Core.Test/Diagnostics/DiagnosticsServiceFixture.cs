using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Options;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Diagnostics;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.Framework;
using HealthCheckModel = NzbDrone.Core.HealthCheck.HealthCheck;

namespace NzbDrone.Core.Test.Diagnostics
{
    [TestFixture]
    public class DiagnosticsServiceFixture : CoreTest<DiagnosticsService>
    {
        private string _tempFolder;
        private string _logFolder;
        private string _logFile;
        private string _dbFile;

        [SetUp]
        public void Setup()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "theoriarr-diagnostics-test-" + Guid.NewGuid().ToString("N"));
            _logFolder = Path.Combine(_tempFolder, "logs");
            _logFile = Path.Combine(_logFolder, "theoriarr.txt");
            _dbFile = Path.Combine(_tempFolder, "theoriarr.db");

            Directory.CreateDirectory(_logFolder);
            File.WriteAllText(_logFile, "SAMPLE LOG LINE");

            using (var connection = new SQLiteConnection($"Data Source={_dbFile}"))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE Config (Key TEXT, Value TEXT);
                        INSERT INTO Config VALUES ('RijndaelPassphrase', 'super-secret-passphrase');
                        INSERT INTO Config VALUES ('UiSettings', 'not-secret');
                        CREATE TABLE Users (Id INTEGER, Username TEXT, Password TEXT, Salt TEXT);
                        INSERT INTO Users VALUES (1, 'admin', 'password-hash', 'salt-value');
                        CREATE TABLE Series (Id INTEGER);
                        INSERT INTO Series VALUES (1);
                        INSERT INTO Series VALUES (2);
                        CREATE TABLE RootFolders (Id INTEGER, Path TEXT, MediaType INTEGER);
                        INSERT INTO RootFolders VALUES (1, 'C:\\tv', 0);";
                    command.ExecuteNonQuery();
                }
            }

            WithFolderInfo();
            WithDisk();
            WithConfig();
            WithDatabase();
            WithEmptyRuntimeState();
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_tempFolder))
            {
                Directory.Delete(_tempFolder, true);
            }
        }

        private void WithFolderInfo()
        {
            Mocker.GetMock<IAppFolderInfo>().SetupGet(f => f.TempFolder).Returns(_tempFolder);
            Mocker.GetMock<IAppFolderInfo>().SetupGet(f => f.AppDataFolder).Returns(_tempFolder);
            Mocker.GetMock<IAppFolderInfo>().SetupGet(f => f.StartUpFolder).Returns(_tempFolder);
        }

        private void WithDisk()
        {
            var disk = Mocker.GetMock<IDiskProvider>();

            disk.Setup(d => d.OpenWriteStream(It.IsAny<string>()))
                .Returns<string>(path => new FileStream(path, FileMode.Create, FileAccess.Write));

            disk.Setup(d => d.OpenReadStream(It.IsAny<string>()))
                .Returns<string>(path => new FileStream(path, FileMode.Open, FileAccess.Read));

            disk.Setup(d => d.FolderExists(It.IsAny<string>())).Returns(true);
            disk.Setup(d => d.FileExists(It.IsAny<string>())).Returns(true);

            disk.Setup(d => d.GetFiles(It.IsAny<string>(), It.IsAny<bool>()))
                .Returns<string, bool>((path, recursive) =>
                    path.Equals(_logFolder, StringComparison.OrdinalIgnoreCase)
                        ? new[] { _logFile }
                        : Array.Empty<string>());

            disk.Setup(d => d.ReadAllText(_logFile)).Returns("SAMPLE LOG LINE");
        }

        private void WithConfig()
        {
            Mocker.GetMock<IConfigFileProvider>()
                .Setup(c => c.GetConfigDictionary())
                .Returns(new Dictionary<string, object>
                {
                    { "ApiKey", "top-secret-api-key" },
                    { "Port", 6868 },
                    { "UrlBase", string.Empty }
                });

            Mocker.GetMock<IConfigFileProvider>().SetupGet(c => c.LogDbEnabled).Returns(false);
        }

        private void WithDatabase()
        {
            Mocker.GetMock<IMainDatabase>().Setup(d => d.OpenConnection()).Returns(() =>
            {
                var connection = new SQLiteConnection($"Data Source={_dbFile}");
                connection.Open();
                return connection;
            });

            Mocker.GetMock<IMainDatabase>().SetupGet(d => d.DatabaseType).Returns(DatabaseType.SQLite);
            Mocker.GetMock<IMainDatabase>().SetupGet(d => d.Version).Returns(new Version(3, 0, 0));
            Mocker.GetMock<IMainDatabase>().SetupGet(d => d.Migration).Returns(316);
        }

        private void WithEmptyRuntimeState()
        {
            Mocker.GetMock<IHealthCheckService>().Setup(h => h.Results()).Returns(new List<HealthCheckModel>());
            Mocker.GetMock<ITaskManager>().Setup(t => t.GetAll()).Returns(new List<ScheduledTask>());
            Mocker.GetMock<IManageCommandQueue>().Setup(c => c.All()).Returns(new List<CommandModel>());
            Mocker.GetMock<IQueueService>().Setup(q => q.GetQueue()).Returns(new List<NzbDrone.Core.Queue.Queue>());
        }

        private string CreateAndRead()
        {
            var path = Subject.CreateDiagnosticsFile();

            File.Exists(path).Should().BeTrue();

            return File.ReadAllText(path);
        }

        [Test]
        public void should_include_all_sections()
        {
            var contents = CreateAndRead();

            contents.Should().Contain("THEORIARR DIAGNOSTICS DUMP");
            contents.Should().Contain("SECTION INDEX");
            contents.Should().Contain("SYSTEM STATUS");
            contents.Should().Contain("STORAGE / DISK USAGE");
            contents.Should().Contain("LIBRARY & DATABASE COUNTS");
            contents.Should().Contain("LOG SUMMARY");
            contents.Should().Contain("FRONTEND / BROWSER STATE");
            contents.Should().Contain("CONFIGURATION");
            contents.Should().Contain("METADATA PROVIDER");
            contents.Should().Contain("UPSTREAM AUTH INTEGRATIONS");
            contents.Should().Contain("LOGGING");
            contents.Should().Contain("HEALTH");
            contents.Should().Contain("SCHEDULED TASKS");
            contents.Should().Contain("COMMAND QUEUE");
            contents.Should().Contain("DOWNLOAD QUEUE");
            contents.Should().Contain("MEDIA COMPRESSION");
            contents.Should().Contain("DATABASE: theoriarr.db");
            contents.Should().Contain("APPLICATION LOG FILES");
        }

        [Test]
        public void should_include_media_compression_registry_and_jobs()
        {
            var capabilities = new MediaCompressionCapabilities
            {
                Fingerprint = "fp-123",
                FFmpegPath = "/usr/bin/ffmpeg",
                FFmpegVersion = "6.1.1",
                Devices = new List<TranscodeDevice>
                {
                    new TranscodeDevice
                    {
                        Id = "nvenc:0",
                        Kind = TranscodeDeviceKind.Nvidia,
                        Name = "GPU 0",
                        Enabled = true,
                        Supported = true,
                        MaxParallel = 2,
                        Priority = 10,
                        Weight = 100,
                        Capabilities = new DeviceCapability
                        {
                            Encoders = new List<string> { "hevc_nvenc" }
                        }
                    }
                }
            };

            Mocker.GetMock<IConfigService>()
                  .SetupGet(config => config.TranscodeDevicesConfig)
                  .Returns(capabilities.ToJson());

            Mocker.GetMock<ITranscodeProfileRepository>()
                  .Setup(repository => repository.All())
                  .Returns(new List<TranscodeProfile>
                  {
                      new TranscodeProfile
                      {
                          Id = 3,
                          Name = "Downscale",
                          MaxHeight = 720,
                          Tag = "hd",
                          PreferEnglishAudio = true
                      }
                  });

            Mocker.GetMock<ITranscodeJobRepository>()
                  .Setup(repository => repository.All())
                  .Returns(new List<TranscodeJob>
                  {
                      new TranscodeJob
                      {
                          Id = 7,
                          MediaType = MediaType.Series,
                          Status = TranscodeJobStatus.Failed,
                          SourcePath = "/media/show/episode.mkv",
                          RateControl = "constant-quality",
                          ProfileId = 3,
                          Container = "mkv",
                          MaxHeight = 720,
                          Tag = "hd",
                          PreferEnglishAudio = true,
                          Error = "boom"
                      }
                  });

            var contents = CreateAndRead();

            contents.Should().Contain("MEDIA COMPRESSION");
            contents.Should().Contain("fp-123");
            contents.Should().Contain("hevc_nvenc");
            contents.Should().Contain("boom");
            contents.Should().Contain("\"rateControl\": \"constant-quality\"");
            contents.Should().Contain("\"profileId\": 3");
            contents.Should().Contain("\"container\": \"mkv\"");
            contents.Should().Contain("\"maxHeight\": 720");
            contents.Should().Contain("\"tag\": \"hd\"");
            contents.Should().Contain("\"preferEnglishAudio\": true");
        }

        [Test]
        public void should_include_build_revision_in_header_and_system_status()
        {
            var contents = CreateAndRead();

            contents.Should().Contain("Revision");
            contents.Should().Contain("Build Config");
            contents.Should().Contain("Git Commit");
            contents.Should().Contain("Git Branch");
        }

        [Test]
        public void should_list_database_counts_and_navigation_help()
        {
            var contents = CreateAndRead();

            contents.Should().Contain("Series");
            contents.Should().Contain("Database Size");
            contents.Should().Contain("dump-nav.py");
        }

        [Test]
        public void should_summarize_the_command_and_download_queues()
        {
            var contents = CreateAndRead();

            contents.Should().Contain("Summary:");
            contents.Should().Contain("DOWNLOAD QUEUE");
        }

        [Test]
        public void should_prettify_frontend_json()
        {
            var path = Subject.CreateDiagnosticsFile("{\"browser\":{\"userAgent\":\"test-agent\"}}");
            var contents = File.ReadAllText(path);

            contents.Should().Contain("\"userAgent\": \"test-agent\"");
            contents.Should().Contain("\n");
        }

        [Test]
        public void should_summarize_the_log_database()
        {
            var logDbFile = Path.Combine(_tempFolder, "logs.db");

            using (var connection = new SQLiteConnection($"Data Source={logDbFile}"))
            {
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE Logs (Id INTEGER, Message TEXT, Time DATETIME, Logger TEXT, Exception TEXT, ExceptionType TEXT, Level TEXT);
                    INSERT INTO Logs VALUES (1, 'boom', '2026-09-21 21:09:06', 'VideoFileInfoReader', NULL, 'X', 'Error');
                    INSERT INTO Logs VALUES (2, 'boom', '2026-09-22 10:00:00', 'VideoFileInfoReader', NULL, 'X', 'Error');
                    INSERT INTO Logs VALUES (3, 'all good', '2026-09-22 11:00:00', 'RssSync', NULL, NULL, 'Info');";
                command.ExecuteNonQuery();
            }

            Mocker.GetMock<ILogDatabase>().Setup(d => d.OpenConnection()).Returns(() =>
            {
                var connection = new SQLiteConnection($"Data Source={logDbFile}");
                connection.Open();
                return connection;
            });

            Mocker.GetMock<IConfigFileProvider>().SetupGet(c => c.LogDbEnabled).Returns(true);

            var contents = CreateAndRead();

            contents.Should().Contain("LOG SUMMARY");
            contents.Should().Contain("Rows by level");
            contents.Should().Contain("VideoFileInfoReader");
            contents.Should().Contain("boom");
        }

        [Test]
        public void section_index_line_numbers_point_at_section_headers()
        {
            var contents = CreateAndRead();
            var lines = contents.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

            var match = Regex.Match(contents, @"^\s*(\d+)\s+\d+\s+DOWNLOAD QUEUE\s*$", RegexOptions.Multiline);
            match.Success.Should().BeTrue("the section index should list DOWNLOAD QUEUE with its line number");

            var lineNumber = int.Parse(match.Groups[1].Value);
            lines[lineNumber - 1].Should().Contain("DOWNLOAD QUEUE");
            lines[lineNumber - 1].Should().StartWith("=");
        }

        [Test]
        public void should_redact_secrets_from_config()
        {
            var contents = CreateAndRead();

            contents.Should().NotContain("top-secret-api-key");
            contents.Should().Contain("ApiKey");
            contents.Should().Contain("********");
        }

        [Test]
        public void should_dump_database_and_redact_secret_columns()
        {
            var contents = CreateAndRead();

            contents.Should().Contain("--- Users (1 rows) ---");
            contents.Should().Contain("--- Config (2 rows) ---");
            contents.Should().Contain("not-secret");

            contents.Should().NotContain("password-hash");
            contents.Should().NotContain("salt-value");
            contents.Should().NotContain("super-secret-passphrase");
        }

        [Test]
        public void should_include_log_file_contents()
        {
            var contents = CreateAndRead();

            contents.Should().Contain("===== FILE: theoriarr.txt =====");
            contents.Should().Contain("SAMPLE LOG LINE");
        }

        [Test]
        public void should_include_frontend_state_when_provided()
        {
            var path = Subject.CreateDiagnosticsFile("{\"browser\":{\"userAgent\":\"test-agent\"}}");
            var contents = File.ReadAllText(path);

            contents.Should().Contain("FRONTEND / BROWSER STATE");
            contents.Should().Contain("test-agent");
        }

        [Test]
        public void should_note_when_frontend_state_is_missing()
        {
            var contents = CreateAndRead();

            contents.Should().Contain("not provided");
        }

        [Test]
        public void should_include_metadata_provider_section()
        {
            var contents = CreateAndRead();

            contents.Should().Contain("METADATA PROVIDER");
            contents.Should().Contain("Backend");
            contents.Should().Contain("Providarr");
            contents.Should().Contain("Forced Retry Host");
            contents.Should().Contain("providarr.deprived.dev");
            contents.Should().Contain("Retry Max Retries");
            contents.Should().Contain("(built-in fallback in use)");
            contents.Should().Contain("Providarr /v1/policy");
        }

        [Test]
        public void should_redact_tmdb_api_key_in_metadata_section()
        {
            Mocker.GetMock<IOptions<MetadataOptions>>()
                  .SetupGet(o => o.Value)
                  .Returns(new MetadataOptions { TmdbApiKey = "super-secret-tmdb-key" });

            var contents = CreateAndRead();

            contents.Should().Contain("TMDb API Key");
            contents.Should().Contain("(set; redacted)");
            contents.Should().NotContain("super-secret-tmdb-key");
        }

        [Test]
        public void should_include_upstream_auth_and_logging_sections()
        {
            var contents = CreateAndRead();

            contents.Should().Contain("UPSTREAM AUTH INTEGRATIONS");
            contents.Should().Contain("Servarr auth enabled");
            contents.Should().Contain("brokered by auth.servarr.com / services.sonarr.tv");

            contents.Should().Contain("LOGGING");
            contents.Should().Contain("Log Level");
            contents.Should().Contain("Active NLog rules");
        }

        [Test]
        public void should_only_return_files_it_created()
        {
            var path = Subject.CreateDiagnosticsFile();
            var name = Path.GetFileName(path);

            Subject.GetDiagnosticsFile(name).Should().Be(path);

            Subject.GetDiagnosticsFile("../../etc/passwd").Should().BeNull();
            Subject.GetDiagnosticsFile("evil.txt").Should().BeNull();
            Subject.GetDiagnosticsFile(null).Should().BeNull();
        }

        [Test]
        public void should_redact_provider_settings_credentials()
        {
            using (var connection = new SQLiteConnection($"Data Source={_dbFile}"))
            {
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE Indexers (Id INTEGER, Name TEXT, Settings TEXT);
                    INSERT INTO Indexers VALUES (1, 'Test', '{""apiKey"":""secret-indexer-key"",""baseUrl"":""https://user:pass@indexer.example.com"",""password"":""secret-password""}');";
                command.ExecuteNonQuery();
            }

            var contents = CreateAndRead();

            contents.Should().Contain("Indexers");
            contents.Should().NotContain("secret-indexer-key");
            contents.Should().NotContain("secret-password");
            contents.Should().NotContain("user:pass");
            contents.Should().Contain("********");
        }

        [Test]
        public void should_redact_proxy_password_from_config_table()
        {
            using (var connection = new SQLiteConnection($"Data Source={_dbFile}"))
            {
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO Config VALUES ('ProxyPassword', 'super-secret-proxy');";
                command.ExecuteNonQuery();
            }

            var contents = CreateAndRead();

            contents.Should().Contain("ProxyPassword");
            contents.Should().NotContain("super-secret-proxy");
        }

        [Test]
        public void should_scrub_credentials_from_command_health_and_queue_text()
        {
            Mocker.GetMock<IManageCommandQueue>().Setup(c => c.All()).Returns(new List<CommandModel>
            {
                new CommandModel { Id = 1, Name = "Test", Message = "failed with apiKey=abc123", Exception = "Authorization: Bearer topsecret" }
            });

            Mocker.GetMock<IHealthCheckService>().Setup(h => h.Results()).Returns(new List<HealthCheckModel>
            {
                new HealthCheckModel { Message = "proxy password=hunter2" }
            });

            Mocker.GetMock<IQueueService>().Setup(q => q.GetQueue()).Returns(new List<NzbDrone.Core.Queue.Queue>
            {
                new NzbDrone.Core.Queue.Queue
                {
                    Id = 1,
                    ErrorMessage = "token=leaked-token",
                    StatusMessages = new List<TrackedDownloadStatusMessage>
                    {
                        new TrackedDownloadStatusMessage("apikey=status-secret", new List<string> { "password=nested-secret" })
                    }
                }
            });

            var contents = CreateAndRead();

            contents.Should().NotContain("abc123");
            contents.Should().NotContain("topsecret");
            contents.Should().NotContain("hunter2");
            contents.Should().NotContain("leaked-token");
            contents.Should().NotContain("status-secret");
            contents.Should().NotContain("nested-secret");
            contents.Should().Contain("********");
        }
    }
}
