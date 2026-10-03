using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using Sonarr.Http.Subsystem;

#pragma warning disable CS0618
namespace NzbDrone.Api.Test.v3.Subsystem
{
    [TestFixture]
    public class SeriesSubsystemTaggingFixture
    {
        private static readonly Type[] SeriesOnlyControllers =
        {
            typeof(Sonarr.Api.V3.Series.SeriesController),
            typeof(Sonarr.Api.V3.Series.SeriesLookupController),
            typeof(Sonarr.Api.V3.Series.SeriesEditorController),
            typeof(Sonarr.Api.V3.Series.SeriesImportController),
            typeof(Sonarr.Api.V3.Series.SeriesFolderController),
            typeof(Sonarr.Api.V3.Episodes.EpisodeController),
            typeof(Sonarr.Api.V3.EpisodeFiles.EpisodeFileController),
            typeof(Sonarr.Api.V3.Profiles.Languages.LanguageProfileController),
            typeof(Sonarr.Api.V3.Profiles.Languages.LanguageProfileSchemaController),
            typeof(Sonarr.Api.V3.SeasonPass.SeasonPassController),
            typeof(Sonarr.Api.V3.Parse.ParseController),
            typeof(Sonarr.Api.V3.Queue.QueueDetailsController),
            typeof(Sonarr.Api.V3.Queue.QueueStatusController),
            typeof(Sonarr.Api.V3.Queue.QueueActionController),
            typeof(Sonarr.Api.V3.Indexers.ReleasePushController),
            typeof(Sonarr.Api.V3.Indexers.IndexerFlagController),
            typeof(Sonarr.Api.V3.Profiles.Release.ReleaseProfileController),
            typeof(Sonarr.Api.V3.Profiles.Delay.DelayProfileController),
            typeof(Sonarr.Api.V3.System.Backup.BackupController),
            typeof(Sonarr.Api.V3.Config.DownloadClientConfigController),
            typeof(Sonarr.Api.V3.ImportLists.ImportListController),
            typeof(Sonarr.Api.V3.AutoTagging.AutoTaggingController),
            typeof(Sonarr.Api.V3.CustomFilters.CustomFilterController)
        };

        // D6 — controllers that now expose both domains' settings and must accept either key.
        private static readonly Type[] DualTaggedControllers =
        {
            typeof(Sonarr.Api.V3.ImportLists.ImportListExclusionController),
            typeof(Sonarr.Api.V3.Config.NamingConfigController),
            typeof(Sonarr.Api.V3.Config.MediaManagementConfigController),
            typeof(Sonarr.Api.V3.Config.UiConfigController),
            typeof(Sonarr.Api.V3.Config.IndexerConfigController),
            typeof(Sonarr.Api.V3.Config.HostConfigController),
            typeof(Sonarr.Api.V3.Config.ImportListConfigController),
            typeof(Sonarr.Api.V3.Profiles.Languages.LanguageController)
        };

        private static AppSubsystem[] SubsystemsOf(Type controller)
        {
            return controller
                .GetCustomAttributes(typeof(AppSubsystemAttribute), true)
                .Cast<AppSubsystemAttribute>()
                .Select(attribute => attribute.Subsystem)
                .ToArray();
        }

        [Test]
        public void series_only_controllers_should_be_tagged_series_only()
        {
            foreach (var controller in SeriesOnlyControllers)
            {
                var subsystems = SubsystemsOf(controller);

                subsystems.Should().Contain(AppSubsystem.Series, $"{controller.Name} must be reachable with the series key");
                subsystems.Should().NotContain(AppSubsystem.Movies, $"{controller.Name} must not be reachable with the movie key");
            }
        }

        [Test]
        public void dual_domain_settings_controllers_should_accept_both_keys()
        {
            foreach (var controller in DualTaggedControllers)
            {
                var subsystems = SubsystemsOf(controller);

                subsystems.Should().Contain(AppSubsystem.Series, $"{controller.Name} must be reachable with the series key");
                subsystems.Should().Contain(AppSubsystem.Movies, $"{controller.Name} must be reachable with the movie key");
            }
        }

        [Test]
        public void metadata_config_should_be_tagged_movies_only()
        {
            var subsystems = SubsystemsOf(typeof(Sonarr.Api.V3.Config.MetadataConfigController));

            subsystems.Should().Contain(AppSubsystem.Movies);
            subsystems.Should().NotContain(AppSubsystem.Series);
        }

        [Test]
        public void movie_parse_controller_should_be_tagged_movies_only()
        {
            var subsystems = SubsystemsOf(typeof(Sonarr.Api.V3.Parse.MovieParseController));

            subsystems.Should().Contain(AppSubsystem.Movies);
            subsystems.Should().NotContain(AppSubsystem.Series);
        }

        [Test]
        public void shared_controllers_should_stay_untagged()
        {
            SubsystemsOf(typeof(Sonarr.Api.V3.Health.HealthController)).Should().BeEmpty();
            SubsystemsOf(typeof(Sonarr.Api.V3.DiskSpace.DiskSpaceController)).Should().BeEmpty();
        }
    }
}
