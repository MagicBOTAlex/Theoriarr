using System.Collections.Generic;
using System.Linq;
using FluentValidation.Results;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Notifications.Telegram
{
    public class Telegram : NotificationBase<TelegramSettings>
    {
        private readonly ITelegramProxy _proxy;
        private readonly IConfigFileProvider _configFileProvider;

        public Telegram(ITelegramProxy proxy, IConfigFileProvider configFileProvider)
        {
            _proxy = proxy;
            _configFileProvider = configFileProvider;
        }

        public override string Name => "Telegram";
        public override string Link => "https://telegram.org/";

        private string InstanceName => _configFileProvider.InstanceName;

        public override void OnGrab(GrabMessage grabMessage)
        {
            if (grabMessage.Movie != null)
            {
                var movieTitle = Settings.IncludeAppNameInTitle ? MOVIE_GRABBED_TITLE_BRANDED : MOVIE_GRABBED_TITLE;
                movieTitle = Settings.IncludeInstanceNameInTitle ? $"{movieTitle} - {InstanceName}" : movieTitle;
                var movieLinks = GetLinks(grabMessage.Movie);

                _proxy.SendNotification(movieTitle, grabMessage.Message, movieLinks, Settings);

                return;
            }

            var title = Settings.IncludeAppNameInTitle ? EPISODE_GRABBED_TITLE_BRANDED : EPISODE_GRABBED_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;
            var links = GetLinks(grabMessage.Series);

            _proxy.SendNotification(title, grabMessage.Message, links, Settings);
        }

        public override void OnDownload(DownloadMessage message)
        {
            if (message.Movie != null)
            {
                string movieTitle;
                if (message.OldMovieFiles.Any())
                {
                    movieTitle = Settings.IncludeAppNameInTitle ? MOVIE_UPGRADED_TITLE_BRANDED : MOVIE_UPGRADED_TITLE;
                }
                else
                {
                    movieTitle = Settings.IncludeAppNameInTitle ? MOVIE_DOWNLOADED_TITLE_BRANDED : MOVIE_DOWNLOADED_TITLE;
                }

                movieTitle = Settings.IncludeInstanceNameInTitle ? $"{movieTitle} - {InstanceName}" : movieTitle;
                var movieLinks = GetLinks(message.Movie);

                _proxy.SendNotification(movieTitle, message.Message, movieLinks, Settings);

                return;
            }

            var title = Settings.IncludeAppNameInTitle ? EPISODE_DOWNLOADED_TITLE_BRANDED : EPISODE_DOWNLOADED_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;
            var links = GetLinks(message.Series);

            _proxy.SendNotification(title, message.Message, links, Settings);
        }

        public override void OnImportComplete(ImportCompleteMessage message)
        {
            var title = Settings.IncludeAppNameInTitle ? EPISODE_DOWNLOADED_TITLE_BRANDED : EPISODE_DOWNLOADED_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;
            var links = GetLinks(message.Series);

            _proxy.SendNotification(title, message.Message, links, Settings);
        }

        public override void OnEpisodeFileDelete(EpisodeDeleteMessage deleteMessage)
        {
            var title = Settings.IncludeAppNameInTitle ? EPISODE_DELETED_TITLE_BRANDED : EPISODE_DELETED_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;
            var links = GetLinks(deleteMessage.Series);

            _proxy.SendNotification(title, deleteMessage.Message, links, Settings);
        }

        public override void OnSeriesAdd(SeriesAddMessage message)
        {
            var title = Settings.IncludeAppNameInTitle ? SERIES_ADDED_TITLE_BRANDED : SERIES_ADDED_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;
            var links = GetLinks(message.Series);

            _proxy.SendNotification(title, message.Message, links, Settings);
        }

        public override void OnSeriesDelete(SeriesDeleteMessage deleteMessage)
        {
            var title = Settings.IncludeAppNameInTitle ? SERIES_DELETED_TITLE_BRANDED : SERIES_DELETED_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;
            var links = GetLinks(deleteMessage.Series);

            _proxy.SendNotification(title, deleteMessage.Message, links, Settings);
        }

        public override void OnMovieAdded(Movie movie)
        {
            var title = Settings.IncludeAppNameInTitle ? MOVIE_ADDED_TITLE_BRANDED : MOVIE_ADDED_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;
            var links = GetLinks(movie);

            _proxy.SendNotification(title, $"{movie.Title} added to library", links, Settings);
        }

        public override void OnMovieFileDelete(MovieFileDeleteMessage deleteMessage)
        {
            var title = Settings.IncludeAppNameInTitle ? MOVIE_FILE_DELETED_TITLE_BRANDED : MOVIE_FILE_DELETED_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;
            var links = GetLinks(deleteMessage.Movie);

            _proxy.SendNotification(title, deleteMessage.Message, links, Settings);
        }

        public override void OnMovieDelete(MovieDeleteMessage deleteMessage)
        {
            var title = Settings.IncludeAppNameInTitle ? MOVIE_DELETED_TITLE_BRANDED : MOVIE_DELETED_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;
            var links = GetLinks(deleteMessage.Movie);

            _proxy.SendNotification(title, deleteMessage.Message, links, Settings);
        }

        public override void OnHealthIssue(HealthCheck.HealthCheck healthCheck)
        {
            var title = Settings.IncludeAppNameInTitle ? HEALTH_ISSUE_TITLE_BRANDED : HEALTH_ISSUE_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;

            _proxy.SendNotification(title, healthCheck.Message, new List<NotificationMetadataLink>(), Settings);
        }

        public override void OnHealthRestored(HealthCheck.HealthCheck previousCheck)
        {
            var title = Settings.IncludeAppNameInTitle ? HEALTH_RESTORED_TITLE_BRANDED : HEALTH_RESTORED_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;

            _proxy.SendNotification(title, $"The following issue is now resolved: {previousCheck.Message}", new List<NotificationMetadataLink>(), Settings);
        }

        public override void OnApplicationUpdate(ApplicationUpdateMessage updateMessage)
        {
            var title = Settings.IncludeAppNameInTitle ? APPLICATION_UPDATE_TITLE_BRANDED : APPLICATION_UPDATE_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;

            _proxy.SendNotification(title, updateMessage.Message, new List<NotificationMetadataLink>(), Settings);
        }

        public override void OnManualInteractionRequired(ManualInteractionRequiredMessage message)
        {
            if (message.Movie != null)
            {
                var movieTitle = Settings.IncludeAppNameInTitle ? MANUAL_INTERACTION_REQUIRED_TITLE_BRANDED : MANUAL_INTERACTION_REQUIRED_TITLE;
                movieTitle = Settings.IncludeInstanceNameInTitle ? $"{movieTitle} - {InstanceName}" : movieTitle;
                var movieLinks = GetLinks(message.Movie);

                _proxy.SendNotification(movieTitle, message.Message, movieLinks, Settings);

                return;
            }

            var title = Settings.IncludeAppNameInTitle ? MANUAL_INTERACTION_REQUIRED_TITLE_BRANDED : MANUAL_INTERACTION_REQUIRED_TITLE;
            title = Settings.IncludeInstanceNameInTitle ? $"{title} - {InstanceName}" : title;
            var links = GetLinks(message.Series);

            _proxy.SendNotification(title, message.Message, links, Settings);
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            failures.AddIfNotNull(_proxy.Test(Settings));

            return new ValidationResult(failures);
        }

        private List<NotificationMetadataLink> GetLinks(Series series)
        {
            return NotificationMetadataLinkGenerator.GenerateLinks(series, Settings.MetadataLinks);
        }

        private List<TelegramLink> GetLinks(Movie movie)
        {
            var links = new List<TelegramLink>();

            if (movie == null)
            {
                return links;
            }

            foreach (var link in Settings.MetadataLinks)
            {
                var linkType = (MetadataLinkType)link;

                if (linkType == MetadataLinkType.Tmdb && movie.TmdbId > 0)
                {
                    links.Add(new TelegramLink("TMDb", $"https://www.themoviedb.org/movie/{movie.TmdbId}"));
                }

                if (linkType == MetadataLinkType.Imdb && movie.ImdbId.IsNotNullOrWhiteSpace())
                {
                    links.Add(new TelegramLink("IMDb", $"https://www.imdb.com/title/{movie.ImdbId}"));
                }

                if (linkType == MetadataLinkType.Trakt && movie.ImdbId.IsNotNullOrWhiteSpace())
                {
                    links.Add(new TelegramLink("Trakt", $"https://trakt.tv/movies/{movie.ImdbId}"));
                }
            }

            return links;
        }
    }
}
