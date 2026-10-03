using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using FluentMigrator;
using Newtonsoft.Json.Linq;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // The unified Discord settings enum appends the MOVIES-only `Tags` field at the end
    // (Grab/Import/Manual). In MOVIES, however, `Tags` sat *before* CustomFormats and
    // CustomFormatScore for the Import field list, so a persisted Radarr row that selected
    // Tags/CustomFormats/CustomFormatScore (13/14/15) is reinterpreted as
    // CustomFormats/CustomFormatScore/Tags (13/14/15) by the unified enum. Remap the
    // trailing three Import indices for MOVIES-origin rows only; Grab and Manual indices
    // are identical on both sides and are left untouched.
    [Migration(313)]
    public class discord_movie_field_indices : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Execute.WithConnection(RemapDiscordMovieFields);
        }

        private void RemapDiscordMovieFields(IDbConnection conn, IDbTransaction tran)
        {
            // A SERIES database has no movie rows at upgrade time; a MOVIES database has
            // them. A row carrying the old MOVIES-only CustomFormatScore index (15) is
            // also unambiguously movie-origin, covering a movie database with no movies.
            var hasMovies = conn.ExecuteScalar<int>("SELECT COUNT(1) FROM \"Movies\"") > 0;

            var updated = new List<object>();

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tran;
                cmd.CommandText = "SELECT \"Id\", \"Settings\" FROM \"Notifications\" WHERE \"Implementation\" = 'Discord'";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var id = reader.GetInt32(0);
                        var settings = Json.Deserialize<JObject>(reader.GetString(1));

                        if (settings?["importFields"] is not JArray importFields || !importFields.Any())
                        {
                            continue;
                        }

                        var values = importFields.Select(v => v.Value<int>()).ToList();

                        if (!hasMovies && !values.Contains(15))
                        {
                            continue;
                        }

                        var remapped = values.Select(v => v switch
                        {
                            13 => 15,
                            14 => 13,
                            15 => 14,
                            _ => v
                        });

                        settings["importFields"] = new JArray(remapped);

                        updated.Add(new
                        {
                            Settings = settings.ToJson(),
                            Id = id
                        });
                    }
                }
            }

            if (updated.Any())
            {
                var updateSql = "UPDATE \"Notifications\" SET \"Settings\" = @Settings WHERE \"Id\" = @Id";
                conn.Execute(updateSql, updated, transaction: tran);
            }
        }
    }
}
