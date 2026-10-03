using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Processes;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.Localization;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tags;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Notifications.CustomScript
{
    public class CustomScript : NotificationBase<CustomScriptSettings>
    {
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IConfigService _configService;
        private readonly IDiskProvider _diskProvider;
        private readonly IProcessProvider _processProvider;
        private readonly ITagRepository _tagRepository;
        private readonly ILocalizationService _localizationService;
        private readonly Logger _logger;
        private static int _theoriarrAliasWarningLogged;

        public CustomScript(IConfigFileProvider configFileProvider,
            IConfigService configService,
            IDiskProvider diskProvider,
            IProcessProvider processProvider,
            ITagRepository tagRepository,
            ILocalizationService localizationService,
            Logger logger)
        {
            _configFileProvider = configFileProvider;
            _configService = configService;
            _diskProvider = diskProvider;
            _processProvider = processProvider;
            _tagRepository = tagRepository;
            _localizationService = localizationService;
            _logger = logger;
        }

        public override string Name => _localizationService.GetLocalizedString("NotificationsCustomScriptSettingsName");

        public override string Link => "https://wiki.servarr.com/sonarr/settings#connections";

        public override ProviderMessage Message => new ProviderMessage(_localizationService.GetLocalizedString("NotificationsCustomScriptSettingsProviderMessage", new Dictionary<string, object> { { "eventTypeTest", "Test" } }), ProviderMessageType.Warning);

        public override void OnGrab(GrabMessage message)
        {
            if (message.Movie != null)
            {
                var movie = message.Movie;
                var remoteMovie = message.RemoteMovie;
                var quality = message.Quality;
                var environmentVariables = new StringDictionary();

                environmentVariables.Add("Radarr_EventType", "Grab");
                environmentVariables.Add("Radarr_InstanceName", _configFileProvider.InstanceName);
                environmentVariables.Add("Radarr_ApplicationUrl", _configService.ApplicationUrl);
                environmentVariables.Add("Radarr_Movie_Id", movie.Id.ToString());
                environmentVariables.Add("Radarr_Movie_Title", movie.MovieMetadata.Value.Title);
                environmentVariables.Add("Radarr_Movie_Year", movie.MovieMetadata.Value.Year.ToString());
                environmentVariables.Add("Radarr_Movie_OriginalLanguage", IsoLanguages.Get(movie.MovieMetadata.Value.OriginalLanguage).ThreeLetterCode);
                environmentVariables.Add("Radarr_Movie_Genres", string.Join("|", movie.MovieMetadata.Value.Genres));
                environmentVariables.Add("Radarr_Movie_Tags", string.Join("|", GetTagLabels(movie)));
                environmentVariables.Add("Radarr_Movie_ImdbId", movie.MovieMetadata.Value.ImdbId ?? string.Empty);
                environmentVariables.Add("Radarr_Movie_TmdbId", movie.MovieMetadata.Value.TmdbId.ToString());
                environmentVariables.Add("Radarr_Movie_In_Cinemas_Date", movie.MovieMetadata.Value.InCinemas.ToString() ?? string.Empty);
                environmentVariables.Add("Radarr_Movie_Physical_Release_Date", movie.MovieMetadata.Value.PhysicalRelease.ToString() ?? string.Empty);
                environmentVariables.Add("Radarr_Movie_Overview", movie.MovieMetadata.Value.Overview);
                environmentVariables.Add("Radarr_Release_Title", remoteMovie.Release.Title);
                environmentVariables.Add("Radarr_Release_Indexer", remoteMovie.Release.Indexer ?? string.Empty);
                environmentVariables.Add("Radarr_Release_Size", remoteMovie.Release.Size.ToString());
                environmentVariables.Add("Radarr_Release_ReleaseGroup", remoteMovie.ParsedMovieInfo.ReleaseGroup ?? string.Empty);
                environmentVariables.Add("Radarr_Release_Quality", quality.Quality.Name);
                environmentVariables.Add("Radarr_Release_QualityVersion", quality.Revision.Version.ToString());
                environmentVariables.Add("Radarr_IndexerFlags", remoteMovie.Release.IndexerFlags.ToString());
                environmentVariables.Add("Radarr_Download_Client", message.DownloadClientName ?? string.Empty);
                environmentVariables.Add("Radarr_Download_Client_Type", message.DownloadClientType ?? string.Empty);
                environmentVariables.Add("Radarr_Download_Id", message.DownloadId ?? string.Empty);
                environmentVariables.Add("Radarr_Release_CustomFormat", string.Join("|", remoteMovie.CustomFormats));
                environmentVariables.Add("Radarr_Release_CustomFormatScore", remoteMovie.CustomFormatScore.ToString());

                ExecuteScript(environmentVariables);

                return;
            }

            var series = message.Series;
            var remoteEpisode = message.Episode;
            var releaseGroup = remoteEpisode.ParsedEpisodeInfo.ReleaseGroup;
            var seriesVariables = new StringDictionary();

            AddInstanceVariables(seriesVariables, "Grab");
            AddSeriesVariables(seriesVariables, series);

            seriesVariables.Add("Sonarr_Release_EpisodeCount", remoteEpisode.Episodes.Count.ToString());
            seriesVariables.Add("Sonarr_Release_SeasonNumber", remoteEpisode.Episodes.First().SeasonNumber.ToString());
            seriesVariables.Add("Sonarr_Release_EpisodeNumbers", string.Join(",", remoteEpisode.Episodes.Select(e => e.EpisodeNumber)));
            seriesVariables.Add("Sonarr_Release_AbsoluteEpisodeNumbers", string.Join(",", remoteEpisode.Episodes.Select(e => e.AbsoluteEpisodeNumber)));
            seriesVariables.Add("Sonarr_Release_EpisodeAirDates", string.Join(",", remoteEpisode.Episodes.Select(e => e.AirDate)));
            seriesVariables.Add("Sonarr_Release_EpisodeAirDatesUtc", string.Join(",", remoteEpisode.Episodes.Select(e => e.AirDateUtc)));
            seriesVariables.Add("Sonarr_Release_EpisodeTitles", string.Join("|", remoteEpisode.Episodes.Select(e => e.Title)));
            seriesVariables.Add("Sonarr_Release_EpisodeOverviews", string.Join("|", remoteEpisode.Episodes.Select(e => e.Overview)));
            seriesVariables.Add("Sonarr_Release_FinaleTypes", string.Join("|", remoteEpisode.Episodes.Select(e => e.FinaleType)));
            seriesVariables.Add("Sonarr_Release_Title", remoteEpisode.Release.Title);
            seriesVariables.Add("Sonarr_Release_Indexer", remoteEpisode.Release.Indexer ?? string.Empty);
            seriesVariables.Add("Sonarr_Release_Size", remoteEpisode.Release.Size.ToString());
            seriesVariables.Add("Sonarr_Release_Quality", remoteEpisode.ParsedEpisodeInfo.Quality.Quality.Name);
            seriesVariables.Add("Sonarr_Release_QualityVersion", remoteEpisode.ParsedEpisodeInfo.Quality.Revision.Version.ToString());
            seriesVariables.Add("Sonarr_Release_ReleaseGroup", releaseGroup ?? string.Empty);
            seriesVariables.Add("Sonarr_Release_IndexerFlags", remoteEpisode.Release.IndexerFlags.ToString());
            seriesVariables.Add("Sonarr_Download_Client", message.DownloadClientName ?? string.Empty);
            seriesVariables.Add("Sonarr_Download_Client_Type", message.DownloadClientType ?? string.Empty);
            seriesVariables.Add("Sonarr_Download_Id", message.DownloadId ?? string.Empty);
            seriesVariables.Add("Sonarr_Release_CustomFormat", string.Join("|", remoteEpisode.CustomFormats));
            seriesVariables.Add("Sonarr_Release_CustomFormatScore", remoteEpisode.CustomFormatScore.ToString());
            seriesVariables.Add("Sonarr_Release_ReleaseType", remoteEpisode.ParsedEpisodeInfo.ReleaseType.ToString());

            ExecuteScript(seriesVariables);
        }

        public override void OnDownload(DownloadMessage message)
        {
            if (message.Movie != null)
            {
                var movie = message.Movie;
                var movieFile = message.MovieFile;
                var sourcePath = message.SourcePath;
                var environmentVariables = new StringDictionary();

                environmentVariables.Add("Radarr_EventType", "Download");
                environmentVariables.Add("Radarr_InstanceName", _configFileProvider.InstanceName);
                environmentVariables.Add("Radarr_ApplicationUrl", _configService.ApplicationUrl);
                environmentVariables.Add("Radarr_IsUpgrade", message.OldMovieFiles.Any().ToString());
                environmentVariables.Add("Radarr_Movie_Id", movie.Id.ToString());
                environmentVariables.Add("Radarr_Movie_Title", movie.MovieMetadata.Value.Title);
                environmentVariables.Add("Radarr_Movie_Year", movie.MovieMetadata.Value.Year.ToString());
                environmentVariables.Add("Radarr_Movie_OriginalLanguage", IsoLanguages.Get(movie.MovieMetadata.Value.OriginalLanguage).ThreeLetterCode);
                environmentVariables.Add("Radarr_Movie_Genres", string.Join("|", movie.MovieMetadata.Value.Genres));
                environmentVariables.Add("Radarr_Movie_Tags", string.Join("|", GetTagLabels(movie)));
                environmentVariables.Add("Radarr_Movie_Path", movie.Path);
                environmentVariables.Add("Radarr_Movie_ImdbId", movie.MovieMetadata.Value.ImdbId ?? string.Empty);
                environmentVariables.Add("Radarr_Movie_TmdbId", movie.MovieMetadata.Value.TmdbId.ToString());
                environmentVariables.Add("Radarr_Movie_In_Cinemas_Date", movie.MovieMetadata.Value.InCinemas.ToString() ?? string.Empty);
                environmentVariables.Add("Radarr_Movie_Physical_Release_Date", movie.MovieMetadata.Value.PhysicalRelease.ToString() ?? string.Empty);
                environmentVariables.Add("Radarr_Movie_Overview", movie.MovieMetadata.Value.Overview);
                environmentVariables.Add("Radarr_MovieFile_Id", movieFile.Id.ToString());
                environmentVariables.Add("Radarr_MovieFile_RelativePath", movieFile.RelativePath);
                environmentVariables.Add("Radarr_MovieFile_Path", Path.Combine(movie.Path, movieFile.RelativePath));
                environmentVariables.Add("Radarr_MovieFile_Quality", movieFile.Quality.Quality.Name);
                environmentVariables.Add("Radarr_MovieFile_QualityVersion", movieFile.Quality.Revision.Version.ToString());
                environmentVariables.Add("Radarr_MovieFile_ReleaseGroup", movieFile.ReleaseGroup ?? string.Empty);
                environmentVariables.Add("Radarr_MovieFile_SceneName", movieFile.SceneName ?? string.Empty);
                environmentVariables.Add("Radarr_MovieFile_SourcePath", sourcePath);
                environmentVariables.Add("Radarr_MovieFile_SourceFolder", Path.GetDirectoryName(sourcePath));
                environmentVariables.Add("Radarr_Download_Client", message.DownloadClientInfo?.Name ?? string.Empty);
                environmentVariables.Add("Radarr_Download_Client_Type", message.DownloadClientInfo?.Type ?? string.Empty);
                environmentVariables.Add("Radarr_Download_Id", message.DownloadId ?? string.Empty);
                environmentVariables.Add("Radarr_MovieFile_MediaInfo_AudioChannels", MediaInfoFormatter.FormatAudioChannels(movieFile.MediaInfo).ToString());
                environmentVariables.Add("Radarr_MovieFile_MediaInfo_AudioCodec", MediaInfoFormatter.FormatAudioCodec(movieFile.MediaInfo, null));
                environmentVariables.Add("Radarr_MovieFile_MediaInfo_AudioLanguages", movieFile.MediaInfo.AudioLanguages.Distinct().ConcatToString(" / "));
                environmentVariables.Add("Radarr_MovieFile_MediaInfo_Languages", movieFile.MediaInfo.AudioLanguages.ConcatToString(" / "));
                environmentVariables.Add("Radarr_MovieFile_MediaInfo_Height", movieFile.MediaInfo.Height.ToString());
                environmentVariables.Add("Radarr_MovieFile_MediaInfo_Width", movieFile.MediaInfo.Width.ToString());
                environmentVariables.Add("Radarr_MovieFile_MediaInfo_Subtitles", movieFile.MediaInfo.Subtitles.ConcatToString(" / "));
                environmentVariables.Add("Radarr_MovieFile_MediaInfo_VideoCodec", MediaInfoFormatter.FormatVideoCodec(movieFile.MediaInfo, null));
                environmentVariables.Add("Radarr_MovieFile_MediaInfo_VideoDynamicRangeType", MediaInfoFormatter.FormatVideoDynamicRangeType(movieFile.MediaInfo));
                environmentVariables.Add("Radarr_MovieFile_CustomFormat", string.Join("|", message.MovieInfo.CustomFormats));
                environmentVariables.Add("Radarr_MovieFile_CustomFormatScore", message.MovieInfo.CustomFormatScore.ToString());
                environmentVariables.Add("Radarr_Release_Indexer", message.Release?.Indexer);
                environmentVariables.Add("Radarr_Release_Size", message.Release?.Size.ToString());
                environmentVariables.Add("Radarr_Release_Title", message.Release?.Title);

                if (message.OldMovieFiles.Any())
                {
                    environmentVariables.Add("Radarr_DeletedRelativePaths", string.Join("|", message.OldMovieFiles.Select(e => e.MovieFile.RelativePath)));
                    environmentVariables.Add("Radarr_DeletedPaths", string.Join("|", message.OldMovieFiles.Select(e => Path.Combine(movie.Path, e.MovieFile.RelativePath))));
                    environmentVariables.Add("Radarr_DeletedDateAdded", string.Join("|", message.OldMovieFiles.Select(e => e.MovieFile.DateAdded)));
                    environmentVariables.Add("Radarr_DeletedRecycleBinPaths", string.Join("|", message.OldMovieFiles.Select(e => e.RecycleBinPath ?? string.Empty)));
                }

                ExecuteScript(environmentVariables);

                return;
            }

            var series = message.Series;
            var episodeFile = message.EpisodeFile;
            var episodeSourcePath = message.SourcePath;
            var seriesVariables = new StringDictionary();

            AddInstanceVariables(seriesVariables, "Download");
            AddSeriesVariables(seriesVariables, series);

            seriesVariables.Add("Sonarr_IsUpgrade", message.OldFiles.Any().ToString());
            seriesVariables.Add("Sonarr_EpisodeFile_Id", episodeFile.Id.ToString());
            seriesVariables.Add("Sonarr_EpisodeFile_EpisodeCount", episodeFile.Episodes.Value.Count.ToString());
            seriesVariables.Add("Sonarr_EpisodeFile_RelativePath", episodeFile.RelativePath);
            seriesVariables.Add("Sonarr_EpisodeFile_Path", Path.Combine(series.Path, episodeFile.RelativePath));
            seriesVariables.Add("Sonarr_EpisodeFile_EpisodeIds", string.Join(",", episodeFile.Episodes.Value.Select(e => e.Id)));
            seriesVariables.Add("Sonarr_EpisodeFile_SeasonNumber", episodeFile.SeasonNumber.ToString());
            seriesVariables.Add("Sonarr_EpisodeFile_EpisodeNumbers", string.Join(",", episodeFile.Episodes.Value.Select(e => e.EpisodeNumber)));
            seriesVariables.Add("Sonarr_EpisodeFile_EpisodeAirDates", string.Join(",", episodeFile.Episodes.Value.Select(e => e.AirDate)));
            seriesVariables.Add("Sonarr_EpisodeFile_EpisodeAirDatesUtc", string.Join(",", episodeFile.Episodes.Value.Select(e => e.AirDateUtc)));
            seriesVariables.Add("Sonarr_EpisodeFile_EpisodeTitles", string.Join("|", episodeFile.Episodes.Value.Select(e => e.Title)));
            seriesVariables.Add("Sonarr_EpisodeFile_EpisodeOverviews", string.Join("|", episodeFile.Episodes.Value.Select(e => e.Overview)));
            seriesVariables.Add("Sonarr_EpisodeFile_FinaleTypes", string.Join("|", episodeFile.Episodes.Value.Select(e => e.FinaleType)));
            seriesVariables.Add("Sonarr_EpisodeFile_Quality", episodeFile.Quality.Quality.Name);
            seriesVariables.Add("Sonarr_EpisodeFile_QualityVersion", episodeFile.Quality.Revision.Version.ToString());
            seriesVariables.Add("Sonarr_EpisodeFile_ReleaseGroup", episodeFile.ReleaseGroup ?? string.Empty);
            seriesVariables.Add("Sonarr_EpisodeFile_SceneName", episodeFile.SceneName ?? string.Empty);
            seriesVariables.Add("Sonarr_EpisodeFile_SourcePath", episodeSourcePath);
            seriesVariables.Add("Sonarr_EpisodeFile_SourceFolder", Path.GetDirectoryName(episodeSourcePath));
            seriesVariables.Add("Sonarr_Download_Client", message.DownloadClientInfo?.Name ?? string.Empty);
            seriesVariables.Add("Sonarr_Download_Client_Type", message.DownloadClientInfo?.Type ?? string.Empty);
            seriesVariables.Add("Sonarr_Download_Id", message.DownloadId ?? string.Empty);
            seriesVariables.Add("Sonarr_EpisodeFile_MediaInfo_AudioChannels", MediaInfoFormatter.FormatAudioChannels(episodeFile.MediaInfo.PrimaryAudioStream).ToString());
            seriesVariables.Add("Sonarr_EpisodeFile_MediaInfo_AudioCodec", MediaInfoFormatter.FormatAudioCodec(episodeFile.MediaInfo.PrimaryAudioStream, null));
            seriesVariables.Add("Sonarr_EpisodeFile_MediaInfo_AudioLanguages", episodeFile.MediaInfo.AudioStreams?.Select(l => l.Language).Distinct().ConcatToString(" / "));
            seriesVariables.Add("Sonarr_EpisodeFile_MediaInfo_Languages", episodeFile.MediaInfo.AudioStreams?.Select(l => l.Language).ConcatToString(" / "));
            seriesVariables.Add("Sonarr_EpisodeFile_MediaInfo_Height", episodeFile.MediaInfo.Height.ToString());
            seriesVariables.Add("Sonarr_EpisodeFile_MediaInfo_Width", episodeFile.MediaInfo.Width.ToString());
            seriesVariables.Add("Sonarr_EpisodeFile_MediaInfo_Subtitles", episodeFile.MediaInfo.SubtitleStreams?.Select(l => l.Language).ConcatToString(" / "));
            seriesVariables.Add("Sonarr_EpisodeFile_MediaInfo_VideoCodec", MediaInfoFormatter.FormatVideoCodec(episodeFile.MediaInfo, null));
            seriesVariables.Add("Sonarr_EpisodeFile_MediaInfo_VideoDynamicRangeType", MediaInfoFormatter.FormatVideoDynamicRangeType(episodeFile.MediaInfo));
            seriesVariables.Add("Sonarr_EpisodeFile_CustomFormat", string.Join("|", message.EpisodeInfo.CustomFormats));
            seriesVariables.Add("Sonarr_EpisodeFile_CustomFormatScore", message.EpisodeInfo.CustomFormatScore.ToString());
            seriesVariables.Add("Sonarr_Release_Indexer", message.Release?.Indexer);
            seriesVariables.Add("Sonarr_Release_Size", message.Release?.Size.ToString());
            seriesVariables.Add("Sonarr_Release_Title", message.Release?.Title);
            seriesVariables.Add("Sonarr_Release_ReleaseType", message.Release?.ReleaseType.ToString() ?? string.Empty);

            if (message.OldFiles.Any())
            {
                seriesVariables.Add("Sonarr_DeletedRelativePaths", string.Join("|", message.OldFiles.Select(e => e.EpisodeFile.RelativePath)));
                seriesVariables.Add("Sonarr_DeletedPaths", string.Join("|", message.OldFiles.Select(e => Path.Combine(series.Path, e.EpisodeFile.RelativePath))));
                seriesVariables.Add("Sonarr_DeletedDateAdded", string.Join("|", message.OldFiles.Select(e => e.EpisodeFile.DateAdded)));
                seriesVariables.Add("Sonarr_DeletedRecycleBinPaths", string.Join("|", message.OldFiles.Select(e => e.RecycleBinPath ?? string.Empty)));
            }

            ExecuteScript(seriesVariables);
        }

        public override void OnImportComplete(ImportCompleteMessage message)
        {
            var series = message.Series;
            var episodes = message.Episodes;
            var episodeFiles = message.EpisodeFiles;
            var sourcePath = message.SourcePath;
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "Download");
            AddSeriesVariables(environmentVariables, series);

            environmentVariables.Add("Sonarr_EpisodeFile_Ids", string.Join("|", episodeFiles.Select(f => f.Id)));
            environmentVariables.Add("Sonarr_EpisodeFile_Count", message.EpisodeFiles.Count.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_RelativePaths", string.Join("|", episodeFiles.Select(f => f.RelativePath)));
            environmentVariables.Add("Sonarr_EpisodeFile_Paths", string.Join("|", episodeFiles.Select(f => Path.Combine(series.Path, f.RelativePath))));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeIds", string.Join(",", episodes.Select(e => e.Id)));
            environmentVariables.Add("Sonarr_EpisodeFile_SeasonNumber", episodes.First().SeasonNumber.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeNumbers", string.Join(",", episodes.Select(e => e.EpisodeNumber)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeAirDates", string.Join(",", episodes.Select(e => e.AirDate)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeAirDatesUtc", string.Join(",", episodes.Select(e => e.AirDateUtc)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeTitles", string.Join("|", episodes.Select(e => e.Title)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeOverviews", string.Join("|", episodes.Select(e => e.Overview)));
            environmentVariables.Add("Sonarr_EpisodeFile_FinaleTypes", string.Join("|", episodes.Select(e => e.FinaleType)));
            environmentVariables.Add("Sonarr_EpisodeFile_Qualities", string.Join("|", episodeFiles.Select(f => f.Quality.Quality.Name)));
            environmentVariables.Add("Sonarr_EpisodeFile_QualityVersions", string.Join("|", episodeFiles.Select(f => f.Quality.Revision.Version)));
            environmentVariables.Add("Sonarr_EpisodeFile_ReleaseGroups", string.Join("|", episodeFiles.Select(f => f.ReleaseGroup)));
            environmentVariables.Add("Sonarr_EpisodeFile_SceneNames", string.Join("|", episodeFiles.Select(f => f.SceneName)));
            environmentVariables.Add("Sonarr_Download_Client", message.DownloadClientInfo?.Name ?? string.Empty);
            environmentVariables.Add("Sonarr_Download_Client_Type", message.DownloadClientInfo?.Type ?? string.Empty);
            environmentVariables.Add("Sonarr_Download_Id", message.DownloadId ?? string.Empty);
            environmentVariables.Add("Sonarr_Release_Group", message.ReleaseGroup ?? string.Empty);
            environmentVariables.Add("Sonarr_Release_Quality", message.ReleaseQuality.Quality.Name);
            environmentVariables.Add("Sonarr_Release_QualityVersion", message.ReleaseQuality.Revision.Version.ToString());
            environmentVariables.Add("Sonarr_Release_Indexer", message.Release?.Indexer ?? string.Empty);
            environmentVariables.Add("Sonarr_Release_Size", message.Release?.Size.ToString() ?? string.Empty);
            environmentVariables.Add("Sonarr_Release_Title", message.Release?.Title ?? string.Empty);

            environmentVariables.Add("Sonarr_Release_ReleaseType", message.Release == null ? message.EpisodeFiles.First().ReleaseType.ToString() : message.Release.ReleaseType.ToString());
            environmentVariables.Add("Sonarr_SourcePath", sourcePath);
            environmentVariables.Add("Sonarr_SourceFolder", Path.GetDirectoryName(sourcePath));
            environmentVariables.Add("Sonarr_DestinationPath", message.DestinationPath);
            environmentVariables.Add("Sonarr_DestinationFolder", Path.GetDirectoryName(message.DestinationPath));

            ExecuteScript(environmentVariables);
        }

        public override void OnRename(Series series, List<RenamedEpisodeFile> renamedFiles)
        {
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "Rename");
            AddSeriesVariables(environmentVariables, series);

            environmentVariables.Add("Sonarr_EpisodeFile_Ids", string.Join(",", renamedFiles.Select(e => e.EpisodeFile.Id)));
            environmentVariables.Add("Sonarr_EpisodeFile_RelativePaths", string.Join("|", renamedFiles.Select(e => e.EpisodeFile.RelativePath)));
            environmentVariables.Add("Sonarr_EpisodeFile_Paths", string.Join("|", renamedFiles.Select(e => Path.Combine(series.Path, e.EpisodeFile.RelativePath))));
            environmentVariables.Add("Sonarr_EpisodeFile_PreviousRelativePaths", string.Join("|", renamedFiles.Select(e => e.PreviousRelativePath)));
            environmentVariables.Add("Sonarr_EpisodeFile_PreviousPaths", string.Join("|", renamedFiles.Select(e => e.PreviousPath)));

            ExecuteScript(environmentVariables);
        }

        public override void OnEpisodeFileDelete(EpisodeDeleteMessage deleteMessage)
        {
            var series = deleteMessage.Series;
            var episodeFile = deleteMessage.EpisodeFile;

            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "EpisodeFileDelete");
            AddSeriesVariables(environmentVariables, series);

            environmentVariables.Add("Sonarr_EpisodeFile_Id", episodeFile.Id.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeCount", episodeFile.Episodes.Value.Count.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_RelativePath", episodeFile.RelativePath);
            environmentVariables.Add("Sonarr_EpisodeFile_Path", Path.Combine(series.Path, episodeFile.RelativePath));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeIds", string.Join(",", episodeFile.Episodes.Value.Select(e => e.Id)));
            environmentVariables.Add("Sonarr_EpisodeFile_SeasonNumber", episodeFile.SeasonNumber.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeNumbers", string.Join(",", episodeFile.Episodes.Value.Select(e => e.EpisodeNumber)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeAirDates", string.Join(",", episodeFile.Episodes.Value.Select(e => e.AirDate)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeAirDatesUtc", string.Join(",", episodeFile.Episodes.Value.Select(e => e.AirDateUtc)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeTitles", string.Join("|", episodeFile.Episodes.Value.Select(e => e.Title)));
            environmentVariables.Add("Sonarr_EpisodeFile_EpisodeOverviews", string.Join("|", episodeFile.Episodes.Value.Select(e => e.Overview)));
            environmentVariables.Add("Sonarr_EpisodeFile_Quality", episodeFile.Quality.Quality.Name);
            environmentVariables.Add("Sonarr_EpisodeFile_QualityVersion", episodeFile.Quality.Revision.Version.ToString());
            environmentVariables.Add("Sonarr_EpisodeFile_ReleaseGroup", episodeFile.ReleaseGroup ?? string.Empty);
            environmentVariables.Add("Sonarr_EpisodeFile_SceneName", episodeFile.SceneName ?? string.Empty);

            ExecuteScript(environmentVariables);
        }

        public override void OnSeriesAdd(SeriesAddMessage message)
        {
            var series = message.Series;
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "SeriesAdd");
            AddSeriesVariables(environmentVariables, series);

            ExecuteScript(environmentVariables);
        }

        public override void OnSeriesDelete(SeriesDeleteMessage deleteMessage)
        {
            var series = deleteMessage.Series;
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "SeriesDelete");
            AddSeriesVariables(environmentVariables, series);

            ExecuteScript(environmentVariables);
        }

        public override void OnMovieRename(Movie movie, List<RenamedMovieFile> renamedFiles)
        {
            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Radarr_EventType", "Rename");
            environmentVariables.Add("Radarr_InstanceName", _configFileProvider.InstanceName);
            environmentVariables.Add("Radarr_ApplicationUrl", _configService.ApplicationUrl);
            environmentVariables.Add("Radarr_Movie_Id", movie.Id.ToString());
            environmentVariables.Add("Radarr_Movie_Title", movie.MovieMetadata.Value.Title);
            environmentVariables.Add("Radarr_Movie_Year", movie.MovieMetadata.Value.Year.ToString());
            environmentVariables.Add("Radarr_Movie_OriginalLanguage", IsoLanguages.Get(movie.MovieMetadata.Value.OriginalLanguage).ThreeLetterCode);
            environmentVariables.Add("Radarr_Movie_Genres", string.Join("|", movie.MovieMetadata.Value.Genres));
            environmentVariables.Add("Radarr_Movie_Tags", string.Join("|", GetTagLabels(movie)));
            environmentVariables.Add("Radarr_Movie_Path", movie.Path);
            environmentVariables.Add("Radarr_Movie_ImdbId", movie.MovieMetadata.Value.ImdbId ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_TmdbId", movie.MovieMetadata.Value.TmdbId.ToString());
            environmentVariables.Add("Radarr_Movie_In_Cinemas_Date", movie.MovieMetadata.Value.InCinemas.ToString() ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_Physical_Release_Date", movie.MovieMetadata.Value.PhysicalRelease.ToString() ?? string.Empty);
            environmentVariables.Add("Radarr_MovieFile_Ids", string.Join(",", renamedFiles.Select(e => e.MovieFile.Id)));
            environmentVariables.Add("Radarr_MovieFile_RelativePaths", string.Join("|", renamedFiles.Select(e => e.MovieFile.RelativePath)));
            environmentVariables.Add("Radarr_MovieFile_Paths", string.Join("|", renamedFiles.Select(e => Path.Combine(movie.Path, e.MovieFile.RelativePath))));
            environmentVariables.Add("Radarr_MovieFile_PreviousRelativePaths", string.Join("|", renamedFiles.Select(e => e.PreviousRelativePath)));
            environmentVariables.Add("Radarr_MovieFile_PreviousPaths", string.Join("|", renamedFiles.Select(e => e.PreviousPath)));

            ExecuteScript(environmentVariables);
        }

        public override void OnMovieAdded(Movie movie)
        {
            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Radarr_EventType", "MovieAdded");
            environmentVariables.Add("Radarr_InstanceName", _configFileProvider.InstanceName);
            environmentVariables.Add("Radarr_ApplicationUrl", _configService.ApplicationUrl);
            environmentVariables.Add("Radarr_Movie_Id", movie.Id.ToString());
            environmentVariables.Add("Radarr_Movie_Title", movie.MovieMetadata.Value.Title);
            environmentVariables.Add("Radarr_Movie_Year", movie.MovieMetadata.Value.Year.ToString());
            environmentVariables.Add("Radarr_Movie_OriginalLanguage", IsoLanguages.Get(movie.MovieMetadata.Value.OriginalLanguage).ThreeLetterCode);
            environmentVariables.Add("Radarr_Movie_Genres", string.Join("|", movie.MovieMetadata.Value.Genres));
            environmentVariables.Add("Radarr_Movie_Tags", string.Join("|", GetTagLabels(movie)));
            environmentVariables.Add("Radarr_Movie_Path", movie.Path);
            environmentVariables.Add("Radarr_Movie_ImdbId", movie.MovieMetadata.Value.ImdbId ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_TmdbId", movie.MovieMetadata.Value.TmdbId.ToString());
            environmentVariables.Add("Radarr_Movie_AddMethod", movie.AddOptions.AddMethod.ToString());

            ExecuteScript(environmentVariables);
        }

        public override void OnMovieFileDelete(MovieFileDeleteMessage deleteMessage)
        {
            var movie = deleteMessage.Movie;
            var movieFile = deleteMessage.MovieFile;

            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Radarr_EventType", "MovieFileDelete");
            environmentVariables.Add("Radarr_InstanceName", _configFileProvider.InstanceName);
            environmentVariables.Add("Radarr_ApplicationUrl", _configService.ApplicationUrl);
            environmentVariables.Add("Radarr_MovieFile_DeleteReason", deleteMessage.Reason.ToString());
            environmentVariables.Add("Radarr_Movie_Id", movie.Id.ToString());
            environmentVariables.Add("Radarr_Movie_Title", movie.Title);
            environmentVariables.Add("Radarr_Movie_Year", movie.Year.ToString());
            environmentVariables.Add("Radarr_Movie_OriginalLanguage", IsoLanguages.Get(movie.MovieMetadata.Value.OriginalLanguage).ThreeLetterCode);
            environmentVariables.Add("Radarr_Movie_Genres", string.Join("|", movie.MovieMetadata.Value.Genres));
            environmentVariables.Add("Radarr_Movie_Tags", string.Join("|", GetTagLabels(movie)));
            environmentVariables.Add("Radarr_Movie_Path", movie.Path);
            environmentVariables.Add("Radarr_Movie_ImdbId", movie.MovieMetadata.Value.ImdbId ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_TmdbId", movie.MovieMetadata.Value.TmdbId.ToString());
            environmentVariables.Add("Radarr_Movie_Overview", movie.MovieMetadata.Value.Overview);
            environmentVariables.Add("Radarr_MovieFile_Id", movieFile.Id.ToString());
            environmentVariables.Add("Radarr_MovieFile_RelativePath", movieFile.RelativePath);
            environmentVariables.Add("Radarr_MovieFile_Path", Path.Combine(movie.Path, movieFile.RelativePath));
            environmentVariables.Add("Radarr_MovieFile_Size", movieFile.Size.ToString());
            environmentVariables.Add("Radarr_MovieFile_Quality", movieFile.Quality.Quality.Name);
            environmentVariables.Add("Radarr_MovieFile_QualityVersion", movieFile.Quality.Revision.Version.ToString());
            environmentVariables.Add("Radarr_MovieFile_ReleaseGroup", movieFile.ReleaseGroup ?? string.Empty);
            environmentVariables.Add("Radarr_MovieFile_SceneName", movieFile.SceneName ?? string.Empty);

            ExecuteScript(environmentVariables);
        }

        public override void OnMovieDelete(MovieDeleteMessage deleteMessage)
        {
            var movie = deleteMessage.Movie;
            var environmentVariables = new StringDictionary();

            environmentVariables.Add("Radarr_EventType", "MovieDelete");
            environmentVariables.Add("Radarr_InstanceName", _configFileProvider.InstanceName);
            environmentVariables.Add("Radarr_ApplicationUrl", _configService.ApplicationUrl);
            environmentVariables.Add("Radarr_Movie_Id", movie.Id.ToString());
            environmentVariables.Add("Radarr_Movie_Title", movie.MovieMetadata.Value.Title);
            environmentVariables.Add("Radarr_Movie_Year", movie.MovieMetadata.Value.Year.ToString());
            environmentVariables.Add("Radarr_Movie_OriginalLanguage", IsoLanguages.Get(movie.MovieMetadata.Value.OriginalLanguage).ThreeLetterCode);
            environmentVariables.Add("Radarr_Movie_Genres", string.Join("|", movie.MovieMetadata.Value.Genres));
            environmentVariables.Add("Radarr_Movie_Tags", string.Join("|", GetTagLabels(movie)));
            environmentVariables.Add("Radarr_Movie_Path", movie.Path);
            environmentVariables.Add("Radarr_Movie_ImdbId", movie.MovieMetadata.Value.ImdbId ?? string.Empty);
            environmentVariables.Add("Radarr_Movie_TmdbId", movie.MovieMetadata.Value.TmdbId.ToString());
            environmentVariables.Add("Radarr_Movie_DeletedFiles", deleteMessage.DeletedFiles.ToString());
            environmentVariables.Add("Radarr_Movie_Overview", movie.MovieMetadata.Value.Overview);

            if (deleteMessage.DeletedFiles && movie.MovieFile != null)
            {
                environmentVariables.Add("Radarr_Movie_Folder_Size", movie.MovieFile.Size.ToString());
            }

            ExecuteScript(environmentVariables);
        }

        public override void OnHealthIssue(HealthCheck.HealthCheck healthCheck)
        {
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "HealthIssue");
            AddRadarrInstanceVariables(environmentVariables, "HealthIssue");

            environmentVariables.Add("Sonarr_Health_Issue_Level", Enum.GetName(typeof(HealthCheckResult), healthCheck.Type));
            environmentVariables.Add("Sonarr_Health_Issue_Message", healthCheck.Message);
            environmentVariables.Add("Sonarr_Health_Issue_Type", healthCheck.Source.Name);
            environmentVariables.Add("Sonarr_Health_Issue_Wiki", healthCheck.WikiUrl.ToString() ?? string.Empty);
            environmentVariables.Add("Radarr_Health_Issue_Level", Enum.GetName(typeof(HealthCheckResult), healthCheck.Type));
            environmentVariables.Add("Radarr_Health_Issue_Message", healthCheck.Message);
            environmentVariables.Add("Radarr_Health_Issue_Type", healthCheck.Source.Name);
            environmentVariables.Add("Radarr_Health_Issue_Wiki", healthCheck.WikiUrl.ToString() ?? string.Empty);

            ExecuteScript(environmentVariables);
        }

        public override void OnHealthRestored(HealthCheck.HealthCheck previousCheck)
        {
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "HealthRestored");
            AddRadarrInstanceVariables(environmentVariables, "HealthRestored");

            environmentVariables.Add("Sonarr_Health_Restored_Level", Enum.GetName(typeof(HealthCheckResult), previousCheck.Type));
            environmentVariables.Add("Sonarr_Health_Restored_Message", previousCheck.Message);
            environmentVariables.Add("Sonarr_Health_Restored_Type", previousCheck.Source.Name);
            environmentVariables.Add("Sonarr_Health_Restored_Wiki", previousCheck.WikiUrl.ToString() ?? string.Empty);
            environmentVariables.Add("Radarr_Health_Restored_Level", Enum.GetName(typeof(HealthCheckResult), previousCheck.Type));
            environmentVariables.Add("Radarr_Health_Restored_Message", previousCheck.Message);
            environmentVariables.Add("Radarr_Health_Restored_Type", previousCheck.Source.Name);
            environmentVariables.Add("Radarr_Health_Restored_Wiki", previousCheck.WikiUrl.ToString() ?? string.Empty);

            ExecuteScript(environmentVariables);
        }

        public override void OnApplicationUpdate(ApplicationUpdateMessage updateMessage)
        {
            var environmentVariables = new StringDictionary();

            AddInstanceVariables(environmentVariables, "ApplicationUpdate");
            AddRadarrInstanceVariables(environmentVariables, "ApplicationUpdate");

            environmentVariables.Add("Sonarr_Update_Message", updateMessage.Message);
            environmentVariables.Add("Sonarr_Update_NewVersion", updateMessage.NewVersion.ToString());
            environmentVariables.Add("Sonarr_Update_PreviousVersion", updateMessage.PreviousVersion.ToString());
            environmentVariables.Add("Radarr_Update_Message", updateMessage.Message);
            environmentVariables.Add("Radarr_Update_NewVersion", updateMessage.NewVersion.ToString());
            environmentVariables.Add("Radarr_Update_PreviousVersion", updateMessage.PreviousVersion.ToString());

            ExecuteScript(environmentVariables);
        }

        public override void OnManualInteractionRequired(ManualInteractionRequiredMessage message)
        {
            var environmentVariables = new StringDictionary();

            if (message.Movie != null)
            {
                var movie = message.Movie;

                AddRadarrInstanceVariables(environmentVariables, "ManualInteractionRequired");

                environmentVariables.Add("Radarr_Movie_Id", movie?.Id.ToString());
                environmentVariables.Add("Radarr_Movie_Title", movie?.MovieMetadata.Value.Title);
                environmentVariables.Add("Radarr_Movie_Year", movie?.MovieMetadata.Value.Year.ToString());
                environmentVariables.Add("Radarr_Movie_OriginalLanguage", IsoLanguages.Get(movie?.MovieMetadata.Value.OriginalLanguage)?.ThreeLetterCode);
                environmentVariables.Add("Radarr_Movie_Genres", string.Join("|", movie?.MovieMetadata.Value.Genres ?? new List<string>()));
                environmentVariables.Add("Radarr_Movie_Tags", string.Join("|", GetTagLabels(movie)));
                environmentVariables.Add("Radarr_Movie_Path", movie?.Path);
                environmentVariables.Add("Radarr_Movie_ImdbId", movie?.MovieMetadata.Value.ImdbId ?? string.Empty);
                environmentVariables.Add("Radarr_Movie_TmdbId", movie?.MovieMetadata.Value.TmdbId.ToString());
                environmentVariables.Add("Radarr_Movie_Overview", movie?.MovieMetadata.Value.Overview);
                environmentVariables.Add("Radarr_Download_Client", message.DownloadClientInfo?.Name ?? string.Empty);
                environmentVariables.Add("Radarr_Download_Client_Type", message.DownloadClientInfo?.Type ?? string.Empty);
                environmentVariables.Add("Radarr_Download_Id", message.DownloadId ?? string.Empty);
                environmentVariables.Add("Radarr_Download_Size", message.TrackedDownload.DownloadItem?.TotalSize.ToString() ?? string.Empty);
                environmentVariables.Add("Radarr_Download_Title", message.TrackedDownload.DownloadItem?.Title ?? string.Empty);

                ExecuteScript(environmentVariables);

                return;
            }

            var series = message.Series;

            AddInstanceVariables(environmentVariables, "ManualInteractionRequired");
            AddSeriesVariables(environmentVariables, series);

            environmentVariables.Add("Sonarr_Download_Client", message.DownloadClientInfo?.Name ?? string.Empty);
            environmentVariables.Add("Sonarr_Download_Client_Type", message.DownloadClientInfo?.Type ?? string.Empty);
            environmentVariables.Add("Sonarr_Download_Id", message.DownloadId ?? string.Empty);
            environmentVariables.Add("Sonarr_Download_Size", message.TrackedDownload.DownloadItem.TotalSize.ToString());
            environmentVariables.Add("Sonarr_Download_Title", message.TrackedDownload.DownloadItem.Title);

            ExecuteScript(environmentVariables);
        }

        public override ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            if (!_diskProvider.FileExists(Settings.Path))
            {
                failures.Add(new NzbDroneValidationFailure("Path", _localizationService.GetLocalizedString("NotificationsCustomScriptValidationFileDoesNotExist")));
            }

            if (failures.Empty())
            {
                try
                {
                    var environmentVariables = new StringDictionary();
                    environmentVariables.Add("Sonarr_EventType", "Test");
                    environmentVariables.Add("Sonarr_InstanceName", _configFileProvider.InstanceName);
                    environmentVariables.Add("Sonarr_ApplicationUrl", _configService.ApplicationUrl);
                    environmentVariables.Add("Radarr_EventType", "Test");
                    environmentVariables.Add("Radarr_InstanceName", _configFileProvider.InstanceName);
                    environmentVariables.Add("Radarr_ApplicationUrl", _configService.ApplicationUrl);

                    var processOutput = ExecuteScript(environmentVariables);

                    if (processOutput.ExitCode != 0)
                    {
                        failures.Add(new NzbDroneValidationFailure(string.Empty, $"Script exited with code: {processOutput.ExitCode}"));
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                    failures.Add(new NzbDroneValidationFailure(string.Empty, ex.Message));
                }
            }

            return new ValidationResult(failures);
        }

        private ProcessOutput ExecuteScript(StringDictionary environmentVariables)
        {
            if (environmentVariables.AddTheoriarrAliases() && Interlocked.Exchange(ref _theoriarrAliasWarningLogged, 1) == 0)
            {
                _logger.Warn("Emitting Theoriarr_* custom script variables alongside the deprecated Sonarr_*/Radarr_* names. " +
                             "Update your scripts to read Theoriarr_*; the legacy names will be removed in a future release.");
            }

            _logger.Debug("Executing external script: {0}", Settings.Path);

            var processOutput = _processProvider.StartAndCapture(Settings.Path, Settings.Arguments, environmentVariables);

            _logger.Debug("Executed external script: {0} - Status: {1}", Settings.Path, processOutput.ExitCode);
            _logger.Debug("Script Output: \r\n{0}", string.Join("\r\n", processOutput.Lines));

            return processOutput;
        }

        private bool ValidatePathParent(string possibleParent, string path)
        {
            return possibleParent.IsParentPath(path);
        }

        private List<string> GetTagLabels(Series series)
        {
            if (series == null)
            {
                return new List<string>();
            }

            return _tagRepository.GetTags(series.Tags)
                .Select(s => s.Label)
                .Where(l => l.IsNotNullOrWhiteSpace())
                .OrderBy(l => l)
                .ToList();
        }

        private List<string> GetTagLabels(Movie movie)
        {
            if (movie == null)
            {
                return new List<string>();
            }

            return _tagRepository.GetTags(movie.Tags)
                .Select(t => t.Label)
                .Where(l => l.IsNotNullOrWhiteSpace())
                .OrderBy(l => l)
                .ToList();
        }

        private void AddInstanceVariables(StringDictionary environmentVariables, string eventType)
        {
            environmentVariables.Add("Sonarr_EventType", eventType);
            environmentVariables.Add("Sonarr_InstanceName", _configFileProvider.InstanceName);
            environmentVariables.Add("Sonarr_ApplicationUrl", _configService.ApplicationUrl);
        }

        private void AddRadarrInstanceVariables(StringDictionary environmentVariables, string eventType)
        {
            environmentVariables.Add("Radarr_EventType", eventType);
            environmentVariables.Add("Radarr_InstanceName", _configFileProvider.InstanceName);
            environmentVariables.Add("Radarr_ApplicationUrl", _configService.ApplicationUrl);
        }

        private void AddSeriesVariables(StringDictionary environmentVariables, Series series)
        {
            environmentVariables.Add("Sonarr_Series_Id", series.Id.ToString());
            environmentVariables.Add("Sonarr_Series_Title", series.Title);
            environmentVariables.Add("Sonarr_Series_TitleSlug", series.TitleSlug);
            environmentVariables.Add("Sonarr_Series_Path", series.Path);
            environmentVariables.Add("Sonarr_Series_TvdbId", series.TvdbId.ToString());
            environmentVariables.Add("Sonarr_Series_TvMazeId", series.TvMazeId.ToString());
            environmentVariables.Add("Sonarr_Series_TmdbId", series.TmdbId.ToString());
            environmentVariables.Add("Sonarr_Series_ImdbId", series.ImdbId ?? string.Empty);
            environmentVariables.Add("Sonarr_Series_Type", series.SeriesType.ToString());
            environmentVariables.Add("Sonarr_Series_Year", series.Year.ToString());
            environmentVariables.Add("Sonarr_Series_OriginalCountry", series.OriginalCountry);
            environmentVariables.Add("Sonarr_Series_OriginalLanguage", IsoLanguages.Get(series.OriginalLanguage).ThreeLetterCode);
            environmentVariables.Add("Sonarr_Series_Genres", string.Join("|", series.Genres));
            environmentVariables.Add("Sonarr_Series_Tags", string.Join("|", GetTagLabels(series)));
        }
    }
}
