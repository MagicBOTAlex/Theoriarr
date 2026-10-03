using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Clients.QBittorrent;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download.DownloadClientTests.QBittorrentTests
{
    [TestFixture]
    public class QBittorrentProxyV1Fixture : CoreTest<QBittorrentProxyV1>
    {
        private QBittorrentSettings _settings;
        private List<HttpRequest> _requests;

        [SetUp]
        public void Setup()
        {
            _requests = new List<HttpRequest>();

            _settings = new QBittorrentSettings
            {
                Host = "127.0.0.1",
                Port = 8080,
                Username = "admin",
                Password = "pass"
            };

            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => _requests.Add(r))
                  .Returns<HttpRequest>(r => new HttpResponse(r, new HttpHeader(), "14"));
        }

        private void GivenResponse(string content, HttpStatusCode statusCode = HttpStatusCode.OK, HttpHeader headers = null)
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => _requests.Add(r))
                  .Returns<HttpRequest>(r => new HttpResponse(r, headers ?? new HttpHeader(), content, statusCode));
        }

        private void GivenVersion(string version, string commandResponse = "Ok.")
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => _requests.Add(r))
                  .Returns<HttpRequest>(r =>
                  {
                      if (r.Url.FullUri.Contains("/version/qbittorrent"))
                      {
                          return new HttpResponse(r, new HttpHeader(), version);
                      }

                      return new HttpResponse(r, new HttpHeader(), commandResponse);
                  });
        }

        [TestCase(HttpStatusCode.Forbidden)]
        [TestCase(HttpStatusCode.Unauthorized)]
        public void should_return_true_when_api_endpoint_requires_authentication(HttpStatusCode statusCode)
        {
            GivenResponse(string.Empty, statusCode);

            Subject.IsApiSupported(_settings).Should().BeTrue();
        }

        [Test]
        public void should_return_false_when_api_endpoint_is_not_found()
        {
            GivenResponse(string.Empty, HttpStatusCode.NotFound);

            Subject.IsApiSupported(_settings).Should().BeFalse();
        }

        [Test]
        public void should_return_false_when_api_endpoint_redirects()
        {
            GivenResponse(string.Empty, HttpStatusCode.Found);

            Subject.IsApiSupported(_settings).Should().BeFalse();
        }

        [Test]
        public void should_probe_api_endpoint_without_following_redirects()
        {
            // A redirect whose target happens to answer with a version must not be accepted as qBittorrent.
            GivenResponse("14", HttpStatusCode.Found);

            Subject.IsApiSupported(_settings).Should().BeFalse();

            _requests.Should().NotBeEmpty();
            _requests.Should().OnlyContain(r => !r.AllowAutoRedirect);
        }

        [Test]
        public void should_return_false_when_api_version_is_not_a_version()
        {
            GivenResponse("<html>not qBittorrent</html>");

            Subject.IsApiSupported(_settings).Should().BeFalse();
        }

        [Test]
        public void should_return_true_when_api_version_is_an_integer()
        {
            GivenResponse("14");

            Subject.IsApiSupported(_settings).Should().BeTrue();
        }

        [Test]
        public void should_throw_clean_exception_when_api_version_cannot_be_parsed()
        {
            _settings.Username = null;
            _settings.Password = null;

            GivenResponse("<html>not qBittorrent</html>");

            Assert.Throws<DownloadClientException>(() => Subject.GetApiVersion(_settings));
        }

        [Test]
        public void should_not_throw_when_move_to_top_is_rejected_because_queueing_is_disabled()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => _requests.Add(r))
                  .Returns<HttpRequest>(r =>
                  {
                      if (r.Url.FullUri.EndsWith("/login"))
                      {
                          var headers = new HttpHeader();
                          headers["Set-Cookie"] = "SID=abc; path=/";
                          return new HttpResponse(r, headers, "Ok.");
                      }

                      if (r.Url.FullUri.Contains("/command/topPrio"))
                      {
                          return new HttpResponse(r, new HttpHeader(), string.Empty, HttpStatusCode.Forbidden);
                      }

                      return new HttpResponse(r, new HttpHeader(), string.Empty);
                  });

            Subject.MoveTorrentToTopInQueue("HASH", _settings);

            _requests.Count(r => r.Url.FullUri.Contains("/command/topPrio")).Should().Be(2);
        }

        [Test]
        public void should_cache_authentication_separately_per_username()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => _requests.Add(r))
                  .Returns<HttpRequest>(r =>
                  {
                      if (r.Url.FullUri.EndsWith("/login"))
                      {
                          var headers = new HttpHeader();
                          headers["Set-Cookie"] = "SID=abc; path=/";
                          return new HttpResponse(r, headers, "Ok.");
                      }

                      return new HttpResponse(r, new HttpHeader(), "4.2.0");
                  });

            var settingsA = new QBittorrentSettings { Host = "127.0.0.1", Port = 8080, Username = "userA", Password = "pass" };
            var settingsB = new QBittorrentSettings { Host = "127.0.0.1", Port = 8080, Username = "userB", Password = "pass" };

            Subject.GetVersion(settingsA);
            Subject.GetVersion(settingsB);
            Subject.GetVersion(settingsA);

            _requests.Count(r => r.Url.FullUri.EndsWith("/login")).Should().Be(2);
        }

        [Test]
        public void should_not_store_request_cookie()
        {
            _settings.Username = null;
            _settings.Password = null;

            Subject.GetVersion(_settings);

            _requests.Should().NotBeEmpty();
            _requests.Should().OnlyContain(r => !r.StoreRequestCookie);
        }

        [Test]
        public void should_wrap_transport_exception_as_download_client_exception()
        {
            _settings.Username = null;
            _settings.Password = null;

            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Throws(new HttpRequestException("Connection refused"));

            Assert.Throws<DownloadClientException>(() => Subject.GetVersion(_settings));
        }

        [Test]
        public void should_handle_unauthorized_on_authenticated_request()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => _requests.Add(r))
                  .Returns<HttpRequest>(r =>
                  {
                      if (r.Url.FullUri.EndsWith("/login"))
                      {
                          var headers = new HttpHeader();
                          headers["Set-Cookie"] = "SID=abc; path=/";
                          return new HttpResponse(r, headers, "Ok.");
                      }

                      return new HttpResponse(r, new HttpHeader(), string.Empty, HttpStatusCode.Unauthorized);
                  });

            Assert.Throws<DownloadClientAuthenticationException>(() => Subject.GetVersion(_settings));

            var apiRequests = _requests.Where(r => r.Url.FullUri.Contains("/version/qbittorrent")).ToList();

            apiRequests.Should().HaveCount(2);
            apiRequests.Should().OnlyContain(r => r.SuppressHttpErrorStatusCodes.Contains(HttpStatusCode.Unauthorized));
        }

        [Test]
        public void should_back_off_after_repeated_failed_logins()
        {
            GivenResponse("Fails.");

            Assert.Throws<DownloadClientAuthenticationException>(() => Subject.GetVersion(_settings));

            var exception = Assert.Throws<DownloadClientAuthenticationException>(() => Subject.GetVersion(_settings));
            exception.Message.Should().Contain("temporarily blocked");

            _requests.Count(r => r.Url.FullUri.EndsWith("/login")).Should().Be(1);
        }

        [TestCase("3.2.4", null)]
        [TestCase("3.3.0", null)]
        [TestCase("3.3.1", "label=tv-sonarr")]
        [TestCase("3.3.4", "label=tv-sonarr")]
        [TestCase("3.3.5", "category=tv-sonarr")]
        [TestCase("4.0.4", "category=tv-sonarr")]
        public void should_send_category_or_label_for_add_from_url_according_to_version(string version, string expectedParameter)
        {
            _settings.Username = null;
            _settings.Password = null;

            GivenVersion(version);

            Subject.AddTorrentFromUrl("magnet:?xt=urn:btih:HASH", null, "tv-sonarr", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/command/download"));
            var summary = request.ContentSummary;

            summary.Should().Contain("urls=");

            if (expectedParameter == null)
            {
                summary.Should().NotContain("label=");
                summary.Should().NotContain("category=");
            }
            else
            {
                summary.Should().Contain(expectedParameter);
            }
        }

        [TestCase("3.2.4", QBittorrentState.Stop, false)]
        [TestCase("3.3.0", QBittorrentState.Stop, false)]
        [TestCase("3.3.5", QBittorrentState.Stop, true)]
        [TestCase("3.3.5", QBittorrentState.Start, true)]
        [TestCase("4.0.4", QBittorrentState.Stop, true)]
        public void should_send_lowercase_paused_for_add_from_url(string version, QBittorrentState state, bool pausedSupported)
        {
            _settings.Username = null;
            _settings.Password = null;
            _settings.InitialState = (int)state;

            GivenVersion(version);

            Subject.AddTorrentFromUrl("magnet:?xt=urn:btih:HASH", null, "tv-sonarr", _settings);

            var summary = _requests.Single(r => r.Url.FullUri.Contains("/command/download")).ContentSummary;

            if (!pausedSupported)
            {
                summary.Should().NotContain("paused=");
            }
            else if (state == QBittorrentState.Stop)
            {
                summary.Should().Contain("paused=true");
                summary.Should().NotContain("paused=True");
            }
            else
            {
                summary.Should().Contain("paused=false");
                summary.Should().NotContain("paused=False");
            }
        }

        [TestCase("3.2.4", null, false)]
        [TestCase("3.3.1", "label=tv-sonarr", false)]
        [TestCase("3.3.5", "category=tv-sonarr", true)]
        public void should_send_add_from_file_parameters_according_to_version(string version, string expectedParameter, bool pausedSupported)
        {
            _settings.Username = null;
            _settings.Password = null;
            _settings.InitialState = (int)QBittorrentState.Stop;

            GivenVersion(version);

            Subject.AddTorrentFromFile("file.torrent", new byte[] { 1, 2, 3 }, null, "tv-sonarr", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/command/upload"));
            var summary = request.ContentSummary;

            request.Headers.ContentType.Should().StartWith("multipart/form-data");
            summary.Should().Contain("torrents=file.torrent");

            if (expectedParameter == null)
            {
                summary.Should().NotContain("label=");
                summary.Should().NotContain("category=");
            }
            else
            {
                summary.Should().Contain(expectedParameter);
            }

            if (pausedSupported)
            {
                summary.Should().Contain("paused=true");
            }
            else
            {
                summary.Should().NotContain("paused=");
            }
        }

        [Test]
        public void should_cache_version_across_adds()
        {
            _settings.Username = null;
            _settings.Password = null;

            GivenVersion("3.3.1");

            Subject.AddTorrentFromUrl("magnet:?xt=urn:btih:ONE", null, "tv-sonarr", _settings);
            Subject.AddTorrentFromUrl("magnet:?xt=urn:btih:TWO", null, "tv-sonarr", _settings);

            _requests.Count(r => r.Url.FullUri.Contains("/version/qbittorrent")).Should().Be(1);
            _requests.Count(r => r.Url.FullUri.Contains("/command/download")).Should().Be(2);
        }

        [Test]
        public void should_send_category_as_label_and_category_query_params_for_get_torrents()
        {
            _settings.Username = null;
            _settings.Password = null;

            GivenResponse("[{\"hash\":\"HASH\",\"name\":\"Name\",\"category\":\"tv-sonarr\"}]");

            var torrents = Subject.GetTorrents("tv-sonarr", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/query/torrents"));

            request.Method.Should().Be(HttpMethod.Get);
            request.Url.FullUri.Should().Contain("label=tv-sonarr");
            request.Url.FullUri.Should().Contain("category=tv-sonarr");

            torrents.Should().HaveCount(1);
            torrents.Single().Hash.Should().Be("HASH");
            torrents.Single().Category.Should().Be("tv-sonarr");
        }

        [Test]
        public void should_not_send_label_or_category_query_params_when_no_category_for_get_torrents()
        {
            _settings.Username = null;
            _settings.Password = null;

            GivenResponse("[]");

            var torrents = Subject.GetTorrents(null, _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/query/torrents"));

            request.Method.Should().Be(HttpMethod.Get);
            request.Url.FullUri.Should().NotContain("label=");
            request.Url.FullUri.Should().NotContain("category=");

            torrents.Should().BeEmpty();
        }

        [Test]
        public void should_get_config_and_map_preferences_fields()
        {
            _settings.Username = null;
            _settings.Password = null;

            GivenResponse("{\"save_path\":\"/downloads\",\"max_ratio_enabled\":true,\"max_ratio\":1.5,\"max_seeding_time\":60,\"max_ratio_act\":1,\"queueing_enabled\":false,\"dht\":true}");

            var preferences = Subject.GetConfig(_settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/query/preferences"));

            request.Method.Should().Be(HttpMethod.Get);

            preferences.SavePath.Should().Be("/downloads");
            preferences.MaxRatioEnabled.Should().BeTrue();
            preferences.MaxRatio.Should().Be(1.5f);
            preferences.MaxSeedingTime.Should().Be(60);
            preferences.MaxRatioAction.Should().Be(QBittorrentMaxRatioAction.Remove);
            preferences.QueueingEnabled.Should().BeFalse();
            preferences.DhtEnabled.Should().BeTrue();
        }

        [Test]
        public void should_get_torrent_properties_from_hash_path()
        {
            _settings.Username = null;
            _settings.Password = null;

            GivenResponse("{\"hash\":\"HASH\",\"save_path\":\"/downloads\",\"seeding_time\":123}");

            var properties = Subject.GetTorrentProperties("HASH", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/query/propertiesGeneral/HASH"));

            request.Method.Should().Be(HttpMethod.Get);

            properties.Hash.Should().Be("HASH");
            properties.SavePath.Should().Be("/downloads");
            properties.SeedingTime.Should().Be(123);
        }

        [Test]
        public void should_get_torrent_files_from_hash_path()
        {
            _settings.Username = null;
            _settings.Password = null;

            GivenResponse("[{\"name\":\"file1.mkv\"},{\"name\":\"file2.mkv\"}]");

            var files = Subject.GetTorrentFiles("HASH", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/query/propertiesFiles/HASH"));

            request.Method.Should().Be(HttpMethod.Get);

            files.Select(f => f.Name).Should().Equal("file1.mkv", "file2.mkv");
        }

        [TestCase(false, "/command/delete")]
        [TestCase(true, "/command/deletePerm")]
        public void should_remove_torrent_with_hashes(bool removeData, string expectedPath)
        {
            _settings.Username = null;
            _settings.Password = null;

            Subject.RemoveTorrent("HASH", removeData, _settings);

            var request = _requests.Single(r => r.Url.FullUri.EndsWith(expectedPath));

            request.Method.Should().Be(HttpMethod.Post);
            request.ContentSummary.Should().Contain("hashes=HASH");
        }

        [Test]
        public void should_set_torrent_label_via_set_category_command()
        {
            _settings.Username = null;
            _settings.Password = null;

            Subject.SetTorrentLabel("HASH", "tv-sonarr", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/command/setCategory"));

            request.Method.Should().Be(HttpMethod.Post);
            request.ContentSummary.Should().Contain("hashes=HASH");
            request.ContentSummary.Should().Contain("category=tv-sonarr");

            _requests.Should().NotContain(r => r.Url.FullUri.Contains("/command/setLabel"));
        }

        [Test]
        public void should_fall_back_to_set_label_when_set_category_is_not_found()
        {
            _settings.Username = null;
            _settings.Password = null;

            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => _requests.Add(r))
                  .Returns<HttpRequest>(r =>
                  {
                      if (r.Url.FullUri.Contains("/command/setCategory"))
                      {
                          throw new HttpException(new HttpResponse(r, new HttpHeader(), string.Empty, HttpStatusCode.NotFound));
                      }

                      return new HttpResponse(r, new HttpHeader(), "Ok.");
                  });

            Subject.SetTorrentLabel("HASH", "tv-sonarr", _settings);

            var setCategory = _requests.Single(r => r.Url.FullUri.Contains("/command/setCategory"));

            setCategory.Method.Should().Be(HttpMethod.Post);
            setCategory.ContentSummary.Should().Contain("hashes=HASH");
            setCategory.ContentSummary.Should().Contain("category=tv-sonarr");

            var setLabel = _requests.Single(r => r.Url.FullUri.Contains("/command/setLabel"));

            setLabel.Method.Should().Be(HttpMethod.Post);
            setLabel.ContentSummary.Should().Contain("hashes=HASH");
            setLabel.ContentSummary.Should().Contain("label=tv-sonarr");
        }

        [Test]
        public void should_add_label_via_add_category_command()
        {
            _settings.Username = null;
            _settings.Password = null;

            Subject.AddLabel("tv-sonarr", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/command/addCategory"));

            request.Method.Should().Be(HttpMethod.Post);
            request.ContentSummary.Should().Contain("category=tv-sonarr");
        }

        [TestCase(true, "value=true")]
        [TestCase(false, "value=false")]
        public void should_set_force_start_with_lowercase_value(bool enabled, string expected)
        {
            _settings.Username = null;
            _settings.Password = null;

            Subject.SetForceStart("HASH", enabled, _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/command/setForceStart"));

            request.Method.Should().Be(HttpMethod.Post);
            request.ContentSummary.Should().Contain("hashes=HASH");
            request.ContentSummary.Should().Contain(expected);
            request.ContentSummary.Should().NotContain(enabled ? "value=True" : "value=False");
        }

        [Test]
        public void should_throw_not_supported_when_getting_labels()
        {
            Assert.Throws<NotSupportedException>(() => Subject.GetLabels(_settings));
        }

        [TestCase("v4.0.4", "4.0.4")]
        [TestCase("4.0.4", "4.0.4")]
        public void should_get_version_and_strip_leading_v(string response, string expected)
        {
            _settings.Username = null;
            _settings.Password = null;

            GivenResponse(response);

            Subject.GetVersion(_settings).Should().Be(expected);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/version/qbittorrent"));

            request.Method.Should().Be(HttpMethod.Get);
        }
    }
}
