using System.Net.Http;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Notifications.Webhook;

namespace NzbDrone.Core.Notifications.Notifiarr
{
    public interface INotifiarrProxy
    {
        void SendNotification(WebhookPayload payload, NotifiarrSettings settings);
    }

    public class NotifiarrProxy : INotifiarrProxy
    {
        private const string URL = "https://notifiarr.com";
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public NotifiarrProxy(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public void SendNotification(WebhookPayload payload, NotifiarrSettings settings)
        {
            ProcessNotification(payload, settings);
        }

        private void ProcessNotification(WebhookPayload payload, NotifiarrSettings settings)
        {
            var isMovie = IsMoviePayload(payload);
            var endpoint = isMovie ? "/api/v1/notification/radarr" : "/api/v1/notification/sonarr";
            var apiKey = isMovie ? settings.APIKey : settings.ApiKey;

            try
            {
                var request = new HttpRequestBuilder(URL + endpoint)
                    .Accept(HttpAccept.Json)
                    .SetHeader("X-API-Key", apiKey)
                    .Build();

                request.Method = HttpMethod.Post;

                request.Headers.ContentType = "application/json";
                request.SetContent(payload.ToJson());

                _httpClient.Post(request);
            }
            catch (HttpException ex)
            {
                var responseCode = ex.Response.StatusCode;
                switch ((int)responseCode)
                {
                    case 401:
                        _logger.Warn("HTTP 401 - API key is invalid");
                        throw new NotifiarrException("API key is invalid");
                    case 400:
                        // 400 responses shouldn't be treated as an actual error because it's a misconfiguration
                        // between the app and Notifiarr for a specific event, but shouldn't stop all events.
                        _logger.Warn("HTTP 400 - Unable to send notification. Ensure the app Integration is enabled & assigned a channel on Notifiarr");
                        break;
                    case 502:
                    case 503:
                    case 504:
                        _logger.Warn("Unable to send notification. Service Unavailable");
                        throw new NotifiarrException("Unable to send notification. Service Unavailable", ex);
                    case 520:
                    case 521:
                    case 522:
                    case 523:
                    case 524:
                        throw new NotifiarrException("Cloudflare Related HTTP Error - Unable to send notification", ex);
                    default:
                        _logger.Error(ex, "Unknown HTTP Error - Unable to send notification");
                        throw new NotifiarrException("Unknown HTTP Error - Unable to send notification", ex);
                }
            }
        }

        private static bool IsMoviePayload(WebhookPayload payload)
        {
            return payload switch
            {
                WebhookAddedPayload => true,
                WebhookMovieDeletePayload => true,
                WebhookMovieFileDeletePayload => true,
                WebhookGrabPayload grab => grab.Movie != null,
                WebhookImportPayload import => import.Movie != null,
                WebhookRenamePayload rename => rename.Movie != null,
                WebhookManualInteractionPayload manual => manual.Movie != null,
                _ => false
            };
        }
    }
}
