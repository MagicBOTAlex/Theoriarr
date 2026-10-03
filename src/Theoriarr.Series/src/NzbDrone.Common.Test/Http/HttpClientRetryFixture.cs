using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Http;
using NzbDrone.Common.Http.Dispatchers;
using NzbDrone.Common.TPL;
using NzbDrone.Test.Common;
using HttpClient = NzbDrone.Common.Http.HttpClient;

namespace NzbDrone.Common.Test.Http
{
    [TestFixture]
    public class HttpClientRetryFixture : TestBase<HttpClient>
    {
        [SetUp]
        public void SetUp()
        {
            Mocker.SetConstant<ICacheManager>(Mocker.Resolve<CacheManager>());
            Mocker.SetConstant<IRateLimitService>(Mocker.Resolve<RateLimitService>());
            Mocker.SetConstant<IEnumerable<IHttpRequestInterceptor>>(Array.Empty<IHttpRequestInterceptor>());
        }

        [Test]
        public async Task should_retry_until_success()
        {
            var attempts = 0;

            Mocker.GetMock<IHttpDispatcher>()
                .Setup(d => d.GetResponseAsync(It.IsAny<HttpRequest>(), It.IsAny<CookieContainer>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((HttpRequest request, CookieContainer _, CancellationToken _) =>
                {
                    attempts++;

                    if (attempts < 3)
                    {
                        return new HttpResponse(request, new HttpHeader(), "down", HttpStatusCode.ServiceUnavailable);
                    }

                    return new HttpResponse(request, new HttpHeader(), "{\"ok\":true}", HttpStatusCode.OK);
                });

            var request = new HttpRequest("http://example.invalid/thing")
            {
                RetryPolicy = new HttpRetryPolicy
                {
                    Enabled = true,
                    MaxRetries = 5,
                    BaseDelay = TimeSpan.FromMilliseconds(1),
                    MaxDelay = TimeSpan.FromMilliseconds(5),
                    Jitter = 0,
                },
            };

            var response = await Subject.ExecuteAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            attempts.Should().Be(3);
        }

        [Test]
        public async Task should_retry_rate_limited_responses()
        {
            var attempts = 0;

            Mocker.GetMock<IHttpDispatcher>()
                .Setup(d => d.GetResponseAsync(It.IsAny<HttpRequest>(), It.IsAny<CookieContainer>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((HttpRequest request, CookieContainer _, CancellationToken _) =>
                {
                    attempts++;

                    if (attempts < 2)
                    {
                        return new HttpResponse(request, new HttpHeader(), "slow down", (HttpStatusCode)429);
                    }

                    return new HttpResponse(request, new HttpHeader(), "ok", HttpStatusCode.OK);
                });

            var request = new HttpRequest("http://example.invalid/thing")
            {
                RetryPolicy = new HttpRetryPolicy
                {
                    Enabled = true,
                    MaxRetries = 3,
                    BaseDelay = TimeSpan.FromMilliseconds(1),
                    MaxDelay = TimeSpan.FromMilliseconds(5),
                    Jitter = 0,
                },
            };

            var response = await Subject.ExecuteAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            attempts.Should().Be(2);
        }

        [Test]
        public void should_not_retry_without_a_policy()
        {
            var attempts = 0;

            Mocker.GetMock<IHttpDispatcher>()
                .Setup(d => d.GetResponseAsync(It.IsAny<HttpRequest>(), It.IsAny<CookieContainer>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((HttpRequest request, CookieContainer _, CancellationToken _) =>
                {
                    attempts++;
                    return new HttpResponse(request, new HttpHeader(), "down", HttpStatusCode.ServiceUnavailable);
                });

            var request = new HttpRequest("http://example.invalid/thing");

            Assert.ThrowsAsync<HttpException>(async () => await Subject.ExecuteAsync(request));
            attempts.Should().Be(1);
        }

        [Test]
        public async Task should_not_retry_post_after_response_received()
        {
            var attempts = 0;

            Mocker.GetMock<IHttpDispatcher>()
                .Setup(d => d.GetResponseAsync(It.IsAny<HttpRequest>(), It.IsAny<CookieContainer>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((HttpRequest request, CookieContainer _, CancellationToken _) =>
                {
                    attempts++;
                    return new HttpResponse(request, new HttpHeader(), "down", HttpStatusCode.ServiceUnavailable);
                });

            var request = new HttpRequest("http://example.invalid/thing")
            {
                Method = HttpMethod.Post,
                RetryPolicy = new HttpRetryPolicy
                {
                    Enabled = true,
                    MaxRetries = 5,
                    BaseDelay = TimeSpan.FromMilliseconds(1),
                    MaxDelay = TimeSpan.FromMilliseconds(5),
                    Jitter = 0,
                },
            };

            Assert.ThrowsAsync<HttpException>(async () => await Subject.ExecuteAsync(request));
            attempts.Should().Be(1);
        }

        [Test]
        public async Task should_retry_from_original_request_not_redirected_url()
        {
            var urls = new List<string>();

            Mocker.GetMock<IHttpDispatcher>()
                .Setup(d => d.GetResponseAsync(It.IsAny<HttpRequest>(), It.IsAny<CookieContainer>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((HttpRequest request, CookieContainer _, CancellationToken _) =>
                {
                    urls.Add(request.Url.FullUri);

                    return urls.Count switch
                    {
                        1 => new HttpResponse(request, new HttpHeader { { "Location", "/other" } }, "moved", HttpStatusCode.Found),
                        2 => new HttpResponse(request, new HttpHeader(), "down", HttpStatusCode.ServiceUnavailable),
                        _ => new HttpResponse(request, new HttpHeader(), "ok", HttpStatusCode.OK),
                    };
                });

            var request = new HttpRequest("http://example.invalid/thing")
            {
                AllowAutoRedirect = true,
                RetryPolicy = new HttpRetryPolicy
                {
                    Enabled = true,
                    MaxRetries = 3,
                    BaseDelay = TimeSpan.FromMilliseconds(1),
                    MaxDelay = TimeSpan.FromMilliseconds(5),
                    Jitter = 0,
                },
            };

            var response = await Subject.ExecuteAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            urls.Should().Equal("http://example.invalid/thing", "http://example.invalid/other", "http://example.invalid/thing");
        }

        [Test]
        public async Task should_strip_authorization_on_cross_origin_redirect()
        {
            var seenAuthorization = new List<string>();
            var seenCredentials = new List<ICredentials>();

            Mocker.GetMock<IHttpDispatcher>()
                .Setup(d => d.GetResponseAsync(It.IsAny<HttpRequest>(), It.IsAny<CookieContainer>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((HttpRequest request, CookieContainer _, CancellationToken _) =>
                {
                    seenAuthorization.Add(request.Headers["Authorization"]);
                    seenCredentials.Add(request.Credentials);

                    if (request.Url.Host == "first.invalid")
                    {
                        return new HttpResponse(request, new HttpHeader { { "Location", "http://second.invalid/thing" } }, "moved", HttpStatusCode.Found);
                    }

                    return new HttpResponse(request, new HttpHeader(), "ok", HttpStatusCode.OK);
                });

            var request = new HttpRequest("http://first.invalid/thing")
            {
                AllowAutoRedirect = true,
                Credentials = new BasicNetworkCredential("user", "pass"),
            };
            request.Headers.Add("Authorization", "Bearer secret");

            var response = await Subject.ExecuteAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            seenAuthorization[0].Should().Be("Bearer secret");
            seenAuthorization[1].Should().BeNull();
            seenCredentials[0].Should().NotBeNull();
            seenCredentials[1].Should().BeNull();
        }

        [Test]
        public void should_refuse_https_to_http_downgrade_redirect()
        {
            Mocker.GetMock<IHttpDispatcher>()
                .Setup(d => d.GetResponseAsync(It.IsAny<HttpRequest>(), It.IsAny<CookieContainer>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((HttpRequest request, CookieContainer _, CancellationToken _) =>
                    new HttpResponse(request, new HttpHeader { { "Location", "http://second.invalid/thing" } }, "moved", HttpStatusCode.Found));

            var request = new HttpRequest("https://first.invalid/thing") { AllowAutoRedirect = true };

            Assert.ThrowsAsync<WebException>(async () => await Subject.ExecuteAsync(request));
        }

        [Test]
        public void should_refuse_non_http_redirect()
        {
            Mocker.GetMock<IHttpDispatcher>()
                .Setup(d => d.GetResponseAsync(It.IsAny<HttpRequest>(), It.IsAny<CookieContainer>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((HttpRequest request, CookieContainer _, CancellationToken _) =>
                    new HttpResponse(request, new HttpHeader { { "Location", "ftp://second.invalid/thing" } }, "moved", HttpStatusCode.Found));

            var request = new HttpRequest("http://first.invalid/thing") { AllowAutoRedirect = true };

            Assert.ThrowsAsync<WebException>(async () => await Subject.ExecuteAsync(request));
        }

        [Test]
        public async Task should_not_throw_when_redirect_has_no_location()
        {
            Mocker.GetMock<IHttpDispatcher>()
                .Setup(d => d.GetResponseAsync(It.IsAny<HttpRequest>(), It.IsAny<CookieContainer>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((HttpRequest request, CookieContainer _, CancellationToken _) =>
                    new HttpResponse(request, new HttpHeader(), "moved", HttpStatusCode.Found));

            var request = new HttpRequest("http://first.invalid/thing") { AllowAutoRedirect = true };

            var response = await Subject.ExecuteAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Found);
        }

        [Test]
        public async Task should_use_first_location_when_duplicate()
        {
            var urls = new List<string>();

            Mocker.GetMock<IHttpDispatcher>()
                .Setup(d => d.GetResponseAsync(It.IsAny<HttpRequest>(), It.IsAny<CookieContainer>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((HttpRequest request, CookieContainer _, CancellationToken _) =>
                {
                    urls.Add(request.Url.FullUri);

                    if (urls.Count == 1)
                    {
                        var headers = new HttpHeader();
                        headers.Add("Location", "http://second.invalid/first");
                        headers.Add("Location", "http://second.invalid/second");
                        return new HttpResponse(request, headers, "moved", HttpStatusCode.Found);
                    }

                    return new HttpResponse(request, new HttpHeader(), "ok", HttpStatusCode.OK);
                });

            var request = new HttpRequest("http://first.invalid/thing") { AllowAutoRedirect = true };

            var response = await Subject.ExecuteAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            urls[1].Should().Be("http://second.invalid/first");
        }
    }
}
