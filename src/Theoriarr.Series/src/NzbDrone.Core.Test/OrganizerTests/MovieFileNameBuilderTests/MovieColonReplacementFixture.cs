using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.OrganizerTests.MovieFileNameBuilderTests
{
    [TestFixture]
    public class MovieColonReplacementFixture : CoreTest<MovieFileNameBuilder>
    {
        private Movie _movie;
        private MovieFile _movieFile;
        private NamingConfig _namingConfig;

        [SetUp]
        public void Setup()
        {
            _movie = Builder<Movie>.CreateNew()
                    .With(m => m.MovieMetadata = new MovieMetadata { Title = "CSI: Vegas", Year = 2000 })
                    .With(m => m.Path = @"C:\Test\CSI".AsOsAgnostic())
                    .Build();

            _namingConfig = NamingConfig.Default;
            _namingConfig.RenameMovies = true;
            _namingConfig.StandardMovieFormat = "{Movie Title}";

            Mocker.GetMock<INamingConfigService>()
                  .Setup(c => c.GetConfig()).Returns(_namingConfig);

            Mocker.GetMock<IQualityDefinitionService>()
                .Setup(v => v.Get(Moq.It.IsAny<Quality>()))
                .Returns<Quality>(v => Quality.DefaultQualityDefinitions.First(c => c.Quality == v));

            _movieFile = new MovieFile
            {
                Quality = new QualityModel(Quality.HDTV720p),
                ReleaseGroup = "TheoriarrTest",
                RelativePath = "CSI.Vegas.2000.mkv".AsOsAgnostic()
            };
        }

        private string Build()
        {
            return Subject.BuildFileName(_movie, _movieFile, _namingConfig, new List<CustomFormat>());
        }

        [Test]
        public void should_replace_colon_followed_by_space_with_space_dash_space_by_default()
        {
            Build().Should().Be("CSI - Vegas");
        }

        [TestCase("{Movie Title}", "CSI: Vegas", ColonReplacementFormat.Custom, "\ua789", "CSI\ua789 Vegas")]
        [TestCase("{Movie Title}", "CSI: Vegas", ColonReplacementFormat.Custom, "∶", "CSI∶ Vegas")]
        [TestCase("{Movie Title}", "CSI: Vegas", ColonReplacementFormat.Dash, null, "CSI- Vegas")]
        [TestCase("{Movie Title}", "CSI: Vegas", ColonReplacementFormat.Delete, null, "CSI Vegas")]
        [TestCase("{Movie Title}", "CSI: Vegas", ColonReplacementFormat.SpaceDash, null, "CSI - Vegas")]
        [TestCase("{Movie Title}", "CSI: Vegas", ColonReplacementFormat.SpaceDashSpace, null, "CSI - Vegas")]
        [TestCase("{Movie Title}", "CSI:Vegas", ColonReplacementFormat.SpaceDashSpace, null, "CSI - Vegas")]
        public void should_replace_colon_with_expected_result(string pattern, string title, ColonReplacementFormat replacementFormat, string customFormat, string expected)
        {
            _movie.Title = title;
            _namingConfig.StandardMovieFormat = pattern;
            _namingConfig.ColonReplacementFormat = replacementFormat;
            _namingConfig.CustomColonReplacementFormat = customFormat;

            Build().Should().Be(expected);
        }

        [TestCase("", "_")]
        [TestCase("   ", "_")]
        [TestCase("!", "_")]
        [TestCase("A", "A")]
        [TestCase("!A", "A")]
        [TestCase(".hack", "H")]
        [TestCase("¡Mucha Lucha!", "M")]
        [TestCase("30 Rock", "3")]
        public void should_not_throw_on_short_or_non_alphanumeric_titles(string title, string expected)
        {
            MovieFileNameBuilder.TitleFirstCharacter(title).Should().Be(expected);
        }
    }
}
