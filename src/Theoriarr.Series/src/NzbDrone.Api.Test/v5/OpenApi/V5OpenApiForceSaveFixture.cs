using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;

namespace NzbDrone.Api.Test.v5.OpenApi
{
    [TestFixture]
    public class V5OpenApiForceSaveFixture
    {
        private static readonly (string Path, string Method)[] ProviderCreateAndUpdateOperations =
        {
            ("/api/v5/connection", "post"),
            ("/api/v5/connection/{id}", "put"),
            ("/api/v5/downloadclient", "post"),
            ("/api/v5/downloadclient/{id}", "put"),
            ("/api/v5/importlist", "post"),
            ("/api/v5/importlist/{id}", "put"),
            ("/api/v5/indexer", "post"),
            ("/api/v5/indexer/{id}", "put"),
            ("/api/v5/metadata", "post"),
            ("/api/v5/metadata/{id}", "put")
        };

        [Test]
        public void v5_openapi_should_document_force_save_for_every_provider_create_and_update()
        {
            using var document = JsonDocument.Parse(File.ReadAllText(FindOpenApiPath()));
            var paths = document.RootElement.GetProperty("paths");

            foreach (var (path, method) in ProviderCreateAndUpdateOperations)
            {
                var parameters = paths.GetProperty(path).GetProperty(method).GetProperty("parameters");

                var forceSave = parameters.EnumerateArray()
                    .SingleOrDefault(p => p.GetProperty("name").GetString() == "forceSave");

                forceSave.ValueKind.Should().NotBe(JsonValueKind.Undefined, $"{method.ToUpperInvariant()} {path} should document forceSave");
                forceSave.GetProperty("in").GetString().Should().Be("query");
                forceSave.GetProperty("schema").GetProperty("type").GetString().Should().Be("boolean");
                forceSave.GetProperty("schema").GetProperty("default").GetBoolean().Should().BeFalse();
            }

            var count = ProviderCreateAndUpdateOperations
                .Select(op => paths.GetProperty(op.Path).GetProperty(op.Method).GetProperty("parameters").EnumerateArray()
                    .Count(p => p.GetProperty("name").GetString() == "forceSave"))
                .Sum();

            count.Should().Be(10, "V5 should document forceSave on the same 10 provider create/update operations as V3");
        }

        private static string FindOpenApiPath()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "Sonarr.Api.V5", "openapi.json");

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                candidate = Path.Combine(directory.FullName, "src", "Sonarr.Api.V5", "openapi.json");

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            throw new FileNotFoundException("Could not locate Sonarr.Api.V5/openapi.json");
        }
    }
}
