using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;

namespace NzbDrone.Core.Download.Clients.QBittorrent
{
    // API https://github.com/qbittorrent/qBittorrent/wiki/Web-API-Documentation

    public class QBittorrentProxyV2 : IQBittorrentProxy
    {
        private static readonly TimeSpan AuthBackoffMinDelay = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan AuthBackoffMaxDelay = TimeSpan.FromMinutes(5);

        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;
        private readonly ICached<Dictionary<string, string>> _authCookieCache;
        private readonly ICached<AuthBackoffState> _authBackoffCache;

        private class AuthBackoffState
        {
            public int Count { get; set; }
            public DateTime LastFailure { get; set; }
        }

        public QBittorrentProxyV2(IHttpClient httpClient, ICacheManager cacheManager, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _authCookieCache = cacheManager.GetCache<Dictionary<string, string>>(GetType(), "authCookies");
            _authBackoffCache = cacheManager.GetCache<AuthBackoffState>(GetType(), "authBackoff");
        }

        public bool IsApiSupported(QBittorrentSettings settings)
        {
            // We can do the api test without having to authenticate since v3.2.0-v4.0.4 will return 404 on the request.
            // Do not follow redirects here: a 3xx means we did not reach the qBittorrent API directly and must
            // not be accepted as qBittorrent just because the redirect target happens to answer with a version.
            var request = BuildRequest(settings).Resource("/api/v2/app/webapiVersion").AllowRedirect(false);
            request.SuppressHttpError = true;

            try
            {
                var response = _httpClient.Execute(request.Build());

                // Version request will return 404 if it doesn't exist.
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return false;
                }

                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                {
                    return true;
                }

                // A redirect that wasn't followed (or a redirect loop) means we did not reach a qBittorrent API endpoint.
                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    return false;
                }

                if (response.HasHttpError)
                {
                    throw new DownloadClientException("Failed to connect to qBittorrent, check your settings.", new HttpException(response));
                }

