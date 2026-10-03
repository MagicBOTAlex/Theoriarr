using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http.Dispatchers;
using NzbDrone.Common.TPL;

namespace NzbDrone.Common.Http
{
    public interface IHttpClient
    {
        HttpResponse Execute(HttpRequest request);
        void DownloadFile(string url, string fileName);
        HttpResponse Get(HttpRequest request);
        HttpResponse<T> Get<T>(HttpRequest request)
            where T : new();
        HttpResponse Head(HttpRequest request);
        HttpResponse Post(HttpRequest request);
        HttpResponse<T> Post<T>(HttpRequest request)
            where T : new();

        Task<HttpResponse> ExecuteAsync(HttpRequest request, CancellationToken cancellationToken = default);
        Task DownloadFileAsync(string url, string fileName, CancellationToken cancellationToken = default);
        Task<HttpResponse> GetAsync(HttpRequest request, CancellationToken cancellationToken = default);
        Task<HttpResponse<T>> GetAsync<T>(HttpRequest request, CancellationToken cancellationToken = default)
            where T : new();
        Task<HttpResponse> HeadAsync(HttpRequest request, CancellationToken cancellationToken = default);
        Task<HttpResponse> PostAsync(HttpRequest request, CancellationToken cancellationToken = default);
        Task<HttpResponse<T>> PostAsync<T>(HttpRequest request, CancellationToken cancellationToken = default)
            where T : new();
    }

    public class HttpClient : IHttpClient
    {
        private const int MaxRedirects = 5;

        private readonly Logger _logger;
        private readonly IRateLimitService _rateLimitService;
        private readonly ICached<CookieContainer> _cookieContainerCache;
        private readonly List<IHttpRequestInterceptor> _requestInterceptors;
        private readonly IHttpDispatcher _httpDispatcher;

        public HttpClient(IEnumerable<IHttpRequestInterceptor> requestInterceptors,
            ICacheManager cacheManager,
            IRateLimitService rateLimitService,
            IHttpDispatcher httpDispatcher,
            Logger logger)
        {
            _requestInterceptors = requestInterceptors.ToList();
            _rateLimitService = rateLimitService;
            _httpDispatcher = httpDispatcher;
            _logger = logger;

            _cookieContainerCache = cacheManager.GetCache<CookieContainer>(typeof(HttpClient));
        }

        public virtual async Task<HttpResponse> ExecuteAsync(HttpRequest request, CancellationToken cancellationToken = default)
        {
            var policy = request.RetryPolicy;

            if (policy == null || !policy.Enabled || policy.MaxRetries <= 0)
            {
                return await ExecuteRequestWithRedirectsAsync(request, cancellationToken);
            }

            var maxAttempts = policy.MaxRetries + 1;

            _logger.Debug("Retry policy active for {0}: up to {1} attempt(s), base delay {2:0}ms, max delay {3:0}ms, jitter {4:P0}", request.Url, maxAttempts, policy.BaseDelay.TotalMilliseconds, policy.MaxDelay.TotalMilliseconds, policy.Jitter);

            for (var attempt = 1; ; attempt++)
            {
                HttpResponse response = null;
                Exception error = null;

                var attemptWatch = Stopwatch.StartNew();

                // Snapshot the request for each attempt. Redirects and forced-GET handling mutate
                // the request in place, so reusing the original would make the next attempt hit the
                // redirected URL (possibly with credentials stripped or a downgraded method).
                var attemptRequest = request.Clone();

                try
                {
                    response = await ExecuteRequestWithRedirectsAsync(attemptRequest, cancellationToken);
                }
                catch (Exception ex) when (attempt < maxAttempts && IsRetryable(ex, cancellationToken) && CanRetry(attemptRequest, ex))
                {
                    error = ex;
                }

                attemptWatch.Stop();

                if (error == null)
                {
                    if (!IsRetryableStatus((int)response.StatusCode) || attempt >= maxAttempts || !CanRetry(attemptRequest))
                    {
                        _logger.Debug("Request to {0} returned {1} on attempt {2}/{3} after {4}ms{5}", attemptRequest.Url, (int)response.StatusCode, attempt, maxAttempts, attemptWatch.ElapsedMilliseconds, DescribeCacheHints(response));
                        return response;
                    }
                }
                else
                {
                    _logger.Debug("Request to {0} failed on attempt {1}/{2} after {3}ms: {4}", attemptRequest.Url, attempt, maxAttempts, attemptWatch.ElapsedMilliseconds, error.Message);
                }

                var delay = ComputeRetryDelay(policy, attempt, error, response);

                if (error != null)
                {
                    _logger.Warn(error, "Request to {0} failed (attempt {1}/{2}, {3}ms); retrying in {4:0}ms", attemptRequest.Url, attempt, maxAttempts, attemptWatch.ElapsedMilliseconds, delay.TotalMilliseconds);
                }
                else
                {
                    _logger.Warn("Request to {0} returned {1} (attempt {2}/{3}, {4}ms); retrying in {5:0}ms", attemptRequest.Url, (int)response.StatusCode, attempt, maxAttempts, attemptWatch.ElapsedMilliseconds, delay.TotalMilliseconds);
                }

                await Task.Delay(delay, cancellationToken);
            }
        }

