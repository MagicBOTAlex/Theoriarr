using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation.Results;
using NLog;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.ImportLists
{
    public interface IImportListFactory : IProviderFactory<IImportList, ImportListDefinition>
    {
        List<IImportList> Enabled(bool filterBlockedImportLists = true);

        // SERIES source-compat alias; EnableAutomaticAdd is folded onto Enabled.
        List<IImportList> AutomaticAddEnabled(bool filterBlockedImportLists = true);

        List<IImportList> Discoverable();
    }

    public class ImportListFactory : ProviderFactory<IImportList, ImportListDefinition>, IImportListFactory
    {
        private readonly IImportListStatusService _importListStatusService;
        private readonly Logger _logger;

        public ImportListFactory(IImportListStatusService importListStatusService,
                              IImportListRepository providerRepository,
                              IEnumerable<IImportList> providers,
                              IServiceProvider container,
                              IEventAggregator eventAggregator,
                              Logger logger)
            : base(providerRepository, FilterProviders(providers), container, eventAggregator, logger)
        {
            _importListStatusService = importListStatusService;
            _logger = logger;
        }

        private static IEnumerable<IImportList> FilterProviders(IEnumerable<IImportList> providers)
        {
            // Providers that depend on Servarr-operated auth (auth.servarr.com /
            // services.sonarr.tv) are unavailable unless a self-hosted broker is enabled.
            return ServarrAuthDependencies.Enabled
                ? providers
                : providers.Where(provider => provider is not IServarrAuthDependent);
        }

        protected override List<ImportListDefinition> Active()
        {
            return base.Active().Where(c => c.Enable).ToList();
        }

        public override void SetProviderCharacteristics(IImportList provider, ImportListDefinition definition)
        {
            base.SetProviderCharacteristics(provider, definition);

            definition.ListType = provider.ListType;
            definition.MinRefreshInterval = provider.MinRefreshInterval;
            definition.MediaType = provider.MediaType;
        }

        public List<IImportList> Enabled(bool filterBlockedImportLists = true)
        {
            var enabledImportLists = GetAvailableProviders().Where(n => ((ImportListDefinition)n.Definition).Enabled);

            if (filterBlockedImportLists)
            {
                return FilterBlockedImportLists(enabledImportLists).ToList();
            }

            return enabledImportLists.ToList();
        }

        public List<IImportList> AutomaticAddEnabled(bool filterBlockedImportLists = true)
        {
            return Enabled(filterBlockedImportLists);
        }

        public List<IImportList> Discoverable()
        {
            var discoverableImportLists = GetAvailableProviders().Where(n => n is IDiscoverableImportList);

            return discoverableImportLists.ToList();
        }

        private IEnumerable<IImportList> FilterBlockedImportLists(IEnumerable<IImportList> importLists)
        {
            var blockedImportLists = _importListStatusService.GetBlockedProviders().ToDictionary(v => v.ProviderId, v => v);

            foreach (var importList in importLists)
            {
                if (blockedImportLists.TryGetValue(importList.Definition.Id, out var blockedImportListStatus))
                {
                    _logger.Debug("Temporarily ignoring import list {0} till {1} due to recent failures.", importList.Definition.Name, blockedImportListStatus.DisabledTill.Value.ToLocalTime());
                    continue;
                }

                yield return importList;
            }
        }

        public override ValidationResult Test(ImportListDefinition definition)
        {
            var result = base.Test(definition);

            if (definition.Id == 0)
            {
                return result;
            }

            if (result == null || result.IsValid)
            {
                _importListStatusService.RecordSuccess(definition.Id);
            }
            else
            {
                _importListStatusService.RecordFailure(definition.Id);
            }

            return result;
        }
    }
}
