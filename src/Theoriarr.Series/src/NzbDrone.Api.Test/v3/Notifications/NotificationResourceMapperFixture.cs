using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.Webhook;
using NzbDrone.Test.Common;
using Sonarr.Api.V3.Notifications;
using Sonarr.Http.ClientSchema;

namespace NzbDrone.Api.Test.v3.Notifications
{
    [TestFixture]
    public class NotificationResourceMapperFixture : TestBase
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ILocalizationService>()
                .Setup(s => s.GetLocalizedString(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Returns<string, Dictionary<string, object>>((s, d) => s);

            SchemaBuilder.Initialize(Mocker.Container);
        }

        [Test]
        public void should_serialize_movie_triggers()
        {
            var resource = new NotificationResource
            {
                OnMovieAdded = true,
                OnMovieDelete = true,
                OnMovieFileDelete = true,
                OnMovieFileDeleteForUpgrade = true,
                SupportsOnMovieAdded = true,
                SupportsOnMovieDelete = true,
                SupportsOnMovieFileDelete = true,
                SupportsOnMovieFileDeleteForUpgrade = true
            };

            var json = STJson.ToJson(resource);

            json.Should().Contain("\"onMovieAdded\": true");
            json.Should().Contain("\"onMovieDelete\": true");
            json.Should().Contain("\"onMovieFileDelete\": true");
            json.Should().Contain("\"onMovieFileDeleteForUpgrade\": true");
            json.Should().Contain("\"supportsOnMovieAdded\": true");
            json.Should().Contain("\"supportsOnMovieDelete\": true");
            json.Should().Contain("\"supportsOnMovieFileDelete\": true");
            json.Should().Contain("\"supportsOnMovieFileDeleteForUpgrade\": true");
        }

        [Test]
        public void should_deserialize_movie_triggers()
        {
            var resource = STJson.Deserialize<NotificationResource>(
                "{ \"onMovieAdded\": true, \"onMovieDelete\": true, \"onMovieFileDelete\": true, \"onMovieFileDeleteForUpgrade\": true, \"supportsOnMovieAdded\": true, \"supportsOnMovieDelete\": true, \"supportsOnMovieFileDelete\": true, \"supportsOnMovieFileDeleteForUpgrade\": true }");

            resource.OnMovieAdded.Should().BeTrue();
            resource.OnMovieDelete.Should().BeTrue();
            resource.OnMovieFileDelete.Should().BeTrue();
            resource.OnMovieFileDeleteForUpgrade.Should().BeTrue();
            resource.SupportsOnMovieAdded.Should().BeTrue();
            resource.SupportsOnMovieDelete.Should().BeTrue();
            resource.SupportsOnMovieFileDelete.Should().BeTrue();
            resource.SupportsOnMovieFileDeleteForUpgrade.Should().BeTrue();
        }

        [Test]
        public void should_round_trip_movie_triggers()
        {
            var definition = new NotificationDefinition
            {
                Name = "Webhook",
                Implementation = "Webhook",
                ImplementationName = "Webhook",
                ConfigContract = "NzbDrone.Core.Notifications.Webhook.WebhookSettings",
                Settings = new WebhookSettings(),
                OnMovieAdded = true,
                OnMovieDelete = true,
                OnMovieFileDelete = true,
                OnMovieFileDeleteForUpgrade = true,
                SupportsOnMovieAdded = true,
                SupportsOnMovieDelete = true,
                SupportsOnMovieFileDelete = true,
                SupportsOnMovieFileDeleteForUpgrade = true
            };

            var mapper = new NotificationResourceMapper();
            var resource = mapper.ToResource(definition);

            resource.OnMovieAdded.Should().BeTrue();
            resource.OnMovieDelete.Should().BeTrue();
            resource.OnMovieFileDelete.Should().BeTrue();
            resource.OnMovieFileDeleteForUpgrade.Should().BeTrue();
            resource.SupportsOnMovieAdded.Should().BeTrue();
            resource.SupportsOnMovieDelete.Should().BeTrue();
            resource.SupportsOnMovieFileDelete.Should().BeTrue();
            resource.SupportsOnMovieFileDeleteForUpgrade.Should().BeTrue();

            var model = mapper.ToModel(resource, definition);

            model.OnMovieAdded.Should().BeTrue();
            model.OnMovieDelete.Should().BeTrue();
            model.OnMovieFileDelete.Should().BeTrue();
            model.OnMovieFileDeleteForUpgrade.Should().BeTrue();
            model.SupportsOnMovieAdded.Should().BeTrue();
            model.SupportsOnMovieDelete.Should().BeTrue();
            model.SupportsOnMovieFileDelete.Should().BeTrue();
            model.SupportsOnMovieFileDeleteForUpgrade.Should().BeTrue();
        }
    }
}