                // Some other service can answer 200 with an html page; only claim support when the body is a version.
                return Version.TryParse(response.Content?.Trim(), out _);
            }
            catch (WebException ex)
            {
                throw new DownloadClientException("Failed to connect to qBittorrent, check your settings.", ex);
            }
            catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
            {
                throw new DownloadClientUnavailableException("Unable to connect to qBittorrent, certificate validation failed.", ex);
            }
            catch (AuthenticationException ex)
            {
                throw new DownloadClientUnavailableException("Unable to connect to qBittorrent, certificate validation failed.", ex);
            }
        }

        public Version GetApiVersion(QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/api/v2/app/webapiVersion");
            var content = ProcessRequest(request, settings);

            if (!Version.TryParse(content?.Trim(), out var version))
            {
                throw new DownloadClientException("Failed to determine qBittorrent API version, check your settings.");
            }

            return version;
        }

        public string GetVersion(QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/api/v2/app/version");
            var response = ProcessRequest(request, settings).TrimStart('v');

            // eg "4.2alpha"
            return response;
        }

        public QBittorrentPreferences GetConfig(QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/api/v2/app/preferences");
            var response = ProcessRequest<QBittorrentPreferences>(request, settings);

            return response;
        }

        public List<QBittorrentTorrent> GetTorrents(string category, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/api/v2/torrents/info");
            if (category.IsNotNullOrWhiteSpace())
            {
                request.AddQueryParam("category", category);
            }

            var response = ProcessRequest<List<QBittorrentTorrent>>(request, settings);

            return response;
        }

        public bool IsTorrentLoaded(string hash, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/api/v2/torrents/properties")
                                                .AddQueryParam("hash", hash);
            request.LogHttpError = false;

            try
            {
                ProcessRequest(request, settings);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public QBittorrentTorrentProperties GetTorrentProperties(string hash, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/api/v2/torrents/properties")
                                                .AddQueryParam("hash", hash);
            var response = ProcessRequest<QBittorrentTorrentProperties>(request, settings);

            return response;
        }

        public List<QBittorrentTorrentFile> GetTorrentFiles(string hash, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/api/v2/torrents/files")
                                                .AddQueryParam("hash", hash);
            var response = ProcessRequest<List<QBittorrentTorrentFile>>(request, settings);

            return response;
        }

        public void AddTorrentFromUrl(string torrentUrl, TorrentSeedConfiguration seedConfiguration, string category, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/api/v2/torrents/add")
                                                .Post()
                                                .AddFormParameter("urls", torrentUrl);

            AddTorrentDownloadFormParameters(request, category, settings);

            if (seedConfiguration != null)
            {
                AddTorrentSeedingFormParameters(request, seedConfiguration);
            }

            var result = ProcessRequest(request, settings);

            // Note: Older qbit versions returned nothing, so we can't do != "Ok." here.
            if (result == "Fails.")
            {
                throw new DownloadClientException("Download client failed to add torrent by url");
            }
        }

        public void AddTorrentFromFile(string fileName, byte[] fileContent, TorrentSeedConfiguration seedConfiguration, string category, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/api/v2/torrents/add")
                                                .Post()
                                                .AddFormUpload("torrents", fileName, fileContent);

            AddTorrentDownloadFormParameters(request, category, settings);

            if (seedConfiguration != null)
            {
                AddTorrentSeedingFormParameters(request, seedConfiguration);
            }

            var result = ProcessRequest(request, settings);

            // Note: Current qbit versions return nothing, so we can't do != "Ok." here.
            if (result == "Fails.")
            {
                throw new DownloadClientException("Download client failed to add torrent");
            }
        }

        public void RemoveTorrent(string hash, bool removeData, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/api/v2/torrents/delete")
                                                .Post()
                                                .AddFormParameter("hashes", hash)
                                                .AddFormParameter("deleteFiles", removeData ? "true" : "false");

            ProcessRequest(request, settings);
        }

        public void SetTorrentLabel(string hash, string label, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/api/v2/torrents/setCategory")
                                                .Post()
                                                .AddFormParameter("hashes", hash)
                                                .AddFormParameter("category", label);
            ProcessRequest(request, settings);
        }

        public void AddLabel(string label, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/api/v2/torrents/createCategory")
                                                .Post()
                                                .AddFormParameter("category", label);
            ProcessRequest(request, settings);
        }

        public Dictionary<string, QBittorrentLabel> GetLabels(QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/api/v2/torrents/categories");
            return Json.Deserialize<Dictionary<string, QBittorrentLabel>>(ProcessRequest(request, settings));
        }

        private void AddTorrentSeedingFormParameters(HttpRequestBuilder request, TorrentSeedConfiguration seedConfiguration, bool always = false)
        {
            var ratioLimit = seedConfiguration.Ratio.HasValue ? seedConfiguration.Ratio : -2;
            var seedingTimeLimit = seedConfiguration.SeedTime.HasValue ? (long)seedConfiguration.SeedTime.Value.TotalMinutes : -2;

            if (ratioLimit != -2 || always)
            {
                request.AddFormParameter("ratioLimit", ratioLimit);
            }

            if (seedingTimeLimit != -2 || always)
            {
                request.AddFormParameter("seedingTimeLimit", seedingTimeLimit);
            }

            if (always)
            {
                request.AddFormParameter("inactiveSeedingTimeLimit", -2);
            }
        }

        private void AddTorrentDownloadFormParameters(HttpRequestBuilder request, string category, QBittorrentSettings settings)
        {
            if (category.IsNotNullOrWhiteSpace())
            {
                request.AddFormParameter("category", category);
            }

            // Avoid extraneous API version check if initial state is ForceStart
            if ((QBittorrentState)settings.InitialState is QBittorrentState.Start or QBittorrentState.Stop)
            {
                var stoppedParameterName = GetApiVersion(settings) >= new Version(2, 11) ? "stopped" : "paused";

                // Note: ForceStart is handled by separate api call
                if ((QBittorrentState)settings.InitialState == QBittorrentState.Start)
                {
                    request.AddFormParameter(stoppedParameterName, false);
                }
                else if ((QBittorrentState)settings.InitialState == QBittorrentState.Stop)
                {
                    request.AddFormParameter(stoppedParameterName, true);
                }
            }

            if (settings.SequentialOrder)
            {
                request.AddFormParameter("sequentialDownload", true);
            }

            if (settings.FirstAndLast)
            {
                request.AddFormParameter("firstLastPiecePrio", true);
            }

            if ((QBittorrentContentLayout)settings.ContentLayout == QBittorrentContentLayout.Original)
            {
                request.AddFormParameter("contentLayout", "Original");
            }
            else if ((QBittorrentContentLayout)settings.ContentLayout == QBittorrentContentLayout.Subfolder)
            {
                request.AddFormParameter("contentLayout", "Subfolder");
            }
        }

        public void SetTorrentSeedingConfiguration(string hash, TorrentSeedConfiguration seedConfiguration, QBittorrentSettings settings)
        {
            // Newer qBittorrent requires shareLimitAction/shareLimitsMode. "Default"
            // keeps the torrent's existing share limit action and mode.
            var request = BuildRequest(settings).Resource("/api/v2/torrents/setShareLimits")
                                                .Post()
                                                .AddFormParameter("hashes", hash)
                                                .AddFormParameter("shareLimitAction", "Default")
                                                .AddFormParameter("shareLimitsMode", "Default");

            AddTorrentSeedingFormParameters(request, seedConfiguration, true);

            try
            {
                ProcessRequest(request, settings);
            }
            catch (DownloadClientException ex)
            {
                // setShareLimits was added in api v2.0.1 so catch it case of the unlikely event that someone has api v2.0
                if (ex.InnerException is HttpException httpException && httpException.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    return;
                }

                throw;
            }
        }

        public void MoveTorrentToTopInQueue(string hash, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/api/v2/torrents/topPrio")
                                                .Post()
                                                .AddFormParameter("hashes", hash);

            try
            {
                ProcessRequest(request, settings);
            }
            catch (DownloadClientException ex)
            {
                // qBittorrent rejects all Prio commands with 409: Conflict if Options -> BitTorrent -> Torrent Queueing is not enabled
                if (ex.InnerException is HttpException httpException && httpException.Response.StatusCode == HttpStatusCode.Conflict)
                {
                    return;
                }

                throw;
            }
        }

        public void SetForceStart(string hash, bool enabled, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/api/v2/torrents/setForceStart")
                                                .Post()
                                                .AddFormParameter("hashes", hash)
                                                .AddFormParameter("value", enabled ? "true" : "false");
            ProcessRequest(request, settings);
        }

        public void AddTags(string hash, IEnumerable<string> tags, QBittorrentSettings settings)
        {
            // addTags was added in API v2.3.0, older servers return 404.
            if (GetApiVersion(settings) < new Version(2, 3))
            {
                return;
            }

            var request = BuildRequest(settings).Resource("/api/v2/torrents/addTags")
                .Post()
                .AddFormParameter("hashes", hash)
                .AddFormParameter("tags", string.Join(",", tags));
            ProcessRequest(request, settings);
        }

        public void SetTorrentLocation(string hash, string location, QBittorrentSettings settings)
        {
            // setLocation was added in API v2.1 (qBittorrent 4.1.0).
            var request = BuildRequest(settings).Resource("/api/v2/torrents/setLocation")
                                                .Post()
                                                .AddFormParameter("hashes", hash)
                                                .AddFormParameter("location", location);
            ProcessRequest(request, settings);
        }

        private static HttpRequestBuilder BuildRequest(QBittorrentSettings settings)
        {
            var requestBuilder = new HttpRequestBuilder(settings.UseSsl, settings.Host, settings.Port, settings.UrlBase)
            {
                LogResponseContent = true,
                StoreRequestCookie = false,
                AllowAutoRedirect = true
            };

            if (settings.ApiKey.IsNotNullOrWhiteSpace())
            {
                requestBuilder.Headers["Authorization"] = $"Bearer {settings.ApiKey}";
            }
            else if (settings.Username.IsNotNullOrWhiteSpace() || settings.Password.IsNotNullOrWhiteSpace())
            {
                requestBuilder.NetworkCredential = new BasicNetworkCredential(settings.Username, settings.Password);
            }

            return requestBuilder;
        }

        private TResult ProcessRequest<TResult>(HttpRequestBuilder requestBuilder, QBittorrentSettings settings)
            where TResult : new()
        {
            var responseContent = ProcessRequest(requestBuilder, settings);

            return Json.Deserialize<TResult>(responseContent);
        }

        private string ProcessRequest(HttpRequestBuilder requestBuilder, QBittorrentSettings settings)
        {
            if (settings.ApiKey.IsNotNullOrWhiteSpace())
            {
                var requestWithApiKey = requestBuilder.Build();
                requestWithApiKey.LogResponseContent = true;

                try
                {
                    return _httpClient.Execute(requestWithApiKey).Content;
                }
                catch (HttpException ex)
                {
                    if (ex.Response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    {
                        _logger.Debug(ex, "qbitTorrent authentication failed.");

                        throw new DownloadClientAuthenticationException(
                            "Failed to authenticate with qBittorrent. API key authentication requires qBittorrent 5.2.0 (Web API 2.15.1) or newer; on older servers use a username and password instead.",
                            ex);
                    }

                    throw new DownloadClientException("Failed to connect to qBittorrent, check your settings.", ex);
                }
                catch (Exception ex)
                {
                    throw new DownloadClientException("Failed to connect to qBittorrent, please check your settings.", ex);
                }
            }

            AuthenticateClient(requestBuilder, settings);

            // Set on the builder so the re-authentication retry below keeps suppressing 401/403.
            requestBuilder.SuppressHttpErrorStatusCodes = [HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized];

            var request = requestBuilder.Build();
            request.LogResponseContent = true;

            try
            {
                var response = _httpClient.Execute(request);

                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                {
                    _logger.Debug("Authentication required, logging in.");

                    AuthenticateClient(requestBuilder, settings, true);

                    request = requestBuilder.Build();

                    response = _httpClient.Execute(request);
                }

                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                {
                    throw new DownloadClientAuthenticationException("Failed to authenticate with qBittorrent.");
                }

                return response.Content;
            }
            catch (DownloadClientAuthenticationException)
            {
                throw;
            }
            catch (HttpException ex)
            {
                throw new DownloadClientException("Failed to connect to qBittorrent, check your settings.", ex);
            }
            catch (HttpRequestException ex) when (ex.InnerException is AuthenticationException)
            {
                throw new DownloadClientUnavailableException("Unable to connect to qBittorrent, certificate validation failed.", ex);
            }
            catch (AuthenticationException ex)
            {
                throw new DownloadClientUnavailableException("Unable to connect to qBittorrent, certificate validation failed.", ex);
            }
            catch (Exception ex)
            {
                throw new DownloadClientException("Failed to connect to qBittorrent, please check your settings.", ex);
            }
        }

        private void AuthenticateClient(HttpRequestBuilder requestBuilder, QBittorrentSettings settings, bool reauthenticate = false)
        {
            if (settings.Username.IsNullOrWhiteSpace() || settings.Password.IsNullOrWhiteSpace())
            {
                if (reauthenticate)
                {
                    throw new DownloadClientAuthenticationException("Failed to authenticate with qBittorrent.");
                }

                return;
            }

            var authKey = $"{requestBuilder.BaseUrl}:{settings.Username}:{settings.Password}";

            var cookies = _authCookieCache.Find(authKey);

            if (cookies == null || reauthenticate)
            {
                EnsureAuthBackoffAllows(authKey);

                if (reauthenticate && cookies != null)
                {
                    LogoutClient(cookies, settings);
                }

                _authCookieCache.Remove(authKey);

                var authRequestBuilder = BuildRequest(settings);

                var authLoginRequest = authRequestBuilder.Resource("/api/v2/auth/login")
                    .Post()
                    .AddFormParameter("username", settings.Username ?? string.Empty)
                    .AddFormParameter("password", settings.Password ?? string.Empty)
                    .Build();

                HttpResponse response;
                try
                {
                    response = _httpClient.Execute(authLoginRequest);
                }
                catch (HttpException ex)
                {
                    _logger.Debug(ex, "qbitTorrent authentication failed.");

                    if (ex.Response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    {
                        RecordAuthFailure(authKey);
                        throw new DownloadClientAuthenticationException("Failed to authenticate with qBittorrent.", ex);
                    }

                    throw new DownloadClientException("Failed to connect to qBittorrent, please check your settings.", ex);
                }
                catch (WebException ex)
                {
                    throw new DownloadClientUnavailableException("Failed to connect to qBittorrent, please check your settings.", ex);
                }

                if (response.Content.IsNotNullOrWhiteSpace() && response.Content != "Ok.")
                {
                    // returns "Fails." on bad login
                    _logger.Debug("qbitTorrent authentication failed.");
                    RecordAuthFailure(authKey);
                    throw new DownloadClientAuthenticationException("Failed to authenticate with qBittorrent.");
                }

                _logger.Debug("qBittorrent authentication succeeded.");

                _authBackoffCache.Remove(authKey);

                cookies = response.GetCookies();

                _authCookieCache.Set(authKey, cookies);
            }

            requestBuilder.SetCookies(cookies);
        }

        private void LogoutClient(Dictionary<string, string> cookies, QBittorrentSettings settings)
        {
            try
            {
                var request = BuildRequest(settings).Resource("/api/v2/auth/logout")
                                                    .Post()
                                                    .SetCookies(cookies)
                                                    .Build();
                request.SuppressHttpError = true;

                _httpClient.Execute(request);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "qBittorrent logout failed.");
            }
        }

        private void EnsureAuthBackoffAllows(string authKey)
        {
            var state = _authBackoffCache.Find(authKey);

            if (state == null)
            {
                return;
            }

            var elapsed = DateTime.UtcNow - state.LastFailure;
            var delay = GetAuthBackoffDelay(state.Count);

            if (elapsed < delay)
            {
                var remaining = (int)Math.Ceiling((delay - elapsed).TotalSeconds);
                throw new DownloadClientAuthenticationException($"qBittorrent authentication is temporarily blocked after repeated failures. Retry in {remaining} seconds or verify the username and password.");
            }
        }

        private void RecordAuthFailure(string authKey)
        {
            var state = _authBackoffCache.Find(authKey) ?? new AuthBackoffState();

            state.Count++;
            state.LastFailure = DateTime.UtcNow;

            _authBackoffCache.Set(authKey, state);
        }

        private static TimeSpan GetAuthBackoffDelay(int failureCount)
        {
            if (failureCount <= 0)
            {
                return TimeSpan.Zero;
            }

            var seconds = Math.Min(AuthBackoffMinDelay.TotalSeconds * Math.Pow(2, failureCount - 1), AuthBackoffMaxDelay.TotalSeconds);

            return TimeSpan.FromSeconds(seconds);
        }
    }
}
