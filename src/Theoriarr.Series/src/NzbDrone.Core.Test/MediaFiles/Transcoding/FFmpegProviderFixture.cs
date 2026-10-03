using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Processes;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.Transcoding
{
    [TestFixture]
    public class FFmpegProviderFixture : CoreTest<FFmpegProvider>
    {
        private string _startupFolder;

        [SetUp]
        public void Setup()
        {
            _startupFolder = @"C:\App".AsOsAgnostic();

            Mocker.GetMock<IAppFolderInfo>()
                  .Setup(s => s.StartUpFolder)
                  .Returns(_startupFolder);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(It.IsAny<string>()))
                  .Returns(false);
        }

        private void GivenConfiguredPath(string path)
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.FFmpegPath)
                  .Returns(path);
        }

        private void GivenExistingFile(string path)
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.FileExists(path))
                  .Returns(true);
        }

        [Test]
        public void should_return_configured_path_when_it_exists()
        {
            var configured = @"C:\tools\ffmpeg".AsOsAgnostic();

            GivenConfiguredPath(configured);
            GivenExistingFile(configured);

            Subject.GetFFmpegPath().Should().Be(configured);
        }

        [Test]
        public void should_fall_back_to_bundled_binary_when_configured_path_is_missing()
        {
            var bundled = Path.Combine(_startupFolder, OsInfo.IsWindows ? "ffmpeg.exe" : "ffmpeg");

            GivenConfiguredPath(@"C:\missing\ffmpeg".AsOsAgnostic());
            GivenExistingFile(bundled);

            Subject.GetFFmpegPath().Should().Be(bundled);
        }

        [Test]
        public void should_return_null_when_no_binary_can_be_found()
        {
            GivenConfiguredPath(string.Empty);

            Subject.GetFFmpegPath().Should().BeNull();
        }

        [Test]
        public void should_parse_version_from_ffmpeg_output()
        {
            var configured = @"C:\tools\ffmpeg".AsOsAgnostic();

            GivenConfiguredPath(configured);
            GivenExistingFile(configured);

            Mocker.GetMock<IProcessProvider>()
                  .Setup(s => s.StartAndCapture(configured, "-version", null))
                  .Returns(new ProcessOutput
                  {
                      ExitCode = 0,
                      Lines = new List<ProcessOutputLine>
                      {
                          new ProcessOutputLine(ProcessOutputLevel.Standard, "ffmpeg version 6.1.1-1ubuntu1 Copyright (c) 2000-2024 the FFmpeg developers")
                      }
                  });

            Subject.GetVersion().Should().Be("6.1.1-1ubuntu1");
        }

        [Test]
        public void should_return_null_version_when_no_binary_can_be_found()
        {
            GivenConfiguredPath(string.Empty);

            Subject.GetVersion().Should().BeNull();
        }

        [Test]
        public void should_return_null_version_when_ffmpeg_cannot_be_executed()
        {
            var configured = @"C:\tools\ffmpeg".AsOsAgnostic();

            GivenConfiguredPath(configured);
            GivenExistingFile(configured);

            Mocker.GetMock<IProcessProvider>()
                  .Setup(s => s.StartAndCapture(configured, "-version", null))
                  .Throws(new System.ComponentModel.Win32Exception("Permission denied"));

            Subject.GetVersion().Should().BeNull();
        }
    }
}
