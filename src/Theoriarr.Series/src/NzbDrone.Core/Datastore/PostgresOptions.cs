using System.Linq;
using Microsoft.Extensions.Configuration;

namespace NzbDrone.Core.Datastore
{
    public class PostgresOptions
    {
        private static readonly string[] ConfigSectionPrefixes = { "Theoriarr", "Sonarr", "Radarr" };

        public string Host { get; set; }
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string MainDb { get; set; }
        public string LogDb { get; set; }
        public string MainDbConnectionString { get; set; }
        public string LogDbConnectionString { get; set; }

        public static PostgresOptions GetOptions()
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var postgresOptions = new PostgresOptions();

            // Legacy sections first, unified section last so "Theoriarr:Postgres" wins when present.
            foreach (var prefix in ConfigSectionPrefixes.Reverse())
            {
                config.GetSection($"{prefix}:Postgres").Bind(postgresOptions);
            }

            return postgresOptions;
        }
    }
}
