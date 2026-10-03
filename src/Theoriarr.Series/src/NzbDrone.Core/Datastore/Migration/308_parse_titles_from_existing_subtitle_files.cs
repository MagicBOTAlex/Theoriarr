using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Dapper;
using FluentMigrator;
using NLog;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Core.Datastore.Migration.Framework;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(308)]
    public class parse_movie_titles_from_existing_subtitle_files : NzbDroneMigrationBase
    {
        private static readonly Logger Logger = NzbDroneLogger.GetLogger(typeof(parse_movie_titles_from_existing_subtitle_files));

        protected override void MainDbUpgrade()
        {
            // SERIES 198 already added SubtitleFiles.Title/Copy; only add if missing.
            if (!Schema.Table("SubtitleFiles").Column("Title").Exists())
            {
                Alter.Table("SubtitleFiles").AddColumn("Title").AsString().Nullable();
            }

            if (!Schema.Table("SubtitleFiles").Column("Copy").Exists())
            {
                Alter.Table("SubtitleFiles").AddColumn("Copy").AsInt32().WithDefaultValue(0);
            }

            Execute.WithConnection(UpdateTitles);
        }

        private void UpdateTitles(IDbConnection conn, IDbTransaction tran)
        {
            var updates = new List<object>();

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tran;
                cmd.CommandText = "SELECT \"SubtitleFiles\".\"Id\", \"SubtitleFiles\".\"RelativePath\", \"MovieFiles\".\"RelativePath\", \"MovieFiles\".\"OriginalFilePath\" FROM \"SubtitleFiles\" JOIN \"MovieFiles\" ON \"SubtitleFiles\".\"MovieFileId\" = \"MovieFiles\".\"Id\"";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var id = reader.GetInt32(0);
                    var relativePath = reader.GetString(1);
                    var movieFileRelativePath = reader.GetString(2);
                    var movieFileOriginalFilePath = reader[3] as string;

                    var subtitleTitleInfo = CleanSubtitleTitleInfo(movieFileRelativePath, movieFileOriginalFilePath, relativePath);

                    updates.Add(new
                    {
                        Id = id,
                        Title = subtitleTitleInfo.Title,
                        Language = subtitleTitleInfo.Language,
                        LanguageTags = subtitleTitleInfo.LanguageTags,
                        Copy = subtitleTitleInfo.Copy
                    });
                }
            }

            var updateSubtitleFilesSql = "UPDATE \"SubtitleFiles\" SET \"Title\" = @Title, \"Copy\" = @Copy, \"Language\" = @Language, \"LanguageTags\" = @LanguageTags, \"LastUpdated\" = CURRENT_TIMESTAMP WHERE \"Id\" = @Id";
            conn.Execute(updateSubtitleFilesSql, updates, transaction: tran);
        }

        private static SubtitleTitleInfo CleanSubtitleTitleInfo(string relativePath, string originalFilePath, string path)
        {
            var subtitleTitleInfo = LanguageParser.ParseSubtitleLanguageInformation(path);

            var movieFileTitle = Path.GetFileNameWithoutExtension(relativePath);
            var originalMovieFileTitle = Path.GetFileNameWithoutExtension(originalFilePath) ?? string.Empty;

            if (subtitleTitleInfo.TitleFirst && (movieFileTitle.Contains(subtitleTitleInfo.RawTitle, StringComparison.OrdinalIgnoreCase) || originalMovieFileTitle.Contains(subtitleTitleInfo.RawTitle, StringComparison.OrdinalIgnoreCase)))
            {
                Logger.Debug("Subtitle title '{0}' is in movie file title '{1}'. Removing from subtitle title.", subtitleTitleInfo.RawTitle, movieFileTitle);

                subtitleTitleInfo = LanguageParser.ParseBasicSubtitle(path);
            }

            var cleanedTags = subtitleTitleInfo.LanguageTags.Where(t => !movieFileTitle.Contains(t, StringComparison.OrdinalIgnoreCase)).ToList();

            if (cleanedTags.Count != subtitleTitleInfo.LanguageTags.Count)
            {
                Logger.Debug("Removed language tags '{0}' from subtitle title '{1}'.", string.Join(", ", subtitleTitleInfo.LanguageTags.Except(cleanedTags)), subtitleTitleInfo.RawTitle);
                subtitleTitleInfo.LanguageTags = cleanedTags;
            }

            return subtitleTitleInfo;
        }
    }
}
