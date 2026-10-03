using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;
using Sonarr.Api.V3.Profiles.Quality;

namespace NzbDrone.Api.Test.v3.Profiles
{
    [TestFixture]
    public class QualityProfileResourceMapperFixture
    {
        [Test]
        public void should_round_trip_movie_language()
        {
            var model = new QualityProfile
            {
                Id = 1,
                Name = "Movies",
                Language = Language.English,
                Items = new List<QualityProfileQualityItem>(),
                FormatItems = new List<ProfileFormatItem>()
            };

            var resource = model.ToResource();

            resource.Language.Should().Be(Language.English);

            var roundTripped = resource.ToModel();

            roundTripped.Language.Should().Be(Language.English);
        }

        [Test]
        public void should_serialize_movie_language()
        {
            var resource = new QualityProfileResource
            {
                Id = 1,
                Name = "Movies",
                Language = Language.English
            };

            var json = STJson.ToJson(resource);

            json.Should().Contain("\"language\"");
            json.Should().Contain("\"id\": 1");
            json.Should().Contain("\"name\": \"English\"");
        }

        [Test]
        public void should_deserialize_movie_language()
        {
            var resource = STJson.Deserialize<QualityProfileResource>(
                "{ \"language\": { \"id\": 1, \"name\": \"English\" } }");

            resource.Language.Should().Be(Language.English);
        }
    }
}
