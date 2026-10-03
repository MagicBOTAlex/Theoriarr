using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.MovieImport;
using NzbDrone.Core.MediaFiles.MovieImport.Specifications;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.MovieImport.Specifications
{
    [TestFixture]
    public class NotSampleSpecificationFixture : CoreTest<NotSampleSpecification>
    {
        private LocalMovie _localMovie;

        [SetUp]
        public void Setup()
        {
            _localMovie = new LocalMovie
                          {
                              Movie = Builder<Movie>.CreateNew()
                                                    .With(m => m.MovieMetadata = Builder<MovieMetadata>.CreateNew().Build())
                                                    .Build(),
                              Path = "/test/movie.mkv"
                          };
        }

        [Test]
        public void should_include_probe_error_in_indeterminate_rejection()
        {
            Mocker.GetMock<IDetectSample>()
                  .Setup(s => s.IsSample(It.IsAny<MovieMetadata>(), It.IsAny<string>(), out It.Ref<string>.IsAny))
                  .Returns((MovieMetadata _, string _, out string reason) =>
                  {
                      reason = "the file could not be read (invalid data found when processing input)";
                      return DetectSampleResult.Indeterminate;
                  });

            var decision = Subject.IsSatisfiedBy(_localMovie, null);

            decision.Accepted.Should().BeFalse();
            decision.Reason.Should().Be(ImportRejectionReason.SampleIndeterminate);
            decision.Message.Should().Contain("invalid data found when processing input");
        }
    }
}
