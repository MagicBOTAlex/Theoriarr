using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Extras.Metadata.Files;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Credits;
using NzbDrone.Core.Movies.Translations;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Extras.Metadata.Consumers.Xbmc
{
    public class XbmcMetadata : MetadataBase<XbmcMetadataSettings>
    {
        private readonly Logger _logger;
        private readonly IMapCoversToLocal _mediaCoverService;
        private readonly ITagRepository _tagRepo;
        private readonly IDetectXbmcNfo _detectNfo;
        private readonly IDiskProvider _diskProvider;
        private readonly ICreditService _creditService;
        private readonly IMovieTranslationService _movieTranslationsService;

        public XbmcMetadata(IDetectXbmcNfo detectNfo,
                            IDiskProvider diskProvider,
                            IMapCoversToLocal mediaCoverService,
                            ITagRepository tagRepo,
                            ICreditService creditService,
                            IMovieTranslationService movieTranslationsService,
                            Logger logger)
        {
            _logger = logger;
            _mediaCoverService = mediaCoverService;
            _tagRepo = tagRepo;
            _diskProvider = diskProvider;
            _detectNfo = detectNfo;
            _creditService = creditService;
            _movieTranslationsService = movieTranslationsService;
        }

        private static readonly Regex SeriesImagesRegex = new Regex(@"^(?<type>poster|banner|fanart)\.(?:png|jpg)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SeasonImagesRegex = new Regex(@"^season(?<season>\d{2,}|-all|-specials)-(?<type>poster|banner|fanart)\.(?:png|jpg)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex EpisodeImageRegex = new Regex(@"-thumb\.(?:png|jpg)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MovieImagesRegex = new Regex(@"^(?<type>poster|banner|fanart|clearart|discart|keyart|landscape|logo|backdrop|clearlogo)\.(?:png|jpe?g)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MovieFileImageRegex = new Regex(@"(?<type>-thumb|-poster|-banner|-fanart|-clearart|-discart|-keyart|-landscape|-logo|-backdrop|-clearlogo)\.(?:png|jpe?g)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public override string Name => "Kodi (XBMC) / Emby";

        // ---------------------------------------------------------------- SERIES

        public override string GetFilenameAfterMove(Series series, EpisodeFile episodeFile, MetadataFile metadataFile)
        {
            var episodeFilePath = Path.Combine(series.Path, episodeFile.RelativePath);

            if (metadataFile.Type == MetadataType.EpisodeImage)
            {
                return GetEpisodeImageFilename(episodeFilePath);
            }

            if (metadataFile.Type == MetadataType.EpisodeMetadata)
            {
                return GetEpisodeMetadataFilename(episodeFilePath);
            }

            _logger.Debug("Unknown episode file metadata: {0}", metadataFile.RelativePath);
            return Path.Combine(series.Path, metadataFile.RelativePath);
        }

        public override MetadataFile FindMetadataFile(Series series, string path)
        {
            var filename = Path.GetFileName(path);

            if (filename == null)
            {
                return null;
            }

            var metadata = new MetadataFile
            {
                SeriesId = series.Id,
                MediaType = NzbDrone.Core.Extras.Files.MediaType.Series,
                Consumer = GetType().Name,
                RelativePath = series.Path.GetRelativePath(path)
            };

            if (SeriesImagesRegex.IsMatch(filename))
            {
                metadata.Type = MetadataType.SeriesImage;
                return metadata;
            }

            var seasonMatch = SeasonImagesRegex.Match(filename);

            if (seasonMatch.Success)
            {
                metadata.Type = MetadataType.SeasonImage;

                var seasonNumberMatch = seasonMatch.Groups["season"].Value;

                if (seasonNumberMatch.Contains("specials"))
                {
                    metadata.SeasonNumber = 0;
                }
                else if (int.TryParse(seasonNumberMatch, out var seasonNumber))
                {
                    metadata.SeasonNumber = seasonNumber;
                }
                else
                {
                    return null;
                }

                return metadata;
            }

            if (EpisodeImageRegex.IsMatch(filename))
            {
                metadata.Type = MetadataType.EpisodeImage;
                return metadata;
            }

            if (filename.Equals("tvshow.nfo", StringComparison.OrdinalIgnoreCase))
            {
                metadata.Type = MetadataType.SeriesMetadata;
                return metadata;
            }

            var parseResult = Parser.Parser.ParseTitle(filename);

            if (parseResult != null &&
                !parseResult.FullSeason &&
                Path.GetExtension(filename).Equals(".nfo", StringComparison.OrdinalIgnoreCase) &&
                _detectNfo.IsXbmcNfoFile(path))
            {
                metadata.Type = MetadataType.EpisodeMetadata;
                return metadata;
            }

            return null;
        }

        public override MetadataFileResult SeriesMetadata(Series series, SeriesMetadataReason reason)
        {
            if (reason == SeriesMetadataReason.EpisodesImported)
            {
                return null;
            }

            var xmlResult = string.Empty;

            if (Settings.SeriesMetadata)
            {
                _logger.Debug("Generating Series Metadata for: {0}", series.Title);

                var tvShow = new XElement("tvshow");

                tvShow.Add(new XElement("title", series.Title));

                if (series.Ratings != null && series.Ratings.Votes > 0)
                {
                    tvShow.Add(new XElement("rating", series.Ratings.Value));
                }

                tvShow.Add(new XElement("plot", series.Overview));
                tvShow.Add(new XElement("mpaa", series.Certification));
                tvShow.Add(new XElement("id", series.TvdbId));

                var uniqueId = new XElement("uniqueid", series.TvdbId);
                uniqueId.SetAttributeValue("type", "tvdb");
                uniqueId.SetAttributeValue("default", true);
                tvShow.Add(uniqueId);

                if (series.ImdbId.IsNotNullOrWhiteSpace())
                {
                    var imdbId = new XElement("uniqueid", series.ImdbId);
                    imdbId.SetAttributeValue("type", "imdb");
                    tvShow.Add(imdbId);
                }

                if (series.TmdbId > 0)
                {
                    var tmdbId = new XElement("uniqueid", series.TmdbId);
                    tmdbId.SetAttributeValue("type", "tmdb");
                    tvShow.Add(tmdbId);
                }

                if (series.TvMazeId > 0)
                {
                    var tvMazeId = new XElement("uniqueid", series.TvMazeId);
                    tvMazeId.SetAttributeValue("type", "tvmaze");
                    tvShow.Add(tvMazeId);
                }

                foreach (var genre in series.Genres)
                {
                    tvShow.Add(new XElement("genre", genre));
                }

                if (series.Tags.Any())
                {
                    var tags = _tagRepo.GetTags(series.Tags);

                    foreach (var tag in tags)
                    {
                        tvShow.Add(new XElement("tag", tag.Label));
                    }
                }

                tvShow.Add(new XElement("status", series.Status));

                if (series.FirstAired.HasValue)
                {
                    tvShow.Add(new XElement("premiered", series.FirstAired.Value.ToString("yyyy-MM-dd")));
                }

                // Add support for Jellyfin's "enddate" tag
                if (series.Status == SeriesStatusType.Ended && series.LastAired.HasValue)
                {
                    tvShow.Add(new XElement("enddate", series.LastAired.Value.ToString("yyyy-MM-dd")));
                }

                tvShow.Add(new XElement("studio", series.Network));

                foreach (var actor in series.Actors)
                {
                    var xmlActor = new XElement("actor",
                        new XElement("name", actor.Name),
                        new XElement("role", actor.Character));

                    if (actor.Images.Any())
                    {
                        xmlActor.Add(new XElement("thumb", actor.Images.First().RemoteUrl));
                    }

                    tvShow.Add(xmlActor);
                }

                if (Settings.SeriesMetadataEpisodeGuide)
                {
                    var episodeGuide = new KodiEpisodeGuide(series);
                    var serializerSettings = STJson.GetSerializerSettings();
                    serializerSettings.WriteIndented = false;

                    tvShow.Add(new XElement("episodeguide", JsonSerializer.Serialize(episodeGuide, serializerSettings)));
                }

                var doc = new XDocument(tvShow)
                {
                    Declaration = new XDeclaration("1.0", "UTF-8", "yes"),
                };

                var sb = new StringBuilder();
                using var sw = new Utf8StringWriter();
                using var xw = XmlWriter.Create(sw, new XmlWriterSettings
                {
                    Encoding = Encoding.UTF8,
                    Indent = true
                });

                doc.Save(xw);
                xw.Flush();

                xmlResult += sw.ToString();
            }

            if (Settings.SeriesMetadataUrl)
            {
                if (Settings.SeriesMetadata)
                {
                    xmlResult += Environment.NewLine;
                }

                xmlResult += "https://www.thetvdb.com/?tab=series&id=" + series.TvdbId;
            }

            return xmlResult.IsNullOrWhiteSpace() ? null : new MetadataFileResult("tvshow.nfo", xmlResult);
        }

        public override MetadataFileResult EpisodeMetadata(Series series, EpisodeFile episodeFile)
        {
            if (!Settings.EpisodeMetadata)
            {
                return null;
            }

            _logger.Debug("Generating Episode Metadata for: {0}", Path.Combine(series.Path, episodeFile.RelativePath));

            var watched = GetExistingWatchedStatus(series, episodeFile.RelativePath);

            var xws = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Indent = true,
                ConformanceLevel =  ConformanceLevel.Fragment
            };

            using var sw = new Utf8StringWriter();
            using var xw = XmlWriter.Create(sw, xws);

            xw.WriteProcessingInstruction("xml", "version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"");

            foreach (var episode in episodeFile.Episodes.Value)
            {
                var image = episode.Images.SingleOrDefault(i => i.CoverType == MediaCoverTypes.Screenshot);

                var details = new XElement("episodedetails");
                details.Add(new XElement("title", episode.Title));
                details.Add(new XElement("season", episode.SeasonNumber));
                details.Add(new XElement("episode", episode.EpisodeNumber));
                details.Add(new XElement("aired", episode.AirDate));
                details.Add(new XElement("plot", episode.Overview));

                if (episode.SeasonNumber == 0 && episode.AiredAfterSeasonNumber.HasValue)
                {
                    details.Add(new XElement("displayafterseason", episode.AiredAfterSeasonNumber));
                }
                else if (episode.SeasonNumber == 0 && episode.AiredBeforeSeasonNumber.HasValue)
                {
                    details.Add(new XElement("displayseason", episode.AiredBeforeSeasonNumber));
                    details.Add(new XElement("displayepisode", episode.AiredBeforeEpisodeNumber ?? -1));
                }

                var tvdbId = new XElement("uniqueid", episode.TvdbId);
                tvdbId.SetAttributeValue("type", "tvdb");
                tvdbId.SetAttributeValue("default", true);
                details.Add(tvdbId);

                var sonarrId = new XElement("uniqueid", episode.Id);
                sonarrId.SetAttributeValue("type", "sonarr");
                details.Add(sonarrId);

                if (image == null)
                {
                    details.Add(new XElement("thumb"));
                }
                else if (Settings.EpisodeImageThumb)
                {
                    details.Add(new XElement("thumb", image.RemoteUrl));
                }

                details.Add(new XElement("watched", watched));

                if (episode.Ratings != null && episode.Ratings.Votes > 0)
                {
                    details.Add(new XElement("rating", episode.Ratings.Value));
                }

                if (episodeFile.MediaInfo != null)
                {
                    var sceneName = episodeFile.GetSceneOrFileName();

                    var fileInfo = new XElement("fileinfo");
                    var streamDetails = new XElement("streamdetails");

                    var video = new XElement("video");
                    video.Add(new XElement("aspect", (float)episodeFile.MediaInfo.Width / (float)episodeFile.MediaInfo.Height));
                    video.Add(new XElement("bitrate", episodeFile.MediaInfo.VideoBitrate));
                    video.Add(new XElement("codec", episodeFile.MediaInfo.VideoFormat));
                    video.Add(new XElement("framerate", episodeFile.MediaInfo.VideoFps.ToString("0.###")));
                    video.Add(new XElement("height", episodeFile.MediaInfo.Height));
                    video.Add(new XElement("scantype", episodeFile.MediaInfo.ScanType));
                    video.Add(new XElement("width", episodeFile.MediaInfo.Width));

                    video.Add(new XElement("duration", episodeFile.MediaInfo.RunTime.TotalMinutes));
                    video.Add(new XElement("durationinseconds", Math.Round(episodeFile.MediaInfo.RunTime.TotalSeconds)));

                    if (episodeFile.MediaInfo.VideoHdrFormat is HdrFormat.DolbyVision or HdrFormat.DolbyVisionHdr10 or HdrFormat.DolbyVisionHdr10Plus or HdrFormat.DolbyVisionHlg or HdrFormat.DolbyVisionSdr)
                    {
                        video.Add(new XElement("hdrtype", "dolbyvision"));
                    }
                    else if (episodeFile.MediaInfo.VideoHdrFormat is HdrFormat.Hdr10 or HdrFormat.Hdr10Plus or HdrFormat.Pq10)
                    {
                        video.Add(new XElement("hdrtype", "hdr10"));
                    }
                    else if (episodeFile.MediaInfo.VideoHdrFormat == HdrFormat.Hlg10)
                    {
                        video.Add(new XElement("hdrtype", "hlg"));
                    }
                    else if (episodeFile.MediaInfo.VideoHdrFormat == HdrFormat.None)
                    {
                        video.Add(new XElement("hdrtype", ""));
                    }

                    streamDetails.Add(video);

                    if (episodeFile.MediaInfo.AudioStreams is { Count: > 0 })
                    {
                        foreach (var audioStream in episodeFile.MediaInfo.AudioStreams)
                        {
                            var audio = new XElement("audio");
                            audio.Add(new XElement("bitrate", audioStream.Bitrate));
                            audio.Add(new XElement("channels", audioStream.Channels));
                            audio.Add(new XElement("codec", XbmcMetadataFormatter.FormatAudioCodec(audioStream)));
                            audio.Add(new XElement("language", audioStream.Language));
                            streamDetails.Add(audio);
                        }
                    }

                    if (episodeFile.MediaInfo.SubtitleStreams is { Count: > 0 })
                    {
                        foreach (var subtitleStream in episodeFile.MediaInfo.SubtitleStreams)
                        {
                            var subtitle = new XElement("subtitle");
                            subtitle.Add(new XElement("language", subtitleStream.Language));
                            streamDetails.Add(subtitle);
                        }
                    }

                    fileInfo.Add(streamDetails);
                    details.Add(fileInfo);
                }

                // Todo: get guest stars, writer and director
                // details.Add(new XElement("credits", tvdbEpisode.Writer.FirstOrDefault()));
                // details.Add(new XElement("director", tvdbEpisode.Directors.FirstOrDefault()));

                details.WriteTo(xw);
            }

            xw.Flush();
            var xmlResult = sw.ToString();

            return new MetadataFileResult(GetEpisodeMetadataFilename(episodeFile.RelativePath), xmlResult.Trim(Environment.NewLine.ToCharArray()));
        }

        public override List<ImageFileResult> SeriesImages(Series series)
        {
            if (!Settings.SeriesImages)
            {
                return new List<ImageFileResult>();
            }

            return ProcessSeriesImages(series).ToList();
        }

        public override List<ImageFileResult> SeasonImages(Series series, Season season)
        {
            if (!Settings.SeasonImages)
            {
                return new List<ImageFileResult>();
            }

            return ProcessSeasonImages(series, season).ToList();
        }

        public override List<ImageFileResult> EpisodeImages(Series series, EpisodeFile episodeFile)
        {
            if (!Settings.EpisodeImages)
            {
                return new List<ImageFileResult>();
            }

            try
            {
                var firstEpisode = episodeFile.Episodes.Value.FirstOrDefault();

                if (firstEpisode == null)
                {
                    _logger.Debug("Episode file has no associated episodes, potentially a duplicate file");
                    return new List<ImageFileResult>();
                }

                var screenshot = firstEpisode.Images.SingleOrDefault(i => i.CoverType == MediaCoverTypes.Screenshot);

                if (screenshot == null)
                {
                    _logger.Debug("Episode screenshot not available");
                    return new List<ImageFileResult>();
                }

                return new List<ImageFileResult>
                   {
                       new ImageFileResult(GetEpisodeImageFilename(episodeFile.RelativePath), screenshot.RemoteUrl)
                   };
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to process episode image for file: {0}", Path.Combine(series.Path, episodeFile.RelativePath));

                return new List<ImageFileResult>();
            }
        }

        private IEnumerable<ImageFileResult> ProcessSeriesImages(Series series)
        {
            foreach (var image in series.Images)
            {
                var source = _mediaCoverService.GetCoverPath(series.Id, image.CoverType);
                var destination = image.CoverType.ToString().ToLowerInvariant() + Path.GetExtension(source);

                yield return new ImageFileResult(destination, source);
            }
        }

        private IEnumerable<ImageFileResult> ProcessSeasonImages(Series series, Season season)
        {
            foreach (var image in season.Images)
            {
                var filename = string.Format("season{0:00}-{1}.jpg", season.SeasonNumber, image.CoverType.ToString().ToLower());

                if (season.SeasonNumber == 0)
                {
                    filename = string.Format("season-specials-{0}.jpg", image.CoverType.ToString().ToLower());
                }

                yield return new ImageFileResult(filename, image.RemoteUrl);
            }
        }

        // ----------------------------------------------------------------- MOVIE

        public override string GetFilenameAfterMove(Movie movie, MovieFile movieFile, MetadataFile metadataFile)
        {
            var movieFilePath = Path.Combine(movie.Path, movieFile.RelativePath);

            if (metadataFile.Type == MetadataType.MovieMetadata)
            {
                return GetMovieMetadataFilename(movieFilePath);
            }

            _logger.Debug("Unknown movie file metadata: {0}", metadataFile.RelativePath);
            return Path.Combine(movie.Path, metadataFile.RelativePath);
        }

        public override MetadataFile FindMetadataFile(Movie movie, string path)
        {
            var filename = Path.GetFileName(path);

            if (filename == null)
            {
                return null;
            }

            var metadata = new MetadataFile
            {
                MovieId = movie.Id,
                MediaType = NzbDrone.Core.Extras.Files.MediaType.Movie,
                Consumer = GetType().Name,
                RelativePath = movie.Path.GetRelativePath(path)
            };

            if (MovieImagesRegex.IsMatch(filename))
            {
                metadata.Type = MetadataType.MovieImage;
                return metadata;
            }

            if (MovieFileImageRegex.IsMatch(filename))
            {
                metadata.Type = MetadataType.MovieImage;
                return metadata;
            }

            if (filename.Equals("movie.nfo", StringComparison.OrdinalIgnoreCase) &&
                _detectNfo.IsXbmcNfoFile(path))
            {
                metadata.Type = MetadataType.MovieMetadata;
                return metadata;
            }

            var parseResult = Parser.Parser.ParseMovieTitle(filename);

            if (parseResult != null &&
                Path.GetExtension(filename).Equals(".nfo", StringComparison.OrdinalIgnoreCase) &&
                _detectNfo.IsXbmcNfoFile(path))
            {
                metadata.Type = MetadataType.MovieMetadata;
                return metadata;
            }

            return null;
        }

        public override MetadataFileResult MovieMetadata(Movie movie, MovieFile movieFile)
        {
            var xmlResult = string.Empty;

            if (Settings.MovieMetadata)
            {
                _logger.Debug("Generating Movie Metadata for: {0}", Path.Combine(movie.Path, movieFile.RelativePath));

                var movieMetadataLanguage = Settings.MovieMetadataLanguage == (int)Language.Original ?
                    (int)movie.MovieMetadata.Value.OriginalLanguage :
                    Settings.MovieMetadataLanguage;

                var movieTranslations = _movieTranslationsService.GetAllTranslationsForMovieMetadata(movie.MovieMetadataId);
                var selectedSettingsLanguage = Language.FindById(movieMetadataLanguage);
                var movieTranslation = movieTranslations.FirstOrDefault(mt => mt.Language == selectedSettingsLanguage);

                var credits = _creditService.GetAllCreditsForMovieMetadata(movie.MovieMetadataId);

                var watched = GetExistingWatchedStatus(movie, movieFile.RelativePath);

                var thumbnail = movie.MovieMetadata.Value.Images.SingleOrDefault(i => i.CoverType == MediaCoverTypes.Screenshot);
                var posters = movie.MovieMetadata.Value.Images.Where(i => i.CoverType == MediaCoverTypes.Poster).ToList();
                var fanarts = movie.MovieMetadata.Value.Images.Where(i => i.CoverType == MediaCoverTypes.Fanart).ToList();

                var details = new XElement("movie");

                var metadataTitle = movieTranslation?.Title ?? movie.Title;

                details.Add(new XElement("title", metadataTitle));

                details.Add(new XElement("originaltitle", movie.MovieMetadata.Value.OriginalTitle));

                details.Add(new XElement("sorttitle", Parser.Parser.NormalizeTitle(metadataTitle)));

                if (movie.MovieMetadata.Value.Ratings?.Tmdb?.Votes > 0 || movie.MovieMetadata.Value.Ratings?.Imdb?.Votes > 0 || movie.MovieMetadata.Value.Ratings?.RottenTomatoes?.Value > 0)
                {
                    var setRating = new XElement("ratings");

                    var defaultRatingSet = false;

                    if (movie.MovieMetadata.Value.Ratings?.Imdb?.Votes > 0)
                    {
                        var setRateImdb = new XElement("rating", new XAttribute("name", "imdb"), new XAttribute("max", "10"), new XAttribute("default", "true"));
                        setRateImdb.Add(new XElement("value", movie.MovieMetadata.Value.Ratings.Imdb.Value));
                        setRateImdb.Add(new XElement("votes", movie.MovieMetadata.Value.Ratings.Imdb.Votes));

                        defaultRatingSet = true;
                        setRating.Add(setRateImdb);
                    }

                    if (movie.MovieMetadata.Value.Ratings?.Tmdb?.Votes > 0)
                    {
                        var setRateTheMovieDb = new XElement("rating", new XAttribute("name", "themoviedb"), new XAttribute("max", "10"));
                        setRateTheMovieDb.Add(new XElement("value", movie.MovieMetadata.Value.Ratings.Tmdb.Value));
                        setRateTheMovieDb.Add(new XElement("votes", movie.MovieMetadata.Value.Ratings.Tmdb.Votes));

                        if (!defaultRatingSet)
                        {
                            defaultRatingSet = true;
                            setRateTheMovieDb.SetAttributeValue("default", "true");
                        }

                        setRating.Add(setRateTheMovieDb);
                    }

                    if (movie.MovieMetadata.Value.Ratings?.RottenTomatoes?.Value > 0)
                    {
                        var setRateRottenTomatoes = new XElement("rating", new XAttribute("name", "tomatometerallcritics"), new XAttribute("max", "100"));
                        setRateRottenTomatoes.Add(new XElement("value", movie.MovieMetadata.Value.Ratings.RottenTomatoes.Value));

                        if (!defaultRatingSet)
                        {
                            setRateRottenTomatoes.SetAttributeValue("default", "true");
                        }

                        setRating.Add(setRateRottenTomatoes);
                    }

                    details.Add(setRating);
                }

                if (movie.MovieMetadata.Value.Ratings?.Tmdb?.Votes > 0)
                {
                    details.Add(new XElement("rating", movie.MovieMetadata.Value.Ratings.Tmdb.Value));
                }

                if (movie.MovieMetadata.Value.Ratings?.RottenTomatoes?.Value > 0)
                {
                    details.Add(new XElement("criticrating", movie.MovieMetadata.Value.Ratings.RottenTomatoes.Value));
                }

                details.Add(new XElement("userrating"));

                details.Add(new XElement("top250"));

                details.Add(new XElement("outline"));

                details.Add(new XElement("plot", movieTranslation?.Overview ?? movie.MovieMetadata.Value.Overview));

                details.Add(new XElement("tagline"));

                details.Add(new XElement("runtime", movie.MovieMetadata.Value.Runtime));

                if (thumbnail != null)
                {
                    details.Add(new XElement("thumb", thumbnail.RemoteUrl));
                }

                foreach (var poster in posters)
                {
                    if (poster != null && poster.RemoteUrl != null)
                    {
                        details.Add(new XElement("thumb", new XAttribute("aspect", "poster"), new XAttribute("preview", poster.RemoteUrl), poster.RemoteUrl));
                    }
                }

                if (fanarts.Any())
                {
                    var fanartElement = new XElement("fanart");

                    foreach (var fanart in fanarts)
                    {
                        if (fanart != null && fanart.RemoteUrl != null)
                        {
                            fanartElement.Add(new XElement("thumb", new XAttribute("preview", fanart.RemoteUrl), fanart.RemoteUrl));
                        }
                    }

                    details.Add(fanartElement);
                }

                if (movie.MovieMetadata.Value.Certification.IsNotNullOrWhiteSpace())
                {
                    details.Add(new XElement("mpaa", movie.MovieMetadata.Value.Certification));
                }

                details.Add(new XElement("playcount"));

                details.Add(new XElement("lastplayed"));

                details.Add(new XElement("id", movie.TmdbId));

                var uniqueId = new XElement("uniqueid", movie.TmdbId);
                uniqueId.SetAttributeValue("type", "tmdb");
                uniqueId.SetAttributeValue("default", true);
                details.Add(uniqueId);

                if (movie.MovieMetadata.Value.ImdbId.IsNotNullOrWhiteSpace())
                {
                    var imdbId = new XElement("uniqueid", movie.MovieMetadata.Value.ImdbId);
                    imdbId.SetAttributeValue("type", "imdb");
                    details.Add(imdbId);
                }

                foreach (var genre in movie.MovieMetadata.Value.Genres)
                {
                    details.Add(new XElement("genre", genre));
                }

                details.Add(new XElement("country"));

                if (Settings.AddCollectionName && movie.MovieMetadata.Value.CollectionTitle.IsNotNullOrWhiteSpace())
                {
                    var setElement = new XElement("set");

                    setElement.SetAttributeValue("tmdbcolid", movie.MovieMetadata.Value.CollectionTmdbId);
                    setElement.Add(new XElement("name", movie.MovieMetadata.Value.CollectionTitle));
                    setElement.Add(new XElement("overview"));

                    details.Add(setElement);
                }

                if (movie.Tags.Any())
                {
                    var tags = _tagRepo.GetTags(movie.Tags);

                    foreach (var tag in tags)
                    {
                        details.Add(new XElement("tag", tag.Label));
                    }
                }

                details.Add(new XElement("status", movie.MovieMetadata.Value.Status));

                foreach (var credit in credits)
                {
                    if (credit.Name != null && credit.Job == "Screenplay")
                    {
                        details.Add(new XElement("credits", credit.Name));
                    }
                }

                foreach (var credit in credits)
                {
                    if (credit.Name != null && credit.Job == "Director")
                    {
                        details.Add(new XElement("director", credit.Name));
                    }
                }

                if (movie.MovieMetadata.Value.InCinemas.HasValue)
                {
                    details.Add(new XElement("premiered", movie.MovieMetadata.Value.InCinemas.Value.ToString("yyyy-MM-dd")));
                }

                details.Add(new XElement("year", movie.Year));

                details.Add(new XElement("studio", movie.MovieMetadata.Value.Studio));

                details.Add(new XElement("trailer", "plugin://plugin.video.youtube/play/?video_id=" + movie.MovieMetadata.Value.YouTubeTrailerId));

                details.Add(new XElement("watched", watched));

                if (movieFile.MediaInfo != null)
                {
                    var sceneName = movieFile.GetSceneOrFileName();

                    var fileInfo = new XElement("fileinfo");
                    var streamDetails = new XElement("streamdetails");

                    var video = new XElement("video");
                    video.Add(new XElement("aspect", (float)movieFile.MediaInfo.Width / (float)movieFile.MediaInfo.Height));
                    video.Add(new XElement("bitrate", movieFile.MediaInfo.VideoBitrate));
                    video.Add(new XElement("codec", MediaInfoFormatter.FormatVideoCodec(movieFile.MediaInfo, sceneName)));
                    video.Add(new XElement("framerate", movieFile.MediaInfo.VideoFps));
                    video.Add(new XElement("height", movieFile.MediaInfo.Height));
                    video.Add(new XElement("scantype", movieFile.MediaInfo.ScanType));
                    video.Add(new XElement("width", movieFile.MediaInfo.Width));

                    if (movieFile.MediaInfo.RunTime != TimeSpan.Zero)
                    {
                        video.Add(new XElement("duration", movieFile.MediaInfo.RunTime.TotalMinutes));
                        video.Add(new XElement("durationinseconds", Math.Round(movieFile.MediaInfo.RunTime.TotalSeconds)));
                    }

                    if (movieFile.MediaInfo.VideoHdrFormat is HdrFormat.DolbyVision or HdrFormat.DolbyVisionHdr10 or HdrFormat.DolbyVisionHdr10Plus or HdrFormat.DolbyVisionHlg or HdrFormat.DolbyVisionSdr)
                    {
                        video.Add(new XElement("hdrtype", "dolbyvision"));
                    }
                    else if (movieFile.MediaInfo.VideoHdrFormat is HdrFormat.Hdr10 or HdrFormat.Hdr10Plus or HdrFormat.Pq10)
                    {
                        video.Add(new XElement("hdrtype", "hdr10"));
                    }
                    else if (movieFile.MediaInfo.VideoHdrFormat == HdrFormat.Hlg10)
                    {
                        video.Add(new XElement("hdrtype", "hlg"));
                    }
                    else if (movieFile.MediaInfo.VideoHdrFormat == HdrFormat.None)
                    {
                        video.Add(new XElement("hdrtype", ""));
                    }

                    streamDetails.Add(video);

                    var audio = new XElement("audio");
                    var audioChannelCount = movieFile.MediaInfo.AudioChannels;
                    audio.Add(new XElement("bitrate", movieFile.MediaInfo.AudioBitrate));
                    audio.Add(new XElement("channels", audioChannelCount));
                    audio.Add(new XElement("codec", MediaInfoFormatter.FormatAudioCodec(movieFile.MediaInfo, sceneName)));
                    audio.Add(new XElement("language", movieFile.MediaInfo.AudioLanguages));
                    streamDetails.Add(audio);

                    if (movieFile.MediaInfo.Subtitles is { Count: > 0 })
                    {
                        foreach (var s in movieFile.MediaInfo.Subtitles)
                        {
                            var subtitle = new XElement("subtitle");
                            subtitle.Add(new XElement("language", s));
                            streamDetails.Add(subtitle);
                        }
                    }

                    fileInfo.Add(streamDetails);
                    details.Add(fileInfo);

                    foreach (var credit in credits)
                    {
                        if (credit.Name != null && credit.Character != null)
                        {
                            var actorElement = new XElement("actor");

                            actorElement.Add(new XElement("name", credit.Name));
                            actorElement.Add(new XElement("role", credit.Character));
                            actorElement.Add(new XElement("order", credit.Order));

                            var headshot = credit.Images.FirstOrDefault(m => m.CoverType == MediaCoverTypes.Headshot);

                            if (headshot != null && headshot.RemoteUrl != null)
                            {
                                actorElement.Add(new XElement("thumb", headshot.RemoteUrl));
                            }

                            details.Add(actorElement);
                        }
                    }
                }

                var doc = new XDocument(details)
                {
                    Declaration = new XDeclaration("1.0", "UTF-8", "yes"),
                };

                using var sw = new Utf8StringWriter();
                using var xw = XmlWriter.Create(sw, new XmlWriterSettings
                {
                    Encoding = Encoding.UTF8,
                    Indent = true
                });

                doc.Save(xw);
                xw.Flush();

                xmlResult += sw.ToString();
                xmlResult += Environment.NewLine;
            }

            if (Settings.MovieMetadataURL)
            {
                xmlResult += "https://www.themoviedb.org/movie/" + movie.MovieMetadata.Value.TmdbId;
                xmlResult += Environment.NewLine;

                xmlResult += "https://www.imdb.com/title/" + movie.MovieMetadata.Value.ImdbId;
                xmlResult += Environment.NewLine;
            }

            var metadataFileName = GetMovieMetadataFilename(movieFile.RelativePath);

            return string.IsNullOrEmpty(xmlResult) ? null : new MetadataFileResult(metadataFileName, xmlResult.Trim(Environment.NewLine.ToCharArray()));
        }

        public override List<ImageFileResult> MovieImages(Movie movie)
        {
            if (!Settings.MovieImages)
            {
                return new List<ImageFileResult>();
            }

            return ProcessMovieImages(movie).ToList();
        }

        private IEnumerable<ImageFileResult> ProcessMovieImages(Movie movie)
        {
            foreach (var image in movie.MovieMetadata.Value.Images)
            {
                var source = _mediaCoverService.GetCoverPath(movie.Id, image.CoverType);
                var destination = image.CoverType.ToString().ToLowerInvariant() + Path.GetExtension(source);

                yield return new ImageFileResult(destination, source);
            }
        }

        private string GetEpisodeMetadataFilename(string episodeFilePath)
        {
            return Path.ChangeExtension(episodeFilePath, "nfo");
        }

        private string GetEpisodeImageFilename(string episodeFilePath)
        {
            return Path.ChangeExtension(episodeFilePath, "").Trim('.') + "-thumb.jpg";
        }

        private string GetMovieMetadataFilename(string movieFilePath)
        {
            if (Settings.UseMovieNfo)
            {
                return Path.Combine(Path.GetDirectoryName(movieFilePath), "movie.nfo");
            }
            else
            {
                return Path.ChangeExtension(movieFilePath, "nfo");
            }
        }

        private bool GetExistingWatchedStatus(Series series, string episodeFilePath)
        {
            var fullPath = Path.Combine(series.Path, GetEpisodeMetadataFilename(episodeFilePath));

            if (!_diskProvider.FileExists(fullPath))
            {
                return false;
            }

            var fileContent = _diskProvider.ReadAllText(fullPath);

            return Regex.IsMatch(fileContent, "<watched>true</watched>");
        }

        private bool GetExistingWatchedStatus(Movie movie, string movieFilePath)
        {
            var fullPath = Path.Combine(movie.Path, GetMovieMetadataFilename(movieFilePath));

            if (!_diskProvider.FileExists(fullPath))
            {
                return false;
            }

            var fileContent = _diskProvider.ReadAllText(fullPath);

            return Regex.IsMatch(fileContent, "<watched>true</watched>");
        }
    }
}