        private async Task<HttpResponse> ExecuteRequestWithRedirectsAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            var cookieContainer = InitializeRequestCookies(request);

            var response = await ExecuteRequestAsync(request, cookieContainer, cancellationToken);

            if (request.AllowAutoRedirect && response.HasHttpRedirect)
            {
                var autoRedirectChain = new List<string> { request.Url.ToString() };

                do
                {
                    var location = GetRedirectLocation(response);

                    if (location == null)
                    {
                        _logger.Warn("Redirect response from {0} did not include a usable Location header; not following.", request.Url);
                        break;
                    }

                    HttpUri target;

                    try
                    {
                        target = request.Url + new HttpUri(location);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(ex, "Redirect response from {0} contained an invalid Location header; not following.", request.Url);
                        break;
                    }

                    var currentOrigin = GetOrigin(request.Url);
                    var targetOrigin = GetOrigin(target);

                    if (!IsHttpScheme(target.Scheme))
                    {
                        throw new WebException($"Refusing to follow redirect from {request.Url} to non-http(s) URL {target}", WebExceptionStatus.ProtocolError);
                    }

                    if (IsHttps(request.Url.Scheme) && !IsHttps(target.Scheme))
                    {
                        throw new WebException($"Refusing to follow insecure https to http redirect from {request.Url} to {target}", WebExceptionStatus.ProtocolError);
                    }

                    if (!string.Equals(currentOrigin, targetOrigin, StringComparison.OrdinalIgnoreCase))
                    {
                        // Never forward credentials or cookies to a different origin.
                        StripSensitiveHeaders(request);
                    }

                    request.Url = target;
                    autoRedirectChain.Add(request.Url.ToString());

                    _logger.Trace("Redirected to {0}", request.Url);

                    if (autoRedirectChain.Count > MaxRedirects)
                    {
                        throw new WebException($"Too many automatic redirections were attempted for {autoRedirectChain.Join(" -> ")}", WebExceptionStatus.ProtocolError);
                    }

                    // 302 or 303 should default to GET on redirect even if POST on original
                    if (RequestRequiresForceGet(response.StatusCode, response.Request.Method))
                    {
                        request.Method = HttpMethod.Get;
                        request.ContentData = null;
                        request.ContentSummary = null;
                    }

                    response = await ExecuteRequestAsync(request, cookieContainer, cancellationToken);
                }
                while (response.HasHttpRedirect);
            }

            if (response.HasHttpRedirect && !RuntimeInfo.IsProduction)
            {
                _logger.Error("Server requested a redirect to [{0}] while in developer mode. Update the request URL to avoid this redirect.", response.Headers["Location"]);
            }

            if (!request.SuppressHttpError && response.HasHttpError && (request.SuppressHttpErrorStatusCodes == null || !request.SuppressHttpErrorStatusCodes.Contains(response.StatusCode)))
            {
                if (request.LogHttpError)
                {
                    _logger.Warn("HTTP Error - {0}", response);
                }

                if ((int)response.StatusCode == 429)
                {
                    throw new TooManyRequestsException(request, response);
                }
                else
                {
                    throw new HttpException(request, response);
                }
            }

            return response;
        }

        private static bool IsRetryable(Exception ex, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            return ex switch
            {
                TooManyRequestsException => true,
                HttpException http => http.Response != null && IsRetryableStatus((int)http.Response.StatusCode),
                WebException => true,
                HttpRequestException => true,
                TaskCanceledException => true,
                OperationCanceledException => true,
                _ => false,
            };
        }

