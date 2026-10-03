using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.IndexerTests
{
    [TestFixture]
    public class HttpIndexerBaseFixture : TestBase<TestIndexer>
    {
        private Series _series;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew().Build();

            Subject.Definition = new IndexerDefinition
            {
                Name = "Test",
                Settings = new TestIndexerSettings { MultiLanguages = Array.Empty<int>() }
            };
        }

        private static IndexerPageableRequestChain TwoTierChain(string firstUrl, string secondUrl)
        {
            var chain = new IndexerPageableRequestChain();
            chain.Add(new[] { new IndexerRequest(firstUrl, HttpAccept.Rss) });
            chain.AddTier(new[] { new IndexerRequest(secondUrl, HttpAccept.Rss) });

            return chain;
        }

        private List<string> WithHttpClient()
        {
            var requestedUris = new List<string>();

            Mocker.GetMock<IHttpClient>()
                .Setup(o => o.ExecuteAsync(It.IsAny<HttpRequest>(), It.IsAny<CancellationToken>()))
                .Callback<HttpRequest, CancellationToken>((request, _) => requestedUris.Add(request.Url.FullUri))
                .ReturnsAsync((HttpRequest request, CancellationToken _) => new HttpResponse(request, new HttpHeader(), "<xml></xml>"));

            return requestedUris;
        }

        private void WithParser()
        {
            var parser = Mocker.GetMock<IParseIndexerResponse>();
            parser.Setup(s => s.ParseResponse(It.IsAny<IndexerResponse>()))
                .Returns(Builder<ReleaseInfo>.CreateListOfSize(1).Build());

            Subject._parser = parser.Object;
        }

        [Test]
        public async Task should_fetch_every_tier_of_a_recent_feed()
        {
            var generator = Mocker.GetMock<IIndexerRequestGenerator>();
            generator.Setup(s => s.GetRecentRequests())
                .Returns(TwoTierChain("http://my.feed.local/tv", "http://my.feed.local/movie"));
            Subject._requestGenerator = generator.Object;

            WithParser();
            var requestedUris = WithHttpClient();

            await Subject.FetchRecent();

            requestedUris.Should().HaveCount(2);
            requestedUris.Should().Contain("http://my.feed.local/tv");
            requestedUris.Should().Contain("http://my.feed.local/movie");
        }

        [Test]
        public void should_stop_at_the_first_search_tier_that_returns_results()
        {
            var generator = Mocker.GetMock<IIndexerRequestGenerator>();
            generator.Setup(s => s.GetSearchRequests(It.IsAny<SingleEpisodeSearchCriteria>()))
                .Returns(TwoTierChain("http://my.feed.local/primary", "http://my.feed.local/fallback"));
            Subject._requestGenerator = generator.Object;

            WithParser();
            var requestedUris = WithHttpClient();

            Subject.Fetch(new SingleEpisodeSearchCriteria
            {
                Series = _series,
                SceneTitles = new List<string> { _series.Title }
            });

            requestedUris.Should().ContainSingle();
            requestedUris.Should().Contain("http://my.feed.local/primary");
        }
    }
}
