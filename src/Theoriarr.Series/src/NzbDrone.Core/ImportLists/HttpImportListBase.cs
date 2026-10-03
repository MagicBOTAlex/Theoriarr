using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Http.CloudFlare;
using NzbDrone.Core.ImportLists.Exceptions;
using NzbDrone.Core.ImportLists.ImportListMovies;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.ImportLists
{
    public abstract class HttpImportListBase<TSettings> : ImportListBase<TSettings>
        where TSettings : IImportListSettings, new()
    {
        protected const int MaxNumResultsPerQuery = 1000;

        protected readonly IHttpClient _httpClient;

        public virtual int PageSize => 0;
        public virtual TimeSpan RateLimit => TimeSpan.FromSeconds(2);

        // MOVIES feature folded in (pre-generated page feeds ignore the short-page break).
        protected virtual bool UsePreGeneratedPages => false;

        public abstract IImportListRequestGenerator GetRequestGenerator();

        // SERIES parser. MediaType == Series providers override this.
        public virtual IParseImportListResponse GetParser()
        {
            return null;
        }

        // MOVIES parser. MediaType == Movie providers override this.
        public virtual IParseImportListMovieResponse GetMovieParser()
        {
            return null;
        }

        // SERIES/localized ctor (canonical).
        public HttpImportListBase(IHttpClient httpClient, IImportListStatusService importListStatusService, IConfigService configService, IParsingService parsingService, ILocalizationService localizationService, Logger logger)
            : base(importListStatusService, configService, parsingService, localizationService, logger)
        {
            _httpClient = httpClient;
        }

        // MOVIES-compatible ctor (no localization).
        public HttpImportListBase(IHttpClient httpClient, IImportListStatusService importListStatusService, IConfigService configService, IParsingService parsingService, Logger logger)
            : base(importListStatusService, configService, parsingService, logger)
        {
            _httpClient = httpClient;
        }

        public override ImportListFetchResult Fetch()
        {
            return MediaType == NzbDrone.Core.ImportLists.MediaType.Movie
                ? FetchMovies(g => g.GetListItems())
                : FetchItems(g => g.GetListItems());
        }

        protected virtual ImportListFetchResult FetchItems(Func<IImportListRequestGenerator, ImportListPageableRequestChain> pageableRequestChainSelector, bool isRecent = false)
        {
            var releases = new List<ImportListItemInfo>();
            var url = string.Empty;
            var anyFailure = true;

            try
            {
                var generator = GetRequestGenerator();
                var parser = GetParser();

                var pageableRequestChain = pageableRequestChainSelector(generator);

                for (var i = 0; i < pageableRequestChain.Tiers; i++)
                {
                    var pageableRequests = pageableRequestChain.GetTier(i);

                    foreach (var pageableRequest in pageableRequests)
                    {
                        var pagedReleases = new List<ImportListItemInfo>();

                        foreach (var request in pageableRequest)
                        {
                            url = request.Url.FullUri;

                            var page = FetchPage(request, parser);

                            pagedReleases.AddRange(page);

                            if (pagedReleases.Count >= MaxNumResultsPerQuery)
                            {
                                break;
                            }

                            if (!IsFullPage(page))
                            {
                                break;
                            }
                        }

                        releases.AddRange(pagedReleases.Where(IsValidItem));
                    }

                    if (releases.Any())
                    {
                        break;
                    }
                }

                _importListStatusService.RecordSuccess(Definition.Id);
                anyFailure = false;
            }
            catch (WebException webException)
            {
                HandleWebException(webException, url);
            }
            catch (TooManyRequestsException ex)
            {
                if (ex.RetryAfter != TimeSpan.Zero)
                {
                    _importListStatusService.RecordFailure(Definition.Id, ex.RetryAfter);
                }
                else
                {
                    _importListStatusService.RecordFailure(Definition.Id, TimeSpan.FromHours(1));
                }

                _logger.Warn("API Request Limit reached for {0}", this);
            }
            catch (HttpException ex)
            {
                _importListStatusService.RecordFailure(Definition.Id);
                _logger.Warn("{0} {1}", this, ex.Message);
            }
            catch (RequestLimitReachedException)
            {
                _importListStatusService.RecordFailure(Definition.Id, TimeSpan.FromHours(1));
                _logger.Warn("API Request Limit reached for {0}", this);
            }
            catch (CloudFlareCaptchaException ex)
            {
                HandleCloudFlareCaptcha(ex, url);
            }
            catch (ImportListException ex)
            {
                _importListStatusService.RecordFailure(Definition.Id);
                _logger.Warn(ex, "{0}", url);
            }
            catch (Exception ex)
            {
                _importListStatusService.RecordFailure(Definition.Id);
                ex.WithData("FeedUrl", url);
                _logger.Error(ex, "An error occurred while processing feed. {0}", url);
            }

            return new ImportListFetchResult(CleanupListItems(releases), anyFailure);
        }

        protected virtual ImportListFetchResult FetchMovies(Func<IImportListRequestGenerator, ImportListPageableRequestChain> pageableRequestChainSelector, bool isRecent = false)
        {
            var movies = new List<ImportListMovie>();
            var url = string.Empty;
            var anyFailure = true;

            try
            {
                var generator = GetRequestGenerator();
                var parser = GetMovieParser();

                var pageableRequestChain = pageableRequestChainSelector(generator);

                for (var i = 0; i < pageableRequestChain.Tiers; i++)
                {
                    var pageableRequests = pageableRequestChain.GetTier(i);

                    foreach (var pageableRequest in pageableRequests)
                    {
                        var pagedMovies = new List<ImportListMovie>();

                        foreach (var request in pageableRequest)
                        {
                            url = request.Url.FullUri;

                            var page = FetchPage(request, parser);

                            pagedMovies.AddRange(page);

                            if (pagedMovies.Count >= MaxNumResultsPerQuery)
                            {
                                break;
                            }

                            if (!UsePreGeneratedPages && !IsFullPage(page))
                            {
                                break;
                            }
                        }

                        movies.AddRange(pagedMovies.Where(IsValidItem));
                    }

                    if (movies.Any())
                    {
                        break;
                    }
                }

                _importListStatusService.RecordSuccess(Definition.Id);
                anyFailure = false;
            }
            catch (WebException webException)
            {
                HandleWebException(webException, url);
            }
            catch (TooManyRequestsException ex)
            {
                var retryTime = ex.RetryAfter != TimeSpan.Zero ? ex.RetryAfter : TimeSpan.FromHours(1);
                _importListStatusService.RecordFailure(Definition.Id, retryTime);

                _logger.Warn("API Request Limit reached for {0}", this);
            }
            catch (HttpException ex)
            {
                _importListStatusService.RecordFailure(Definition.Id);
                _logger.Warn("{0} {1}", this, ex.Message);
            }
            catch (RequestLimitReachedException)
            {
                _importListStatusService.RecordFailure(Definition.Id, TimeSpan.FromHours(1));
                _logger.Warn("API Request Limit reached for {0}", this);
            }
            catch (CloudFlareCaptchaException ex)
            {
                HandleCloudFlareCaptcha(ex, url);
            }
            catch (ImportListException ex)
            {
                _importListStatusService.RecordFailure(Definition.Id);
                _logger.Warn(ex, "{0}", url);
            }
            catch (Exception ex)
            {
                _importListStatusService.RecordFailure(Definition.Id);
                ex.WithData("FeedUrl", url);
                _logger.Error(ex, "An error occurred while processing feed. {0}", url);
            }

            return new ImportListFetchResult(CleanupListItems(movies), anyFailure);
        }

        private void HandleWebException(WebException webException, string url)
        {
            if (webException.Status is WebExceptionStatus.NameResolutionFailure or WebExceptionStatus.ConnectFailure)
            {
                _importListStatusService.RecordConnectionFailure(Definition.Id);
            }
            else
            {
                _importListStatusService.RecordFailure(Definition.Id);
            }

            if (webException.Message.Contains("502") || webException.Message.Contains("503") ||
                webException.Message.Contains("timed out"))
            {
                _logger.Warn("{0} server is currently unavailable. {1} {2}", this, url, webException.Message);
            }
            else
            {
                _logger.Warn("{0} {1} {2}", this, url, webException.Message);
            }
        }

        private void HandleCloudFlareCaptcha(CloudFlareCaptchaException ex, string url)
        {
            _importListStatusService.RecordFailure(Definition.Id);
            ex.WithData("FeedUrl", url);

            if (ex.IsExpired)
            {
                _logger.Error(ex, "Expired CAPTCHA token for {0}, please refresh in import list settings.", this);
            }
            else
            {
                _logger.Error(ex, "CAPTCHA token required for {0}, check import list settings.", this);
            }
        }

        protected virtual bool IsValidItem(ImportListItemInfo listItem)
        {
            if (listItem.Title.IsNullOrWhiteSpace() && listItem.ImdbId.IsNullOrWhiteSpace() && listItem.TmdbId == 0)
            {
                return false;
            }

            return true;
        }

        protected virtual bool IsValidItem(ImportListMovie listItem)
        {
            if (listItem.Title.IsNullOrWhiteSpace() && listItem.ImdbId.IsNullOrWhiteSpace() && listItem.TmdbId == 0)
            {
                return false;
            }

            return true;
        }

        protected virtual bool IsFullPage(IList<ImportListItemInfo> page)
        {
            return PageSize != 0 && page.Count >= PageSize;
        }

        protected virtual bool IsFullPage(IList<ImportListMovie> page)
        {
            return PageSize != 0 && page.Count >= PageSize;
        }

        protected virtual IList<ImportListItemInfo> FetchPage(ImportListRequest request, IParseImportListResponse parser)
        {
            var response = FetchImportListResponse(request);

            return parser.ParseResponse(response).ToList();
        }

        protected virtual IList<ImportListMovie> FetchPage(ImportListRequest request, IParseImportListMovieResponse parser)
        {
            var response = FetchImportListResponse(request);

            return parser.ParseResponse(response).ToList();
        }

        protected virtual ImportListResponse FetchImportListResponse(ImportListRequest request)
        {
            _logger.Debug("Downloading Feed " + request.HttpRequest.ToString(false));

            if (request.HttpRequest.RateLimit < RateLimit)
            {
                request.HttpRequest.RateLimit = RateLimit;
            }

            request.HttpRequest.AllowAutoRedirect = true;

            return new ImportListResponse(request, _httpClient.Execute(request.HttpRequest));
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            failures.AddIfNotNull(TestConnection());
        }

        protected virtual ValidationFailure TestConnection()
        {
            try
            {
                var generator = GetRequestGenerator();
                var pageableRequests = generator.GetListItems();

                var allTiers = pageableRequests.GetAllTiers();
                if (!allTiers.Any())
                {
                    return new NzbDroneValidationFailure(string.Empty,
                               "No pages were returned from your import list, please check your settings and the log for details.")
                    { IsWarning = true };
                }

                var firstTier = allTiers.First();
                if (!firstTier.Any())
                {
                    return new NzbDroneValidationFailure(string.Empty,
                               "No data could be retrieved from your import list, please check your settings.")
                    { IsWarning = true };
                }

                var firstRequest = firstTier.First();

                IList<ImportListItemInfo> releases;

                if (MediaType == NzbDrone.Core.ImportLists.MediaType.Movie)
                {
                    var movieParser = GetMovieParser();
                    var movieReleases = FetchPage(firstRequest, movieParser);

                    if (movieReleases.Empty())
                    {
                        return new NzbDroneValidationFailure(string.Empty,
                                   "No results were returned from your import list, please check your settings and the log for details.")
                        { IsWarning = true };
                    }

                    return null;
                }

                var parser = GetParser();
                releases = FetchPage(firstRequest, parser);

                if (releases.Empty())
                {
                    return new NzbDroneValidationFailure(string.Empty,
                               "No results were returned from your import list, please check your settings and the log for details.")
                    { IsWarning = true };
                }
            }
            catch (RequestLimitReachedException)
            {
                _logger.Warn("Request limit reached");
            }
            catch (UnsupportedFeedException ex)
            {
                _logger.Warn(ex, "Import list feed is not supported");

                return new ValidationFailure(string.Empty, "Import list feed is not supported: " + ex.Message);
            }
            catch (ImportListException ex)
            {
                _logger.Warn(ex, "Unable to connect to import list");

                return new ValidationFailure(string.Empty, $"Unable to connect to import list: {ex.Message}. Check the log surrounding this error for details.");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to connect to import list");

                return new ValidationFailure(string.Empty, $"Unable to connect to import list: {ex.Message}. Check the log surrounding this error for details.");
            }

            return null;
        }
    }
}
