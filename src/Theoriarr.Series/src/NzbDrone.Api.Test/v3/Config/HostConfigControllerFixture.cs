using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Authentication;
using NzbDrone.Core.Configuration;
using NzbDrone.Test.Common;
using Sonarr.Api.V3.Config;
using Sonarr.Http.REST;
using Sonarr.Http.Subsystem;

namespace NzbDrone.Api.Test.v3.Config
{
    [TestFixture]
    public class HostConfigControllerFixture : TestBase<HostConfigController>
    {
        private const string SeriesKey = "series-api-key";
        private const string MovieKey = "movie-api-key";

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IConfigFileProvider>().SetupGet(c => c.ApiKey).Returns(SeriesKey);
            Mocker.GetMock<IConfigFileProvider>().SetupGet(c => c.MovieApiKey).Returns(MovieKey);
            Mocker.GetMock<IConfigFileProvider>().SetupGet(c => c.OidcClientSecret).Returns(string.Empty);
            Mocker.GetMock<IUserService>().Setup(s => s.FindUser())
                  .Returns(new User { Username = "admin", Password = "password-hash" });
        }

        private void GivenApiKeyRequest(AppSubsystem subsystem)
        {
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("ApiKey", "true") }, "API Key"))
            };

            Subject.ControllerContext = new ControllerContext { HttpContext = context };

            Mocker.GetMock<ISubsystemAccessor>().SetupGet(a => a.Subsystem).Returns(subsystem);
        }

        [Test]
        public void get_should_not_leak_series_key_to_movie_key()
        {
            GivenApiKeyRequest(AppSubsystem.Movies);

            var resource = Subject.GetHostConfig();

            resource.ApiKey.Should().Be("********");
            resource.Password.Should().Be("********");
            resource.SslCertPassword.Should().Be("********");
            resource.ProxyPassword.Should().Be("********");
            resource.MovieApiKey.Should().Be(MovieKey);
        }

        [Test]
        public void get_should_not_leak_movie_key_to_series_key()
        {
            GivenApiKeyRequest(AppSubsystem.Series);

            var resource = Subject.GetHostConfig();

            resource.MovieApiKey.Should().Be("********");
            resource.ApiKey.Should().Be(SeriesKey);
        }

        [Test]
        public void movie_key_should_not_be_able_to_change_the_series_key()
        {
            GivenApiKeyRequest(AppSubsystem.Movies);

            var resource = Subject.GetHostConfig();
            resource.ApiKey = "attacker-controlled";

            Subject.Invoking(s => s.SaveHostConfig(resource))
                   .Should().Throw<ForbiddenException>();
        }

        [Test]
        public void movie_key_should_not_be_able_to_change_authentication()
        {
            GivenApiKeyRequest(AppSubsystem.Movies);

            var resource = Subject.GetHostConfig();
            resource.AuthenticationMethod = AuthenticationType.Forms;
            resource.Username = "attacker";
            resource.Password = "hunter2";

            Subject.Invoking(s => s.SaveHostConfig(resource))
                   .Should().Throw<ForbiddenException>();
        }

        [Test]
        public void movie_key_should_be_able_to_rotate_its_own_key()
        {
            GivenApiKeyRequest(AppSubsystem.Movies);

            var resource = Subject.GetHostConfig();
            resource.MovieApiKey = "new-movie-key";

            Subject.Invoking(s => s.SaveHostConfig(resource))
                   .Should().NotThrow();

            Mocker.GetMock<IConfigFileProvider>().Verify(
                c => c.SaveConfigDictionary(It.Is<System.Collections.Generic.Dictionary<string, object>>(
                    d => (string)d["MovieApiKey"] == "new-movie-key")),
                Times.Once);
        }

        [Test]
        public void movie_key_should_be_able_to_change_non_sensitive_settings()
        {
            GivenApiKeyRequest(AppSubsystem.Movies);

            var resource = Subject.GetHostConfig();
            resource.LogLevel = "debug";

            Subject.Invoking(s => s.SaveHostConfig(resource))
                   .Should().NotThrow();
        }

        [Test]
        public void series_key_should_be_able_to_change_authentication()
        {
            GivenApiKeyRequest(AppSubsystem.Series);

            var resource = Subject.GetHostConfig();
            resource.AuthenticationMethod = AuthenticationType.Forms;
            resource.Username = "admin";
            resource.Password = "password-hash";

            Subject.Invoking(s => s.SaveHostConfig(resource))
                   .Should().NotThrow();
        }

        [Test]
        public void masked_secrets_should_not_be_persisted_over_the_stored_values()
        {
            GivenApiKeyRequest(AppSubsystem.Movies);

            var resource = Subject.GetHostConfig();

            Subject.SaveHostConfig(resource);

            Mocker.GetMock<IConfigFileProvider>().Verify(
                c => c.SaveConfigDictionary(It.Is<System.Collections.Generic.Dictionary<string, object>>(
                    d => (string)d["ApiKey"] == SeriesKey &&
                         (string)d["SslCertPassword"] != "********" &&
                         (string)d["ProxyPassword"] != "********")),
                Times.Once);
        }
    }
}
