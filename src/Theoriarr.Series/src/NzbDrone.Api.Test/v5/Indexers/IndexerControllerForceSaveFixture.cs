using System;
using System.Collections.Generic;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;
using NzbDrone.SignalR;
using NzbDrone.Test.Common;
using Sonarr.Api.V5.Indexers;
using Sonarr.Api.V5.Provider;

namespace NzbDrone.Api.Test.v5.Indexers
{
    [TestFixture]
    public class IndexerControllerForceSaveFixture : TestBase
    {
        private Mock<IIndexerFactory> _indexerFactory;
        private TestIndexerController _subject;

        [SetUp]
        public void Setup()
        {
            _indexerFactory = Mocker.GetMock<IIndexerFactory>();
            _indexerFactory.Setup(f => f.All()).Returns(new List<IndexerDefinition>());
            _indexerFactory.Setup(f => f.Create(It.IsAny<IndexerDefinition>())).Returns<IndexerDefinition>(d => d);
            _indexerFactory.Setup(f => f.Get(It.IsAny<int>())).Returns<int>(id => new IndexerDefinition
            {
                Id = id,
                Name = "Warning",
                Implementation = "Newznab",
                ConfigContract = "Test.WarningIndexerSettings",
                EnableRss = true,
                Settings = new WarningIndexerSettings()
            });

            _indexerFactory.Setup(f => f.Test(It.IsAny<IndexerDefinition>())).Returns(new NzbDroneValidationResult());

            _subject = new TestIndexerController(
                Mocker.GetMock<IBroadcastSignalRMessage>().Object,
                _indexerFactory.Object,
                new WarningIndexerResourceMapper(),
                new IndexerBulkResourceMapper());

            // The response helpers need an IUrlHelper; the controller has no MVC context in a unit test.
            _subject.Url = Mocker.GetMock<IUrlHelper>().Object;
        }

        [Test]
        public void create_should_reject_warning_only_indexer_without_force_save()
        {
            Action act = () => _subject.CreateProvider(new IndexerResource { Name = "Warning" });

            act.Should().Throw<ValidationException>();
            _indexerFactory.Verify(f => f.Create(It.IsAny<IndexerDefinition>()), Times.Never());
        }

        [Test]
        public void create_should_accept_warning_only_indexer_with_force_save()
        {
            Action act = () => _subject.CreateProvider(new IndexerResource { Name = "Warning" }, forceSave: true);

            act.Should().NotThrow();
            _indexerFactory.Verify(f => f.Create(It.IsAny<IndexerDefinition>()), Times.Once());
        }

        [Test]
        public void update_should_accept_warning_only_indexer_with_force_save()
        {
            var existingDefinition = new IndexerDefinition
            {
                Id = 7,
                Name = "Warning",
                Implementation = "Newznab",
                ConfigContract = "Test.WarningIndexerSettings",
                EnableRss = true,
                Settings = new WarningIndexerSettings()
            };

            _indexerFactory.Setup(f => f.Find(7)).Returns(existingDefinition);
            _indexerFactory.Setup(f => f.Get(7)).Returns(existingDefinition);

            Action act = () => _subject.UpdateProvider(7, new IndexerResource { Id = 7, Name = "Warning" }, forceSave: true);

            act.Should().NotThrow();
            _indexerFactory.Verify(f => f.Update(It.IsAny<IndexerDefinition>()), Times.Once());
        }

        [Test]
        public void create_force_save_should_reject_hard_connection_failure()
        {
            _indexerFactory.Setup(f => f.Test(It.IsAny<IndexerDefinition>()))
                .Returns(new NzbDroneValidationResult(new[] { new NzbDroneValidationFailure("BaseUrl", "hard failure") }));

            Action act = () => _subject.CreateProvider(new IndexerResource { Name = "Warning" }, forceSave: true);

            act.Should().Throw<ValidationException>();
            _indexerFactory.Verify(f => f.Create(It.IsAny<IndexerDefinition>()), Times.Never());
        }

        [Test]
        public void update_force_save_should_reject_hard_connection_failure()
        {
            var existingDefinition = new IndexerDefinition
            {
                Id = 9,
                Name = "Warning",
                Implementation = "Newznab",
                ConfigContract = "Test.WarningIndexerSettings",
                EnableRss = true,
                Settings = new WarningIndexerSettings()
            };

            _indexerFactory.Setup(f => f.Find(9)).Returns(existingDefinition);
            _indexerFactory.Setup(f => f.Get(9)).Returns(existingDefinition);
            _indexerFactory.Setup(f => f.Test(It.IsAny<IndexerDefinition>()))
                .Returns(new NzbDroneValidationResult(new[] { new NzbDroneValidationFailure("BaseUrl", "hard failure") }));

            Action act = () => _subject.UpdateProvider(9, new IndexerResource { Id = 9, Name = "Warning" }, forceSave: true);

            act.Should().Throw<ValidationException>();
            _indexerFactory.Verify(f => f.Update(It.IsAny<IndexerDefinition>()), Times.Never());
        }

        [Test]
        public void create_should_still_skip_connection_test_when_skip_testing_is_set()
        {
            _indexerFactory.Setup(f => f.Test(It.IsAny<IndexerDefinition>()))
                .Returns(new NzbDroneValidationResult(new[] { new NzbDroneValidationFailure("BaseUrl", "hard failure") }));

            Action act = () => _subject.CreateProvider(new IndexerResource { Name = "Warning" }, skipTesting: true, skipValidation: SkipValidation.Warnings);

            act.Should().NotThrow();
            _indexerFactory.Verify(f => f.Create(It.IsAny<IndexerDefinition>()), Times.Once());
        }

        private class TestIndexerController : ProviderControllerBase<IndexerResource, IndexerBulkResource, IIndexer, IndexerDefinition>
        {
            public TestIndexerController(IBroadcastSignalRMessage signalRBroadcaster,
                IProviderFactory<IIndexer, IndexerDefinition> providerFactory,
                ProviderResourceMapper<IndexerResource, IndexerDefinition> resourceMapper,
                ProviderBulkResourceMapper<IndexerBulkResource, IndexerDefinition> bulkResourceMapper)
                : base(signalRBroadcaster, providerFactory, "indexer", resourceMapper, bulkResourceMapper)
            {
            }
        }

        private class WarningIndexerResourceMapper : IndexerResourceMapper
        {
            public override IndexerDefinition ToModel(IndexerResource resource, IndexerDefinition existingDefinition)
            {
                return new IndexerDefinition
                {
                    Id = resource.Id,
                    Name = resource.Name ?? "Warning",
                    Implementation = "Newznab",
                    ConfigContract = "Test.WarningIndexerSettings",
                    EnableRss = true,
                    Settings = new WarningIndexerSettings()
                };
            }

            public override IndexerResource ToResource(IndexerDefinition definition)
            {
                return new IndexerResource
                {
                    Id = definition.Id,
                    Name = definition.Name
                };
            }
        }

        private class WarningIndexerSettings : IIndexerSettings
        {
            public string BaseUrl { get; set; } = "http://indexer.local";
            public IEnumerable<int> MultiLanguages { get; set; } = Array.Empty<int>();
            public IEnumerable<int> FailDownloads { get; set; } = Array.Empty<int>();

            public NzbDroneValidationResult Validate()
            {
                return new NzbDroneValidationResult(new[]
                {
                    new NzbDroneValidationFailure("BaseUrl", "warning") { IsWarning = true }
                });
            }
        }
    }
}
