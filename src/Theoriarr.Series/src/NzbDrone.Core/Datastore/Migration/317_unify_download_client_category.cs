using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using FluentMigrator;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // Before the shared-category change every client constructor wrote a movie category default
    // ("radarr", "Movies"/"movies"). Those values persisted in stored settings, and because
    // EffectiveMovieCategory prefers the movie category when set, movie downloads kept being
    // labelled with the old name even after the visible series category was changed. The movie
    // category field is hidden in the unified UI, so clear the legacy defaults and let movies
    // fall back to the single shared series category.
    [Migration(317)]
    public class unify_download_client_category : NzbDroneMigrationBase
    {
        private const string MovieCategoryKey = "movieCategory";

        private static readonly HashSet<string> LegacyMovieCategories = new(StringComparer.OrdinalIgnoreCase)
        {
            "radarr",
            "Movies"
        };

        protected override void MainDbUpgrade()
        {
            Execute.WithConnection(RemoveLegacyMovieCategories);
        }

        private void RemoveLegacyMovieCategories(IDbConnection conn, IDbTransaction tran)
        {
            var updatedClients = new List<object>();

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tran;
                cmd.CommandText = "SELECT \"Id\", \"Settings\" FROM \"DownloadClients\"";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var id = reader.GetInt32(0);
                        var settingsJson = reader.GetString(1);
                        var settings = Json.Deserialize<Dictionary<string, object>>(settingsJson);
                        var changed = false;

                        foreach (var key in settings.Keys.ToList())
                        {
                            if (!key.Equals(MovieCategoryKey, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (settings[key] is string value && LegacyMovieCategories.Contains(value))
                            {
                                settings[key] = string.Empty;
                                changed = true;
                            }
                        }

                        if (changed)
                        {
                            updatedClients.Add(new
                            {
                                Settings = settings.ToJson(),
                                Id = id
                            });
                        }
                    }
                }
            }

            if (updatedClients.Any())
            {
                var updateClientsSql = "UPDATE \"DownloadClients\" SET \"Settings\" = @Settings WHERE \"Id\" = @Id";
                conn.Execute(updateClientsSql, updatedClients, transaction: tran);
            }
        }
    }
}