        private static bool IsRetryableStatus(int statusCode)
        {
            return statusCode == 408 || statusCode == 429 || statusCode >= 500;
        }

        private static bool CanRetry(HttpRequest request, Exception error = null)
        {
            // A response was received (error == null), so the request definitely reached the
            // server and must only be replayed when the method is idempotent. Exceptions may be
            // retried for any method when the failure is provably pre-send.
            if (IsIdempotentMethod(request.Method))
            {
                return true;
            }

            return error != null && IsProvablyPreSend(error);
        }

        private static bool IsIdempotentMethod(HttpMethod method)
        {
            return method == HttpMethod.Get ||
                   method == HttpMethod.Head ||
                   method == HttpMethod.Options ||
                   method == HttpMethod.Trace ||
                   method == HttpMethod.Put ||
                   method == HttpMethod.Delete;
        }

        private static bool IsProvablyPreSend(Exception ex)
        {
            return ex switch
            {
                HttpRequestException http => http.HttpRequestError is
                    HttpRequestError.NameResolutionError or
                    HttpRequestError.ConnectionError or
                    HttpRequestError.SecureConnectionError or
                    HttpRequestError.ProxyTunnelError,
                WebException web => web.Status is
                    WebExceptionStatus.NameResolutionFailure or
                    WebExceptionStatus.ConnectFailure or
                    WebExceptionStatus.ProxyNameResolutionFailure,
                _ => false,
            };
        }

        private string GetRedirectLocation(HttpResponse response)
        {
            var values = response.Headers.GetValues("Location");

            if (values == null || values.Length == 0)
            {
                return null;
            }

            if (values.Length > 1)
            {
                _logger.Warn("Redirect response from {0} included multiple Location headers; using the first.", response.Request.Url);
            }

            foreach (var value in values)
            {
                if (value.IsNotNullOrWhiteSpace())
                {
                    return value.Trim();
                }
            }

            return null;
        }

