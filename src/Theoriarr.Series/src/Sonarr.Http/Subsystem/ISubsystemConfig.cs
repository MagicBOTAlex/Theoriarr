using NzbDrone.Core.Configuration;

namespace Sonarr.Http.Subsystem
{
    public interface ISubsystemConfig
    {
        string SeriesApiKey { get; }
        string MovieApiKey { get; }
    }

    public class SubsystemConfig : ISubsystemConfig
    {
        // Both keys live on the unified NzbDrone.Core.Configuration.ConfigFileProvider
        // (IConfigFileProvider.ApiKey / IConfigFileProvider.MovieApiKey), persisted in the
        // shared config.xml. A missing/null movie key leaves only the series key authenticating.
        private readonly IConfigFileProvider _configFileProvider;

        public SubsystemConfig(IConfigFileProvider configFileProvider)
        {
            _configFileProvider = configFileProvider;
        }

        public string SeriesApiKey => _configFileProvider.ApiKey;

        public string MovieApiKey => _configFileProvider.MovieApiKey;
    }
}
