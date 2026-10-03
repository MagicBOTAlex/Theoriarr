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
    // API https://github.com/qbittorrent/qBittorrent/wiki/WebUI-API-Documentation

    public class QBittorrentProxyV1 : IQBittorrentProxy
    {
        private static readonly TimeSpan AuthBackoffMinDelay = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan AuthBackoffMaxDelay = TimeSpan.FromMinutes(5);

        // /command/download and /command/upload read the label from "label" up to this version, after which it is "category".
        private static readonly Version LabelParameterVersion = new Version(3, 3, 1);
        private static readonly Version CategoryParameterVersion = new Version(3, 3, 5);

        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;
        private readonly ICached<Dictionary<string, string>> _authCookieCache;
        private readonly ICached<AuthBackoffState> _authBackoffCache;
        private readonly ICached<Version> _versionCache;

        private class AuthBackoffState
        {
            public int Count { get; set; }
            public DateTime LastFailure { get; set; }
        }

        public QBittorrentProxyV1(IHttpClient httpClient, ICacheManager cacheManager, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _authCookieCache = cacheManager.GetCache<Dictionary<string, string>>(GetType(), "authCookies");
            _authBackoffCache = cacheManager.GetCache<AuthBackoffState>(GetType(), "authBackoff");
            _versionCache = cacheManager.GetCache<Version>(GetType(), "versions");
        }

        public bool IsApiSupported(QBittorrentSettings settings)
        {
            // We can do the api test without having to authenticate since v4.1 will return 404 on the request.
            // Do not follow redirects here: a 3xx means we did not reach the qBittorrent API directly and must
            // not be accepted as qBittorrent just because the redirect target happens to answer with a version.
            var request = BuildRequest(settings).Resource("/version/api").AllowRedirect(false);
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

                // Some other service can answer 200 with an html page; only claim support when the body is an integer version.
                return Version.TryParse("1." + response.Content?.Trim(), out _);
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
            // Version request does not require authentication and will return 404 if it doesn't exist.
            var request = BuildRequest(settings).Resource("/version/api");
            var content = ProcessRequest(request, settings);

            if (!Version.TryParse("1." + content?.Trim(), out var version))
            {
                throw new DownloadClientException("Failed to determine qBittorrent API version, check your settings.");
            }

            return version;
        }

        public string GetVersion(QBittorrentSettings settings)
        {
            // Version request does not require authentication.
            var request = BuildRequest(settings).Resource("/version/qbittorrent");
            var response = ProcessRequest(request, settings).TrimStart('v');

            return response;
        }

        public QBittorrentPreferences GetConfig(QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/query/preferences");
            var response = ProcessRequest<QBittorrentPreferences>(request, settings);

            return response;
        }

        public List<QBittorrentTorrent> GetTorrents(string category, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/query/torrents");
            if (category.IsNotNullOrWhiteSpace())
            {
                request.AddQueryParam("label", category);
                request.AddQueryParam("category", category);
            }

            var response = ProcessRequest<List<QBittorrentTorrent>>(request, settings);

            return response;
        }

        public bool IsTorrentLoaded(string hash, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource($"/query/propertiesGeneral/{hash}");
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
            var request = BuildRequest(settings).Resource($"/query/propertiesGeneral/{hash}");
            var response = ProcessRequest<QBittorrentTorrentProperties>(request, settings);

            return response;
        }

        public List<QBittorrentTorrentFile> GetTorrentFiles(string hash, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource($"/query/propertiesFiles/{hash}");
            var response = ProcessRequest<List<QBittorrentTorrentFile>>(request, settings);

            return response;
        }

        public void AddTorrentFromUrl(string torrentUrl, TorrentSeedConfiguration seedConfiguration, string category, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/command/download")
                                                .Post()
                                                .AddFormParameter("urls", torrentUrl);

            AddTorrentParameters(request, category, settings);

            var result = ProcessRequest(request, settings);

            // Note: Older qbit versions returned nothing, so we can't do != "Ok." here.
            if (result == "Fails.")
            {
                throw new DownloadClientException("Download client failed to add torrent by url");
            }
        }

        public void AddTorrentFromFile(string fileName, byte[] fileContent, TorrentSeedConfiguration seedConfiguration, string category, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/command/upload")
                                                .Post()
                                                .AddFormUpload("torrents", fileName, fileContent);

            AddTorrentParameters(request, category, settings);

            var result = ProcessRequest(request, settings);

            // Note: Current qbit versions return nothing, so we can't do != "Ok." here.
            if (result == "Fails.")
            {
                throw new DownloadClientException("Download client failed to add torrent");
            }
        }

        private void AddTorrentParameters(HttpRequestBuilder request, string category, QBittorrentSettings settings)
        {
            var version = GetCachedVersion(settings);

            if (category.IsNotNullOrWhiteSpace())
            {
                if (version >= CategoryParameterVersion)
                {
                    request.AddFormParameter("category", category);
                }
                else if (version >= LabelParameterVersion)
                {
                    // 3.3.1-3.3.4 understand the label under the "label" key, not "category".
                    request.AddFormParameter("label", category);
                }
            }

            // qBittorrent 3.2.4-3.3.0 reject any additional form key on /command/download (and do not read them on upload),
            // and 3.3.1-3.3.4 only understand the label, so "paused" is only sent to 3.3.5 and newer.
            if (version < CategoryParameterVersion)
            {
                return;
            }

            // Note: ForceStart is handled by separate api call
            if ((QBittorrentState)settings.InitialState == QBittorrentState.Start)
            {
                request.AddFormParameter("paused", "false");
            }
            else if ((QBittorrentState)settings.InitialState == QBittorrentState.Stop)
            {
                request.AddFormParameter("paused", "true");
            }
        }

        private Version GetCachedVersion(QBittorrentSettings settings)
        {
            var versionKey = string.Format("{0}_{1}_{2}_{3}", settings.UseSsl, settings.Host, settings.Port, settings.UrlBase);

            // GetVersion performs an HTTP request; cache the parsed result so add calls don't probe it every time.
            return _versionCache.Get(versionKey, () => ParseVersion(GetVersion(settings)), TimeSpan.FromMinutes(10.0));
        }

        private static Version ParseVersion(string versionString)
        {
            // Only 3.2.4-3.3.0 use the strict "urls"/upload-only add form. When the version cannot be
            // parsed fall back to the current parameter names.
            return Version.TryParse(versionString, out var version) ? version : CategoryParameterVersion;
        }

        public void RemoveTorrent(string hash, bool removeData, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource(removeData ? "/command/deletePerm" : "/command/delete")
                                                    .Post()
                                                    .AddFormParameter("hashes", hash);

            ProcessRequest(request, settings);
        }

        public void SetTorrentLabel(string hash, string label, QBittorrentSettings settings)
        {
            var setCategoryRequest = BuildRequest(settings).Resource("/command/setCategory")
                                                        .Post()
                                                        .AddFormParameter("hashes", hash)
                                                        .AddFormParameter("category", label);
            try
            {
                ProcessRequest(setCategoryRequest, settings);
            }
            catch (DownloadClientException ex)
            {
                // if setCategory fails due to method not being found, then try older setLabel command for qBittorrent < v.3.3.5
                if (ex.InnerException is HttpException httpException && httpException.Response.StatusCode == HttpStatusCode.NotFound)
                {
                    var setLabelRequest = BuildRequest(settings).Resource("/command/setLabel")
                                                                .Post()
                                                                .AddFormParameter("hashes", hash)
                                                                .AddFormParameter("label", label);

                    ProcessRequest(setLabelRequest, settings);
                }
            }
        }

        public void AddLabel(string label, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/command/addCategory")
                                                .Post()
                                                .AddFormParameter("category", label);
            ProcessRequest(request, settings);
        }

        public Dictionary<string, QBittorrentLabel> GetLabels(QBittorrentSettings settings)
        {
            throw new NotSupportedException("qBittorrent api v1 does not support getting all torrent categories");
        }

        public void SetTorrentSeedingConfiguration(string hash, TorrentSeedConfiguration seedConfiguration, QBittorrentSettings settings)
        {
            // Not supported on api v1
        }

        public void MoveTorrentToTopInQueue(string hash, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/command/topPrio")
                                                    .Post()
                                                    .AddFormParameter("hashes", hash);
            try
            {
                ProcessRequest(request, settings);
            }
            catch (DownloadClientException ex)
            {
                // qBittorrent rejects all Prio commands with 403: Forbidden if Options -> BitTorrent -> Torrent Queueing is not enabled
                if (ex.InnerException is HttpException httpException && httpException.Response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return;
                }

                throw;
            }
        }

        public void SetForceStart(string hash, bool enabled, QBittorrentSettings settings)
        {
            var request = BuildRequest(settings).Resource("/command/setForceStart")
                                                .Post()
                                                .AddFormParameter("hashes", hash)
                                                .AddFormParameter("value", enabled ? "true" : "false");
            ProcessRequest(request, settings);
        }

        public void AddTags(string hash, IEnumerable<string> tags, QBittorrentSettings settings)
        {
            // Not supported on api v1
        }

        public void SetTorrentLocation(string hash, string location, QBittorrentSettings settings)
        {
            throw new NotSupportedException("qBittorrent api v1 does not support changing a torrent's location");
        }

        private HttpRequestBuilder BuildRequest(QBittorrentSettings settings)
        {
            var requestBuilder = new HttpRequestBuilder(settings.UseSsl, settings.Host, settings.Port, settings.UrlBase)
            {
                LogResponseContent = true,
                StoreRequestCookie = false,
                NetworkCredential = new BasicNetworkCredential(settings.Username, settings.Password),
                AllowAutoRedirect = true
            };
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
            AuthenticateClient(requestBuilder, settings);

            // Set on the builder so the re-authentication retry below keeps suppressing 401/403.
            requestBuilder.SuppressHttpErrorStatusCodes = new[] { HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized };

            var request = requestBuilder.Build();
            request.LogResponseContent = true;

            HttpResponse response;
            try
            {
                response = _httpClient.Execute(request);

                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                {
                    _logger.Debug("Authentication required, logging in.");

                    AuthenticateClient(requestBuilder, settings, true);

                    request = requestBuilder.Build();

                    response = _httpClient.Execute(request);
                }

                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
                {
                    // Attach the response so callers can distinguish the queueing-disabled 403 from a genuine auth failure.
                    throw new DownloadClientAuthenticationException("Failed to authenticate with qBittorrent.", new HttpException(response));
                }
            }
            catch (DownloadClientAuthenticationException)
            {
                throw;
            }
            catch (HttpException ex)
            {
                throw new DownloadClientException("Failed to connect to qBittorrent, check your settings.", ex);
            }
            catch (WebException ex)
            {
                throw new DownloadClientException("Failed to connect to qBittorrent, please check your settings.", ex);
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

            return response.Content;
        }

        private void AuthenticateClient(HttpRequestBuilder requestBuilder, QBittorrentSettings settings, bool reauthenticate = false)
        {
            if (settings.Username.IsNullOrWhiteSpace() || settings.Password.IsNullOrWhiteSpace())
            {
                return;
            }

            var authKey = string.Format("{0}:{1}:{2}", requestBuilder.BaseUrl, settings.Username, settings.Password);

            var cookies = _authCookieCache.Find(authKey);

            if (cookies == null || reauthenticate)
            {
                EnsureAuthBackoffAllows(authKey);

                if (reauthenticate && cookies != null)
                {
                    LogoutClient(cookies, settings);
                }

                _authCookieCache.Remove(authKey);

                var authLoginRequest = BuildRequest(settings).Resource("/login")
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
                    _logger.Debug("qbitTorrent authentication failed.");
                    if (ex.Response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
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

                if (response.Content != "Ok.")
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
                var request = BuildRequest(settings).Resource("/logout")
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