        private static bool IsHttpScheme(string scheme)
        {
            return string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHttps(string scheme)
        {
            return string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetOrigin(HttpUri uri)
        {
            var scheme = (uri.Scheme ?? string.Empty).ToLowerInvariant();
            var port = uri.Port ?? DefaultPort(scheme);

            return $"{scheme}://{uri.Host?.ToLowerInvariant()}:{port}";
        }

        private static int DefaultPort(string scheme)
        {
            return scheme switch
            {
                "https" => 443,
                "http" => 80,
                _ => 0,
            };
        }

        private static void StripSensitiveHeaders(HttpRequest request)
        {
            request.Headers.Remove("Authorization");
            request.Headers.Remove("Proxy-Authorization");
            request.Headers.Remove("Cookie");
            request.Credentials = null;
        }

        // Surfaces Providarr's per-response cache hints (headers) in the debug log so it is
        // obvious whether a metadata response was served from cache, by which provider, etc.
        private static string DescribeCacheHints(HttpResponse response)
        {
            var headers = response?.Headers;

            if (headers == null)
            {
                return string.Empty;
            }

            var hints = new List<string>();

            AddCacheHint(headers, hints, "x-providarr-cache");
            AddCacheHint(headers, hints, "x-providarr-provider");
            AddCacheHint(headers, hints, "x-providarr-replay");
            AddCacheHint(headers, hints, "x-providarr-source");

            return hints.Count == 0 ? string.Empty : $" [{string.Join(", ", hints)}]";
        }

        private static void AddCacheHint(HttpHeader headers, ICollection<string> hints, string name)
        {
            if (!headers.ContainsKey(name))
            {
                return;
            }

            var text = headers[name]?.ToString();

            if (!string.IsNullOrWhiteSpace(text))
            {
                hints.Add($"{name}={text}");
            }
        }

        private static TimeSpan ComputeRetryDelay(HttpRetryPolicy policy, int attempt, Exception error, HttpResponse response)
        {
            TimeSpan delay;

            if (error is TooManyRequestsException { RetryAfter: { Ticks: > 0 } } tooMany)
            {
                delay = tooMany.RetryAfter;
            }
            else if (response != null &&
                     (int)response.StatusCode == 429 &&
                     response.Headers.ContainsKey("Retry-After") &&
                     int.TryParse(response.Headers["Retry-After"].ToString(), out var seconds))
            {
                delay = TimeSpan.FromSeconds(seconds);
            }
            else
            {
                var exponent = Math.Min(attempt - 1, 10);
                delay = TimeSpan.FromMilliseconds(policy.BaseDelay.TotalMilliseconds * Math.Pow(2, exponent));
            }

            if (delay > policy.MaxDelay)
            {
                delay = policy.MaxDelay;
            }

            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            if (policy.Jitter > 0)
            {
                var factor = 1.0 + (((Random.Shared.NextDouble() * 2) - 1) * policy.Jitter);
                delay = TimeSpan.FromMilliseconds(Math.Max(0, delay.TotalMilliseconds * factor));
            }

            return delay;
        }

        public HttpResponse Execute(HttpRequest request)
        {
            return ExecuteAsync(request).GetAwaiter().GetResult();
        }

        private static bool RequestRequiresForceGet(HttpStatusCode statusCode, HttpMethod requestMethod)
        {
            return statusCode switch
            {
                HttpStatusCode.Moved or HttpStatusCode.Found or HttpStatusCode.MultipleChoices => requestMethod == HttpMethod.Post,
                HttpStatusCode.SeeOther => requestMethod != HttpMethod.Get && requestMethod != HttpMethod.Head,
                _ => false,
            };
        }

        private async Task<HttpResponse> ExecuteRequestAsync(HttpRequest request, CookieContainer cookieContainer, CancellationToken cancellationToken = default)
        {
            foreach (var interceptor in _requestInterceptors)
            {
                request = interceptor.PreRequest(request);
            }

            if (request.RateLimit != TimeSpan.Zero)
            {
                await _rateLimitService.WaitAndPulseAsync(request.Url.Host, request.RateLimitKey, request.RateLimit);
            }

            _logger.Trace(request);

            var stopWatch = Stopwatch.StartNew();

            var response = await _httpDispatcher.GetResponseAsync(request, cookieContainer, cancellationToken);

            HandleResponseCookies(response, cookieContainer);

            stopWatch.Stop();

            _logger.Trace("{0} ({1} ms)", response, stopWatch.ElapsedMilliseconds);

            foreach (var interceptor in _requestInterceptors)
            {
                response = interceptor.PostResponse(response);
            }

            if (request.LogResponseContent && response.ResponseData != null)
            {
                _logger.Trace("Response content ({0} bytes): {1}", response.ResponseData.Length, response.Content);
            }

            return response;
        }

        private CookieContainer InitializeRequestCookies(HttpRequest request)
        {
            lock (_cookieContainerCache)
            {
                var sourceContainer = new CookieContainer();

                var presistentContainer = _cookieContainerCache.Get("container", () => new CookieContainer());
                var persistentCookies = presistentContainer.GetCookies((Uri)request.Url);
                sourceContainer.Add(persistentCookies);

                if (request.Cookies.Count != 0)
                {
                    foreach (var pair in request.Cookies)
                    {
                        Cookie cookie;
                        if (pair.Value == null)
                        {
                            cookie = new Cookie(pair.Key, "", "/")
                            {
                                Expires = DateTime.Now.AddDays(-1)
                            };
                        }
                        else
                        {
                            cookie = new Cookie(pair.Key, pair.Value, "/")
                            {
                                // Use Now rather than UtcNow to work around Mono cookie expiry bug.
                                // See https://gist.github.com/ta264/7822b1424f72e5b4c961
                                Expires = DateTime.Now.AddHours(1)
                            };
                        }

                        sourceContainer.Add((Uri)request.Url, cookie);

                        if (request.StoreRequestCookie)
                        {
                            presistentContainer.Add((Uri)request.Url, cookie);
                        }
                    }
                }

                return sourceContainer;
            }
        }

        private void HandleResponseCookies(HttpResponse response, CookieContainer container)
        {
            foreach (Cookie cookie in container.GetAllCookies())
            {
                cookie.Expired = true;
            }

            var cookieHeaders = response.GetCookieHeaders();

            if (cookieHeaders.Empty())
            {
                return;
            }

            AddCookiesToContainer(response.Request.Url, cookieHeaders, container);

            if (response.Request.StoreResponseCookie)
            {
                lock (_cookieContainerCache)
                {
                    var persistentCookieContainer = _cookieContainerCache.Get("container", () => new CookieContainer());

                    AddCookiesToContainer(response.Request.Url, cookieHeaders, persistentCookieContainer);
                }
            }
        }

        private void AddCookiesToContainer(HttpUri url, string[] cookieHeaders, CookieContainer container)
        {
            foreach (var cookieHeader in cookieHeaders)
            {
                try
                {
                    container.SetCookies((Uri)url, cookieHeader);
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Invalid cookie in {0}", url);
                }
            }
        }

        public async Task DownloadFileAsync(string url, string fileName, CancellationToken cancellationToken = default)
        {
            var fileNamePart = fileName + ".part";

            try
            {
                var fileInfo = new FileInfo(fileName);
                if (fileInfo.Directory != null && !fileInfo.Directory.Exists)
                {
                    fileInfo.Directory.Create();
                }

                _logger.Debug("Downloading [{0}] to [{1}]", url, fileName);

                var stopWatch = Stopwatch.StartNew();
                await using (var fileStream = new FileStream(fileNamePart, FileMode.Create, FileAccess.ReadWrite))
                {
                    var request = new HttpRequest(url);
                    request.AllowAutoRedirect = true;
                    request.ResponseStream = fileStream;
                    request.RequestTimeout = TimeSpan.FromSeconds(300);
                    var response = await GetAsync(request, cancellationToken);

                    if (response.Headers.ContentType != null && response.Headers.ContentType.Contains("text/html"))
                    {
                        throw new HttpException(request, response, "Site responded with html content.");
                    }
                }

                stopWatch.Stop();

                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }

                File.Move(fileNamePart, fileName);
                _logger.Debug("Downloading Completed. took {0:0}s", stopWatch.Elapsed.Seconds);
            }
            finally
            {
                if (File.Exists(fileNamePart))
                {
                    File.Delete(fileNamePart);
                }
            }
        }

        public void DownloadFile(string url, string fileName)
        {
            // https://docs.microsoft.com/en-us/archive/msdn-magazine/2015/july/async-programming-brownfield-async-development#the-thread-pool-hack
            Task.Run(() => DownloadFileAsync(url, fileName)).GetAwaiter().GetResult();
        }

        public Task<HttpResponse> GetAsync(HttpRequest request, CancellationToken cancellationToken = default)
        {
            request.Method = HttpMethod.Get;
            return ExecuteAsync(request, cancellationToken);
        }

        public HttpResponse Get(HttpRequest request)
        {
            return Task.Run(() => GetAsync(request)).GetAwaiter().GetResult();
        }

        public async Task<HttpResponse<T>> GetAsync<T>(HttpRequest request, CancellationToken cancellationToken = default)
            where T : new()
        {
            var response = await GetAsync(request, cancellationToken);
            CheckResponseContentType(response);
            return new HttpResponse<T>(response);
        }

        public HttpResponse<T> Get<T>(HttpRequest request)
            where T : new()
        {
            return Task.Run(() => GetAsync<T>(request)).GetAwaiter().GetResult();
        }

        public Task<HttpResponse> HeadAsync(HttpRequest request, CancellationToken cancellationToken = default)
        {
            request.Method = HttpMethod.Head;
            return ExecuteAsync(request, cancellationToken);
        }

        public HttpResponse Head(HttpRequest request)
        {
            return Task.Run(() => HeadAsync(request)).GetAwaiter().GetResult();
        }

        public Task<HttpResponse> PostAsync(HttpRequest request, CancellationToken cancellationToken = default)
        {
            request.Method = HttpMethod.Post;
            return ExecuteAsync(request, cancellationToken);
        }

        public HttpResponse Post(HttpRequest request)
        {
            return Task.Run(() => PostAsync(request)).GetAwaiter().GetResult();
        }

        public async Task<HttpResponse<T>> PostAsync<T>(HttpRequest request, CancellationToken cancellationToken = default)
            where T : new()
        {
            var response = await PostAsync(request, cancellationToken);
            CheckResponseContentType(response);
            return new HttpResponse<T>(response);
        }

        public HttpResponse<T> Post<T>(HttpRequest request)
            where T : new()
        {
            return Task.Run(() => PostAsync<T>(request)).GetAwaiter().GetResult();
        }

        private void CheckResponseContentType(HttpResponse response)
        {
            if (response.Headers.ContentType != null && response.Headers.ContentType.Contains("text/html"))
            {
                throw new UnexpectedHtmlContentException(response);
            }
        }
    }
}
