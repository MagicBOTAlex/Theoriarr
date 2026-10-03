using System.Linq;
using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Configuration
{
    public interface IConfigRepository : IBasicRepository<Config>
    {
        Config Get(string key);
        Config Upsert(string key, string value);
    }

    public class ConfigRepository : BasicRepository<Config>, IConfigRepository
    {
        public ConfigRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public Config Get(string key)
        {
            return Query(c => c.Key == key).SingleOrDefault();
        }

        public Config Upsert(string key, string value)
        {
            key = key.ToLowerInvariant();

            using (var conn = _database.OpenConnection())
            {
                // Single atomic statement so concurrent writes can't insert duplicate keys
                // (Config.Key is unique) or race between the lookup and the write.
                conn.Execute(
                    $"INSERT INTO \"{_table}\" (\"Key\", \"Value\") VALUES (@Key, @Value) " +
                    "ON CONFLICT (\"Key\") DO UPDATE SET \"Value\" = EXCLUDED.\"Value\"",
                    new { Key = key, Value = value });
            }

            return Get(key);
        }
    }
}
