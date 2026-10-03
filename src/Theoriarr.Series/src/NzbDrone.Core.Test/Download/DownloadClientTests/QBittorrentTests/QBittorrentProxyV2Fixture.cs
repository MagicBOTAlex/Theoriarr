using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
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
    public class QBittorrentProxyV2Fixture : CoreTest<QBittorrentProxyV2>
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
                Port = 443,
                UseSsl = true,
                Username = "admin",
                Password = "pass"
            };

            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => _requests.Add(r))
                  .Returns<HttpRequest>(r => new HttpResponse(r, new HttpHeader(), "2.14.1"));
        }

        private void GivenResponse(string content, HttpStatusCode statusCode = HttpStatusCode.OK, HttpHeader headers = null)
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => _requests.Add(r))
                  .Returns<HttpRequest>(r => new HttpResponse(r, headers ?? new HttpHeader(), content, statusCode));
        }

        private IEnumerable<HttpRequest> ApiRequests => _requests.Where(r => !r.Url.FullUri.Contains("/auth/"));

        [TestCase(80, false)]
        [TestCase(443, true)]
        [TestCase(8080, false)]
        public void should_not_send_origin_or_referer_headers(int port, bool useSsl)
        {
            _settings.Port = port;
            _settings.UseSsl = useSsl;

            Subject.IsApiSupported(_settings);

            _requests.Should().NotBeEmpty();

            var request = _requests.First();

            request.Headers.ContainsKey("Origin").Should().BeFalse();
            request.Headers.ContainsKey("Referer").Should().BeFalse();
        }

        [Test]
        public void should_return_false_when_webapi_version_is_not_a_version()
        {
            GivenResponse("<html>not qBittorrent</html>");

            Subject.IsApiSupported(_settings).Should().BeFalse();
        }

        [Test]
        public void should_return_false_when_api_endpoint_returns_not_found()
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
            GivenResponse("2.14.1", HttpStatusCode.Found);

            Subject.IsApiSupported(_settings).Should().BeFalse();

            _requests.Should().NotBeEmpty();
            _requests.Should().OnlyContain(r => !r.AllowAutoRedirect);
        }

        [Test]
        public void should_return_true_when_api_version_is_a_version()
        {
            GivenResponse("2.14.1");

            Subject.IsApiSupported(_settings).Should().BeTrue();
        }

        [TestCase(HttpStatusCode.Forbidden)]
        [TestCase(HttpStatusCode.Unauthorized)]
        public void should_return_true_when_api_endpoint_requires_authentication(HttpStatusCode statusCode)
        {
            GivenResponse(string.Empty, statusCode);

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
        public void should_send_share_limit_action_and_mode()
        {
            _settings.Username = null;
            _settings.Password = null;

            var seedConfiguration = new TorrentSeedConfiguration
            {
                Ratio = 1.5,
                SeedTime = TimeSpan.FromHours(1)
            };

            Subject.SetTorrentSeedingConfiguration("HASH", seedConfiguration, _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/torrents/setShareLimits"));
            var summary = request.ContentSummary;

            summary.Should().Contain("shareLimitAction=Default");
            summary.Should().Contain("shareLimitsMode=Default");
            summary.Should().Contain("ratioLimit=1.5");
            summary.Should().Contain("seedingTimeLimit=60");
            summary.Should().Contain("inactiveSeedingTimeLimit=-2");
        }

        [Test]
        public void should_preserve_suppressed_status_codes_on_reauthentication_retry()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => _requests.Add(r))
                  .Returns<HttpRequest>(r =>
                  {
                      if (r.Url.FullUri.Contains("/auth/login"))
                      {
                          var headers = new HttpHeader();
                          headers["Set-Cookie"] = "SID=abc; path=/";
                          return new HttpResponse(r, headers, "Ok.");
                      }

                      if (r.Url.FullUri.Contains("/auth/logout"))
                      {
                          return new HttpResponse(r, new HttpHeader(), string.Empty);
                      }

                      return new HttpResponse(r, new HttpHeader(), string.Empty, HttpStatusCode.Unauthorized);
                  });

            Assert.Throws<DownloadClientAuthenticationException>(() => Subject.GetVersion(_settings));

            var apiRequests = _requests.Where(r => r.Url.FullUri.Contains("/app/version")).ToList();

            apiRequests.Should().HaveCount(2);
            apiRequests.Should().OnlyContain(r => r.SuppressHttpErrorStatusCodes.Contains(HttpStatusCode.Unauthorized));
        }

        [Test]
        public void should_logout_before_reauthenticating()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Callback<HttpRequest>(r => _requests.Add(r))
                  .Returns<HttpRequest>(r =>
                  {
                      if (r.Url.FullUri.Contains("/auth/login"))
                      {
                          var headers = new HttpHeader();
                          headers["Set-Cookie"] = "SID=abc; path=/";
                          return new HttpResponse(r, headers, "Ok.");
                      }

                      if (r.Url.FullUri.Contains("/app/version"))
                      {
                          return new HttpResponse(r, new HttpHeader(), string.Empty, HttpStatusCode.Unauthorized);
                      }

                      return new HttpResponse(r, new HttpHeader(), string.Empty);
                  });

            Assert.Throws<DownloadClientAuthenticationException>(() => Subject.GetVersion(_settings));

            _requests.Should().Contain(r => r.Url.FullUri.Contains("/auth/logout"));
        }

        [Test]
        public void should_back_off_after_repeated_failed_logins()
        {
            GivenResponse("Fails.");

            Assert.Throws<DownloadClientAuthenticationException>(() => Subject.GetVersion(_settings));

            var loginRequests = _requests.Count(r => r.Url.FullUri.Contains("/auth/login"));
            loginRequests.Should().Be(1);

            var exception = Assert.Throws<DownloadClientAuthenticationException>(() => Subject.GetVersion(_settings));
            exception.Message.Should().Contain("temporarily blocked");

            _requests.Count(r => r.Url.FullUri.Contains("/auth/login")).Should().Be(1);
        }

        [TestCase("2.2", false)]
        [TestCase("2.3", true)]
        [TestCase("2.3.0", true)]
        [TestCase("2.4", true)]
        [TestCase("2.11", true)]
        public void should_send_add_tags_only_when_api_version_supports_it(string apiVersion, bool expected)
        {
            _settings.Username = null;
            _settings.Password = null;

            GivenResponse(apiVersion);

            Subject.AddTags("HASH", new[] { "tag1", "tag2" }, _settings);

            var addTagsRequests = _requests.Where(r => r.Url.FullUri.Contains("/torrents/addTags")).ToList();

            addTagsRequests.Should().HaveCount(expected ? 1 : 0);

            if (expected)
            {
                var summary = addTagsRequests.Single().ContentSummary;

                summary.Should().Contain("hashes=HASH");
                summary.Should().Contain($"tags={Uri.EscapeDataString("tag1,tag2")}");
            }
        }

        [TestCase("2.10", QBittorrentState.Start, false)]
        [TestCase("2.11", QBittorrentState.Start, true)]
        [TestCase("2.11.0", QBittorrentState.Start, true)]
        [TestCase("2.4", QBittorrentState.Start, false)]
        [TestCase("2.11", QBittorrentState.Stop, true)]
        [TestCase("2.10", QBittorrentState.Stop, false)]
        public void should_send_correct_paused_parameter_for_api_version(string apiVersion, QBittorrentState state, bool stopped)
        {
            _settings.Username = null;
            _settings.Password = null;
            _settings.InitialState = (int)state;

            GivenResponse(apiVersion);

            Subject.AddTorrentFromUrl("magnet:?xt=urn:btih:HASH", null, "tv-sonarr", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/torrents/add") && !r.Url.FullUri.Contains("/torrents/addTags"));
            var summary = request.ContentSummary;

            if (stopped)
            {
                summary.Should().Contain($"stopped={state == QBittorrentState.Stop}");
                summary.Should().NotContain("paused=");
            }
            else
            {
                summary.Should().Contain($"paused={state == QBittorrentState.Stop}");
                summary.Should().NotContain("stopped=");
            }
        }

        [Test]
        public void should_send_add_torrent_form_parameters()
        {
            _settings.Username = null;
            _settings.Password = null;
            _settings.InitialState = (int)QBittorrentState.Start;
            _settings.SequentialOrder = true;
            _settings.FirstAndLast = true;
            _settings.ContentLayout = (int)QBittorrentContentLayout.Subfolder;

            GivenResponse("2.14.1");

            Subject.AddTorrentFromUrl("magnet:?xt=urn:btih:HASH", null, "tv-sonarr", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/torrents/add") && !r.Url.FullUri.Contains("/torrents/addTags"));
            var summary = request.ContentSummary;

            summary.Should().Contain("stopped=False");
            summary.Should().Contain("category=tv-sonarr");
            summary.Should().Contain("sequentialDownload=True");
            summary.Should().Contain("firstLastPiecePrio=True");
            summary.Should().Contain("contentLayout=Subfolder");
        }

        [TestCase(QBittorrentContentLayout.Default, null)]
        [TestCase(QBittorrentContentLayout.Original, "contentLayout=Original")]
        [TestCase(QBittorrentContentLayout.Subfolder, "contentLayout=Subfolder")]
        public void should_send_content_layout_when_not_default(QBittorrentContentLayout layout, string expected)
        {
            _settings.Username = null;
            _settings.Password = null;
            _settings.InitialState = (int)QBittorrentState.Start;
            _settings.ContentLayout = (int)layout;

            GivenResponse("2.14.1");

            Subject.AddTorrentFromUrl("magnet:?xt=urn:btih:HASH", null, "tv-sonarr", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/torrents/add") && !r.Url.FullUri.Contains("/torrents/addTags"));
            var summary = request.ContentSummary;

            if (expected == null)
            {
                summary.Should().NotContain("contentLayout=");
            }
            else
            {
                summary.Should().Contain(expected);
            }
        }

        [TestCase(true, "deleteFiles=true")]
        [TestCase(false, "deleteFiles=false")]
        public void should_send_hashes_and_delete_files_when_removing_torrent(bool removeData, string expected)
        {
            _settings.Username = null;
            _settings.Password = null;

            Subject.RemoveTorrent("HASH", removeData, _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/torrents/delete"));
            var summary = request.ContentSummary;

            summary.Should().Contain("hashes=HASH");
            summary.Should().Contain(expected);
        }

        [Test]
        public void should_use_hash_query_param_for_torrent_properties()
        {
            _settings.Username = null;
            _settings.Password = null;

            GivenResponse("{}");

            Subject.GetTorrentProperties("HASH", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/torrents/properties"));

            request.Url.FullUri.Should().Contain("hash=HASH");
            request.Url.FullUri.Should().NotContain("hashes=HASH");
        }

        [Test]
        public void should_report_correct_api_version_when_api_key_authentication_fails()
        {
            _settings.Username = null;
            _settings.Password = null;
            _settings.ApiKey = "apikey";

            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Throws(new HttpException(new HttpResponse(new HttpRequest("http://localhost/"), new HttpHeader(), Array.Empty<byte>(), HttpStatusCode.Forbidden)));

            var exception = Assert.Throws<DownloadClientAuthenticationException>(() => Subject.GetVersion(_settings));

            exception.Message.Should().Contain("qBittorrent 5.2.0");
            exception.Message.Should().Contain("Web API 2.15.1");
            exception.Message.Should().NotContain("2.14.0");
        }

        [Test]
        public void should_send_urls_form_parameter_when_adding_torrent_from_url()
        {
            _settings.Username = null;
            _settings.Password = null;
            _settings.InitialState = (int)QBittorrentState.ForceStart;

            GivenResponse("2.14.1");

            Subject.AddTorrentFromUrl("magnet:?xt=urn:btih:HASH", null, "tv-sonarr", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/torrents/add") && !r.Url.FullUri.Contains("/torrents/addTags"));
            var summary = request.ContentSummary;

            summary.Should().Contain($"urls={Uri.EscapeDataString("magnet:?xt=urn:btih:HASH")}");
        }

        [Test]
        public void should_send_torrent_file_as_multipart_form_upload()
        {
            _settings.Username = null;
            _settings.Password = null;
            _settings.InitialState = (int)QBittorrentState.ForceStart;

            var fileContent = new byte[] { 0x64, 0x38, 0x3A, 0x61, 0x6E, 0x6E, 0x6F, 0x75, 0x6E, 0x63, 0x65 };

            Subject.AddTorrentFromFile("test.torrent", fileContent, null, "tv-sonarr", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/torrents/add") && !r.Url.FullUri.Contains("/torrents/addTags"));

            request.Headers.ContentType.Should().StartWith("multipart/form-data");

            var summary = request.ContentSummary;

            summary.Should().Contain($"torrents=test.torrent ({fileContent.Length} bytes)");
            summary.Should().Contain("category=tv-sonarr");

            var body = Encoding.UTF8.GetString(request.ContentData);

            body.Should().Contain("name=\"torrents\"");
            body.Should().Contain("filename=\"test.torrent\"");
        }

        [Test]
        public void should_send_seeding_limits_when_adding_torrent_with_seed_configuration()
        {
            _settings.Username = null;
            _settings.Password = null;
            _settings.InitialState = (int)QBittorrentState.ForceStart;

            var seedConfiguration = new TorrentSeedConfiguration
            {
                Ratio = 1.5,
                SeedTime = TimeSpan.FromHours(1)
            };

            GivenResponse("2.14.1");

            Subject.AddTorrentFromUrl("magnet:?xt=urn:btih:HASH", seedConfiguration, "tv-sonarr", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/torrents/add") && !r.Url.FullUri.Contains("/torrents/addTags"));
            var summary = request.ContentSummary;

            summary.Should().Contain("ratioLimit=1.5");
            summary.Should().Contain("seedingTimeLimit=60");
            summary.Should().NotContain("inactiveSeedingTimeLimit=");
        }

        [Test]
        public void should_send_hashes_and_category_when_setting_label()
        {
            _settings.Username = null;
            _settings.Password = null;

            Subject.SetTorrentLabel("HASH", "tv-sonarr", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/torrents/setCategory"));
            var summary = request.ContentSummary;

            summary.Should().Contain("hashes=HASH");
            summary.Should().Contain("category=tv-sonarr");
        }

        [TestCase(true, "value=true")]
        [TestCase(false, "value=false")]
        public void should_send_hashes_and_value_when_setting_force_start(bool enabled, string expected)
        {
            _settings.Username = null;
            _settings.Password = null;

            Subject.SetForceStart("HASH", enabled, _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/torrents/setForceStart"));
            var summary = request.ContentSummary;

            summary.Should().Contain("hashes=HASH");
            summary.Should().Contain(expected);
        }

        [Test]
        public void should_send_hashes_when_moving_torrent_to_top()
        {
            _settings.Username = null;
            _settings.Password = null;

            Subject.MoveTorrentToTopInQueue("HASH", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/torrents/topPrio"));
            var summary = request.ContentSummary;

            summary.Should().Contain("hashes=HASH");
        }

        [Test]
        public void should_swallow_conflict_when_moving_torrent_to_top()
        {
            _settings.Username = null;
            _settings.Password = null;

            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Throws(new HttpException(new HttpResponse(new HttpRequest("http://localhost/"), new HttpHeader(), Array.Empty<byte>(), HttpStatusCode.Conflict)));

            Assert.DoesNotThrow(() => Subject.MoveTorrentToTopInQueue("HASH", _settings));
        }

        [Test]
        public void should_send_hashes_and_location_when_setting_location()
        {
            _settings.Username = null;
            _settings.Password = null;

            Subject.SetTorrentLocation("HASH", "/downloads/correct", _settings);

            var request = _requests.Single(r => r.Url.FullUri.Contains("/torrents/setLocation"));
            var summary = request.ContentSummary;

            summary.Should().Contain("hashes=HASH");
            summary.Should().Contain("location=");
        }
    }
}
