using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation.Results;
using NLog;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.ImportLists.ImportListMovies;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.ImportLists
{
    // D5: the fetch result now carries BOTH item pipelines. A provider populates only the
    // list matching its MediaType; the fetch/sync services read both.
    public class ImportListFetchResult
    {
        public ImportListFetchResult()
        {
            Series = new List<ImportListItemInfo>();
            Movies = new List<ImportListMovie>();
        }

        // SERIES pipeline.
        public ImportListFetchResult(IEnumerable<ImportListItemInfo> series, bool anyFailure)
        {
            Series = series.ToList();
            Movies = new List<ImportListMovie>();
            AnyFailure = anyFailure;
        }

        // MOVIES pipeline.
        public ImportListFetchResult(IEnumerable<ImportListMovie> movies, bool anyFailure)
        {
            Series = new List<ImportListItemInfo>();
            Movies = movies.ToList();
            AnyFailure = anyFailure;
        }

        public List<ImportListItemInfo> Series { get; set; }
        public List<ImportListMovie> Movies { get; set; }
        public bool AnyFailure { get; set; }
        public int SyncedLists { get; set; }
    }

    public abstract class ImportListBase<TSettings> : IImportList
        where TSettings : IImportListSettings, new()
    {
        protected readonly IImportListStatusService _importListStatusService;
        protected readonly IConfigService _configService;
        protected readonly IParsingService _parsingService;
        protected readonly ILocalizationService _localizationService;
        protected readonly Logger _logger;

        public abstract string Name { get; }

        public abstract ImportListType ListType { get; }

        public abstract TimeSpan MinRefreshInterval { get; }

        // D5: media discriminator. Series providers use the default; movie providers override.
        public virtual MediaType MediaType => NzbDrone.Core.ImportLists.MediaType.Series;

        // Provider-level flags. Defaults keep existing series providers source-compatible;
        // movie providers override per their implementation.
        public virtual bool Enabled => true;
        public virtual bool EnableAuto => false;

        // SERIES/localized ctor (canonical).
        public ImportListBase(IImportListStatusService importListStatusService, IConfigService configService, IParsingService parsingService, ILocalizationService localizationService, Logger logger)
        {
            _importListStatusService = importListStatusService;
            _configService = configService;
            _parsingService = parsingService;
            _localizationService = localizationService;
            _logger = logger;
        }

        // MOVIES-compatible ctor (no localization). _localizationService is optional so that
        // ported movie providers need no ctor churn; Test() falls back to a hardcoded message.
        public ImportListBase(IImportListStatusService importListStatusService, IConfigService configService, IParsingService parsingService, Logger logger)
            : this(importListStatusService, configService, parsingService, null, logger)
        {
        }

        public Type ConfigContract => typeof(TSettings);

        public virtual ProviderMessage Message => null;

        public virtual IEnumerable<ProviderDefinition> DefaultDefinitions
        {
            get
            {
                var config = (IProviderConfig)new TSettings();

                yield return new ImportListDefinition
                {
                    Enabled = config.Validate().IsValid,
                    EnableAuto = true,
                    Implementation = GetType().Name,
                    Settings = config,
                    MediaType = MediaType
                };
            }
        }

        public virtual ProviderDefinition Definition { get; set; }

        public virtual object RequestAction(string action, IDictionary<string, string> query)
        {
            return null;
        }

        protected TSettings Settings => (TSettings)Definition.Settings;

        public abstract ImportListFetchResult Fetch();

        // SERIES de-dupe key (Title/TvdbId/ImdbId).
        protected virtual IList<ImportListItemInfo> CleanupListItems(IEnumerable<ImportListItemInfo> releases)
        {
            var result = releases.DistinctBy(r => new { r.Title, r.TvdbId, r.ImdbId }).ToList();

            result.ForEach(c =>
            {
                c.ImportListId = Definition.Id;
                c.ImportList = Definition.Name;
            });

            return result;
        }

        // MOVIES de-dupe key (TmdbId).
        protected virtual List<ImportListMovie> CleanupListItems(IEnumerable<ImportListMovie> listMovies)
        {
            var result = listMovies.DistinctBy(m => m.TmdbId).ToList();

            result.ForEach(c =>
            {
                c.ListId = Definition.Id;
            });

            return result;
        }

        public ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();

            try
            {
                Test(failures);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Test aborted due to exception");

                var message = _localizationService != null
                    ? _localizationService.GetLocalizedString("ImportListsValidationTestFailed", new Dictionary<string, object> { { "exceptionMessage", ex.Message } })
                    : "Test was aborted due to an error: " + ex.Message;

                failures.Add(new ValidationFailure(string.Empty, message));
            }

            return new ValidationResult(failures);
        }

        protected abstract void Test(List<ValidationFailure> failures);

        public override string ToString()
        {
            return Definition.Name;
        }
    }
}
