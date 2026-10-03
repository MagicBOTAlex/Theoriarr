using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using DryIoc.ImTools;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Validation;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.IndexerTests.NewznabTests
{
    [TestFixture]
    public class NewznabFixture : CoreTest<Newznab>
    {
        private NewznabCapabilities _caps;

        [SetUp]
        public void Setup()
        {
            Subject.Definition = new IndexerDefinition()
                {
                    Id = 5,
                    Name = "Newznab",
                    Settings = new NewznabSettings()
                        {
                            BaseUrl = "http://indexer.local/",
                            Categories = new int[] { 1 }
                        }
                };

            _caps = new NewznabCapabilities();
            Mocker.GetMock<INewznabCapabilitiesProvider>()
                .Setup(v => v.GetCapabilities(It.IsAny<NewznabSettings>()))
                .Returns(_caps);
        }

        [Test]
        public async Task should_fetch_movie_recent_feed_alongside_tv_feed_for_mixed_categories()
        {
            Subject.Definition.Settings = new NewznabSettings()
                {
                    BaseUrl = "http://indexer.local/",
                    Categories = new[] { 5030 },
                    AnimeCategories = Array.Empty<int>(),
                    MovieCategories = new[] { 2040 },
                    ApiKey = "abcd"
                };

            _caps.SupportedTvSearchParameters = new[] { "q", "season", "ep" };
            _caps.SupportedMovieSearchParameters = new[] { "q", "imdbid" };

            const string tvFeed = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<rss version=\"2.0\" xmlns:newznab=\"http://www.newznab.com/DTD/2010/feeds/attributes/\"><channel><title>Prowlarr</title><item>" +
                "<title>White.Collar.S03E05.720p.HDTV.X264-DIMENSION</title>" +
                "<pubDate>Mon, 15 May 2017 19:15:56 +0000</pubDate>" +
                "<guid isPermaLink=\"true\">tv-release</guid>" +
                "<comments>https://indexer.local/details/tv</comments>" +
                "<link>https://indexer.local/download/tv</link>" +
                "<size>1183105773</size><category>5030</category>" +
                "<enclosure url=\"https://indexer.local/download/tv.nzb\" type=\"application/x-nzb\" />" +
                "<newznab:attr name=\"category\" value=\"5030\" />" +
                "</item></channel></rss>";

            const string movieFeed = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                "<rss version=\"2.0\" xmlns:newznab=\"http://www.newznab.com/DTD/2010/feeds/attributes/\"><channel><title>Prowlarr</title><item>" +
                "<title>Some.Movie.2020.1080p.WEB-DL.DD5.1.H.264-GROUP</title>" +
                "<pubDate>Mon, 15 May 2017 20:15:56 +0000</pubDate>" +
                "<guid isPermaLink=\"true\">movie-release</guid>" +
                "<comments>https://indexer.local/details/movie</comments>" +
                "<link>https://indexer.local/download/movie</link>" +
                "<size>4567890123</size><category>2040</category>" +
                "<enclosure url=\"https://indexer.local/download/movie.nzb\" type=\"application/x-nzb\" />" +
                "<newznab:attr name=\"category\" value=\"2040\" />" +
                "<newznab:attr name=\"tmdbid\" value=\"11\" />" +
                "</item></channel></rss>";

            var requestedUris = new List<string>();

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                .Callback<HttpRequest, CancellationToken>((request, _) => requestedUris.Add(request.Url.FullUri))
                .ReturnsAsync((HttpRequest request, CancellationToken _) =>
                    new HttpResponse(request, new HttpHeader(), request.Url.FullUri.Contains("t=movie") ? movieFeed : tvFeed));

            var releases = await Subject.FetchRecent();

            requestedUris.Should().Contain(u => u.Contains("t=tvsearch"));
            requestedUris.Should().Contain(u => u.Contains("t=movie"));

            releases.Should().Contain(r => r.Title == "White.Collar.S03E05.720p.HDTV.X264-DIMENSION");
            releases.Should().Contain(r => r.Title == "Some.Movie.2020.1080p.WEB-DL.DD5.1.H.264-GROUP");
        }

        [Test]
        public async Task should_parse_recent_feed_from_newznab_nzb_su()
        {
            var recentFeed = ReadAllText(@"Files/Indexers/Newznab/newznab_nzb_su.xml");

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .ReturnsAsync((HttpRequest r, CancellationToken _) => new HttpResponse(r, new HttpHeader(), recentFeed));

            var releases = await Subject.FetchRecent();

            releases.Should().HaveCount(100);

            var releaseInfo = releases.First();

            releaseInfo.Title.Should().Be("White.Collar.S03E05.720p.HDTV.X264-DIMENSION");
            releaseInfo.DownloadProtocol.Should().Be(DownloadProtocol.Usenet);
            releaseInfo.DownloadUrl.Should().Be("http://nzb.su/getnzb/24967ef4c2e26296c65d3bbfa97aa8fe.nzb&i=37292&r=xxx");
            releaseInfo.InfoUrl.Should().Be("http://nzb.su/details/24967ef4c2e26296c65d3bbfa97aa8fe");
            releaseInfo.CommentUrl.Should().Be("http://nzb.su/details/24967ef4c2e26296c65d3bbfa97aa8fe#comments");
            releaseInfo.IndexerId.Should().Be(Subject.Definition.Id);
            releaseInfo.Indexer.Should().Be(Subject.Definition.Name);
            releaseInfo.PublishDate.Should().Be(DateTime.Parse("2012/02/27 16:09:39"));
            releaseInfo.Size.Should().Be(1183105773);
        }

        [Test]
        public async Task should_parse_recent_feed_from_newznab_animetosho()
        {
            var recentFeed = ReadAllText(@"Files/Indexers/Torznab/torznab_animetosho.xml");

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .ReturnsAsync((HttpRequest r, CancellationToken _) => new HttpResponse(r, new HttpHeader(), recentFeed));

            var releases = await Subject.FetchRecent();

            releases.Should().HaveCount(1);

            releases.First().Should().BeOfType<ReleaseInfo>();
            var releaseInfo = releases.First() as ReleaseInfo;

            releaseInfo.Title.Should().Be("[HorribleSubs] Frame Arms Girl - 07 [720p].mkv");
            releaseInfo.DownloadProtocol.Should().Be(DownloadProtocol.Usenet);
            releaseInfo.DownloadUrl.Should().Be("http://storage.localhost/nzb/123452.nzb");
            releaseInfo.InfoUrl.Should().Be("https://localhost/view/horriblesubs-frame-arms-girl-07-720p-mkv.123452");
            releaseInfo.CommentUrl.Should().Be("https://localhost/view/horriblesubs-frame-arms-girl-07-720p-mkv.123452");
            releaseInfo.Indexer.Should().Be(Subject.Definition.Name);
            releaseInfo.PublishDate.Should().Be(DateTime.Parse("Mon, 15 May 2017 19:15:56 +0000").ToUniversalTime());
            releaseInfo.Size.Should().Be(473987489);
            releaseInfo.TvdbId.Should().Be(0);
            releaseInfo.TvRageId.Should().Be(0);
        }

        [Test]
        public async Task should_use_size_element_when_enclosure_length_is_missing()
        {
            var recentFeed = ReadAllText(@"Files/Indexers/Newznab/newznab_prowlarr.xml");

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .ReturnsAsync((HttpRequest r, CancellationToken _) => new HttpResponse(r, new HttpHeader(), recentFeed));

            var releases = await Subject.FetchRecent();

            releases.Should().HaveCount(1);

            var release = releases.Single();

            release.Size.Should().Be(4567890123);
            release.IndexerFlags.Should().HaveFlag(IndexerFlags.Internal);
        }

        [Test]
        public void should_use_best_pagesize_reported_by_caps()
        {
            _caps.MaxPageSize = 30;
            _caps.DefaultPageSize = 25;

            Subject.PageSize.Should().Be(30);
        }

        [Test]
        public void should_not_use_pagesize_over_100_even_if_reported_in_caps()
        {
            _caps.MaxPageSize = 250;
            _caps.DefaultPageSize = 25;

            Subject.PageSize.Should().Be(100);
        }

        [Test]
        public async Task should_record_indexer_failure_if_caps_throw()
        {
            var request = new HttpRequest("http://my.indexer.com");
            var response = new HttpResponse(request, new HttpHeader(), Array.Empty<byte>(), (HttpStatusCode)429);
            response.Headers["Retry-After"] = "300";

            Mocker.GetMock<INewznabCapabilitiesProvider>()
                .Setup(v => v.GetCapabilities(It.IsAny<NewznabSettings>()))
                .Throws(new TooManyRequestsException(request, response));

            _caps.MaxPageSize = 30;
            _caps.DefaultPageSize = 25;

            var releases = await Subject.FetchRecent();

            releases.Should().BeEmpty();

            Mocker.GetMock<IIndexerStatusService>()
                  .Verify(v => v.RecordFailure(It.IsAny<int>(), TimeSpan.FromMinutes(5.0)), Times.Once());

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public async Task should_back_off_when_recent_feed_reports_error_429()
        {
            const string rateLimited = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><error code=\"429\" description=\"Request limit reached\"/>";

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .ReturnsAsync((HttpRequest r, CancellationToken _) => new HttpResponse(r, new HttpHeader(), rateLimited));

            var releases = await Subject.FetchRecent();

            releases.Should().BeEmpty();

            Mocker.GetMock<IIndexerStatusService>()
                  .Verify(v => v.RecordFailure(It.IsAny<int>(), TimeSpan.FromHours(1)), Times.Once());

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public async Task should_record_failure_when_recent_feed_reports_error_410()
        {
            const string gone = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><error code=\"410\" description=\"Gone\"/>";

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .ReturnsAsync((HttpRequest r, CancellationToken _) => new HttpResponse(r, new HttpHeader(), gone));

            var releases = await Subject.FetchRecent();

            releases.Should().BeEmpty();

            Mocker.GetMock<IIndexerStatusService>()
                  .Verify(v => v.RecordFailure(It.IsAny<int>()), Times.Once());

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public async Task should_parse_languages()
        {
            var recentFeed = ReadAllText(@"Files/Indexers/Newznab/newznab_language.xml");

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .ReturnsAsync((HttpRequest r, CancellationToken _) => new HttpResponse(r, new HttpHeader(), recentFeed));

            var releases = await Subject.FetchRecent();

            releases.Should().HaveCount(100);

            releases[0].Languages.Should().BeEquivalentTo(new[] { Language.English, Language.Japanese });
            releases[1].Languages.Should().BeEquivalentTo(new[] { Language.English, Language.Spanish });
            releases[2].Languages.Should().BeEquivalentTo(new[] { Language.French });
        }

        [Test]
        public void should_treat_empty_recent_feed_as_warning()
        {
            const string emptyFeed = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><rss version=\"2.0\"><channel><title>empty</title></channel></rss>";

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .ReturnsAsync((HttpRequest r, CancellationToken _) => new HttpResponse(r, new HttpHeader(), emptyFeed));

            var result = new NzbDroneValidationResult(Subject.Test());

            result.IsValid.Should().BeTrue();
            result.HasWarnings.Should().BeTrue();
        }

        [Test]
        public void should_report_invalid_api_key_for_unauthorized_recent_feed()
        {
            var request = new HttpRequest("http://indexer.local/api?t=tvsearch");
            var response = new HttpResponse(request, new HttpHeader(), Array.Empty<byte>(), HttpStatusCode.Unauthorized);

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .ThrowsAsync(new HttpException(request, response));

            var result = new NzbDroneValidationResult(Subject.Test());

            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(f => f.PropertyName == "ApiKey");
        }

        [TestCase("no custom attributes")]
        [TestCase("prematch=1 attribute", IndexerFlags.Scene)]
        [TestCase("haspretime=1 attribute", IndexerFlags.Scene)]
        [TestCase("prematch=0 attribute")]
        [TestCase("haspretime=0 attribute")]
        [TestCase("nuked=1 attribute", IndexerFlags.Nuked)]
        [TestCase("nuked=0 attribute")]
        [TestCase("prematch=1 and nuked=1 attributes", IndexerFlags.Scene, IndexerFlags.Nuked)]
        [TestCase("haspretime=0 and nuked=0 attributes")]
        [TestCase("subs=eng", IndexerFlags.Subtitles)]
        [TestCase("subs=''")]
        [TestCase("tag=internal attribute", IndexerFlags.Internal)]
        [TestCase("tag=scene attribute", IndexerFlags.Scene)]
        [TestCase("tag=freeleech attribute", IndexerFlags.Freeleech)]
        [TestCase("tag=halfleech attribute", IndexerFlags.Halfleech)]
        [TestCase("tag=doubleupload attribute", IndexerFlags.DoubleUpload)]
        public async Task should_parse_indexer_flags(string releaseGuid, params IndexerFlags[] indexerFlags)
        {
            var feed = ReadAllText(@"Files/Indexers/Newznab/newznab_indexerflags.xml");

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.Is<HttpRequest>(v => v.Method == HttpMethod.Get)))
                .ReturnsAsync((HttpRequest r, CancellationToken _) => new HttpResponse(r, new HttpHeader(), feed));

            var releases = await Subject.FetchRecent();

            var release = releases.Should().ContainSingle(r => r.Guid == releaseGuid).Subject;

            indexerFlags.ForEach(f => release.IndexerFlags.Should().HaveFlag(f));
        }
    }
}
