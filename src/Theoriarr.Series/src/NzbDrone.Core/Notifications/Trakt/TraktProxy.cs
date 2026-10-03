using System;
using System.Net.Http;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Notifications.Trakt.Resource;

namespace NzbDrone.Core.Notifications.Trakt
{
    public interface ITraktProxy
    {
        string GetUserName(string accessToken);
        string GetMovieUserName(string accessToken);
        HttpRequest GetOAuthRequest(string callbackUrl);
        HttpRequest GetMovieOAuthRequest(string callbackUrl);
        TraktAuthRefreshResource RefreshAuthToken(string refreshToken);
        TraktAuthRefreshResource RefreshMovieAuthToken(string refreshToken);
        void AddToCollection(TraktCollectShowsResource payload, string accessToken);
        void RemoveFromCollection(TraktCollectShowsResource payload, string accessToken);
        void AddToCollection(TraktCollectMoviesResource payload, string accessToken);
        void RemoveFromCollection(TraktCollectMoviesResource payload, string accessToken);
        HttpRequest BuildRequest(string resource, HttpMethod method, string accessToken);
        HttpRequest BuildRequest(HttpRequest request, string accessToken);
    }

    public class TraktProxy : ITraktProxy
    {
        private const string URL = "https://api.trakt.tv";

        // SERIES (Sonarr) OAuth application — canonical for the unified connector.
        private const string OAuthUrl = "https://trakt.tv/oauth/authorize";
        private const string RedirectUri = "https://auth.servarr.com/v1/trakt_sonarr/auth";
        private const string RenewUri = "https://auth.servarr.com/v1/trakt_sonarr/renew";
        private const string ClientId = "d44ba57cab40c31eb3f797dcfccd203500796539125b333883ec1d94aa62ed4c";

        // MOVIES (Radarr) OAuth application — merged so movie collection sync uses the
        // client an existing Radarr token was issued to.
        private const string MovieOAuthUrl = "https://auth.trakt.tv/oauth/authorize";
        private const string MovieRedirectUri = "https://auth.servarr.com/v1/trakt/auth";
        private const string MovieRenewUri = "https://auth.servarr.com/v1/trakt/renew";
        private const string MovieClientId = "64508a8bf370cee550dde4806469922fd7cd70afb2d5690e3ee7f75ae784b70e";

        private static readonly TimeSpan DefaultRateLimit = TimeSpan.FromSeconds(2);

        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public TraktProxy(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public void AddToCollection(TraktCollectShowsResource payload, string accessToken)
        {
            var request = BuildRequest("sync/collection", HttpMethod.Post, accessToken);

            request.Headers.ContentType = "application/json";
            request.SetContent(payload.ToJson());

            MakeRequest(request);
        }

        public void RemoveFromCollection(TraktCollectShowsResource payload, string accessToken)
        {
            var request = BuildRequest("sync/collection/remove", HttpMethod.Post, accessToken);

            request.Headers.ContentType = "application/json";
            request.SetContent(payload.ToJson());

            MakeRequest(request);
        }

        public void AddToCollection(TraktCollectMoviesResource payload, string accessToken)
        {
            var request = BuildRequest("sync/collection", HttpMethod.Post, accessToken, true);

            request.Headers.ContentType = "application/json";
            request.SetContent(payload.ToJson());

            MakeRequest(request);
        }

        public void RemoveFromCollection(TraktCollectMoviesResource payload, string accessToken)
        {
            var request = BuildRequest("sync/collection/remove", HttpMethod.Post, accessToken, true);

            request.Headers.ContentType = "application/json";
            request.SetContent(payload.ToJson());

            MakeRequest(request);
        }

        public string GetUserName(string accessToken)
        {
            var request = BuildRequest("users/settings", HttpMethod.Get, accessToken);
            var response = _httpClient.Get<TraktUserSettingsResource>(request);

            return response?.Resource?.User?.Ids?.Slug;
        }

        public string GetMovieUserName(string accessToken)
        {
            var request = BuildRequest("users/settings", HttpMethod.Get, accessToken, true);
            var response = _httpClient.Get<TraktUserSettingsResource>(request);

            return response?.Resource?.User?.Ids?.Slug;
        }

        public HttpRequest GetOAuthRequest(string callbackUrl)
        {
            return BuildOAuthRequest(OAuthUrl, RedirectUri, ClientId, callbackUrl);
        }

        public HttpRequest GetMovieOAuthRequest(string callbackUrl)
        {
            return BuildOAuthRequest(MovieOAuthUrl, MovieRedirectUri, MovieClientId, callbackUrl);
        }

        public TraktAuthRefreshResource RefreshAuthToken(string refreshToken)
        {
            return RefreshAuthToken(RenewUri, refreshToken);
        }

        public TraktAuthRefreshResource RefreshMovieAuthToken(string refreshToken)
        {
            return RefreshAuthToken(MovieRenewUri, refreshToken);
        }

        public HttpRequest BuildRequest(string resource, HttpMethod method, string accessToken)
        {
            return BuildRequest(resource, method, accessToken, false);
        }

        public HttpRequest BuildRequest(HttpRequest request, string accessToken)
        {
            return BuildRequest(request, accessToken, false);
        }

        private static HttpRequest BuildOAuthRequest(string oauthUrl, string redirectUri, string clientId, string callbackUrl)
        {
            return new HttpRequestBuilder(oauthUrl)
                            .AddQueryParam("client_id", clientId)
                            .AddQueryParam("response_type", "code")
                            .AddQueryParam("redirect_uri", redirectUri)
                            .AddQueryParam("state", callbackUrl)
                            .Build();
        }

        private TraktAuthRefreshResource RefreshAuthToken(string renewUri, string refreshToken)
        {
            var request = new HttpRequestBuilder(renewUri)
                    .AddQueryParam("refresh_token", refreshToken)
                    .WithRateLimit(2)
                    .Build();

            return _httpClient.Get<TraktAuthRefreshResource>(request)?.Resource ?? null;
        }

        private HttpRequest BuildRequest(string resource, HttpMethod method, string accessToken, bool useMovieClient)
        {
            var request = new HttpRequestBuilder(URL).Resource(resource).Build();

            request.RateLimit = DefaultRateLimit;
            request.Method = method;

            return BuildRequest(request, accessToken, useMovieClient);
        }

        private HttpRequest BuildRequest(HttpRequest request, string accessToken, bool useMovieClient)
        {
            if (request.RateLimit < DefaultRateLimit)
            {
                request.RateLimit = DefaultRateLimit;
            }

            request.Headers.Accept = HttpAccept.Json.Value;

            request.Headers.Add("trakt-api-version", "2");
            request.Headers.Add("trakt-api-key", useMovieClient ? MovieClientId : ClientId);

            if (accessToken.IsNotNullOrWhiteSpace())
            {
                request.Headers.Add("Authorization", "Bearer " + accessToken);
            }

            return request;
        }

        private void MakeRequest(HttpRequest request)
        {
            try
            {
                _httpClient.Execute(request);
            }
            catch (HttpException ex)
            {
                throw new TraktException("Unable to send payload", ex);
            }
        }
    }
}
