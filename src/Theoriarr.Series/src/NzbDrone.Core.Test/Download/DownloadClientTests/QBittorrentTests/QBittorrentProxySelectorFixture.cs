using System;
using System.Net;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Download.Clients.QBittorrent;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download.DownloadClientTests.QBittorrentTests
{
    [TestFixture]
    public class QBittorrentProxySelectorFixture : CoreTest<QBittorrentProxySelector>
    {
        [Test]
        public void should_resolve_versions_independently_per_url_base()
        {
            Mocker.GetMock<IHttpClient>()
                  .Setup(s => s.Execute(It.IsAny<HttpRequest>()))
                  .Returns<HttpRequest>(r =>
                  {
                      var uri = r.Url.FullUri;

                      if (uri.Contains("/a/") && uri.EndsWith("webapiVersion"))
                      {
                          return new HttpResponse(r, new HttpHeader(), "2.14.1");
                      }

                      if (uri.Contains("/b/") && uri.EndsWith("version/api"))
                      {
                          return new HttpResponse(r, new HttpHeader(), "14");
                      }

                      return new HttpResponse(r, new HttpHeader(), string.Empty, HttpStatusCode.NotFound);
                  });

            var settingsA = new QBittorrentSettings { Host = "127.0.0.1", Port = 8080, UrlBase = "/a" };
            var settingsB = new QBittorrentSettings { Host = "127.0.0.1", Port = 8080, UrlBase = "/b" };

            Subject.GetApiVersion(settingsA, false).Should().Be(new Version(2, 14, 1));
            Subject.GetApiVersion(settingsB, false).Should().Be(new Version(1, 14));
        }
    }
}
