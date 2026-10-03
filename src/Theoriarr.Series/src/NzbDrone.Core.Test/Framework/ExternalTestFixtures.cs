using System;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;

namespace NzbDrone.Core.Test.Framework
{
    /// <summary>
    /// Optional out-of-repo fixtures describing real (copyrighted) works used by a few tests so they
    /// can be validated against real-world naming. The values are deliberately kept out of the
    /// repository: point <c>THEORIARR_TEST_FIXTURES</c> at the file, or drop
    /// <c>theoriarr-test-fixtures.json</c> next to the repo root (<c>../theoriarr-test-fixtures.json</c>).
    /// When the file is absent those tests pass without asserting rather than failing.
    /// </summary>
    public static class ExternalTestFixtures
    {
        private const string FileName = "theoriarr-test-fixtures.json";
        private const string EnvironmentVariable = "THEORIARR_TEST_FIXTURES";

        private static readonly Lazy<ExternalTestFixtureData> Fixtures = new Lazy<ExternalTestFixtureData>(Load);

        public static bool IsAvailable => Fixtures.Value != null;

        public static ExternalTestFixtureData Data => Fixtures.Value;

        private static string ResolvePath()
        {
            var explicitPath = Environment.GetEnvironmentVariable(EnvironmentVariable);

            if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            {
                return explicitPath;
            }

            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);

            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
                {
                    var sibling = Path.GetFullPath(Path.Combine(directory.FullName, "..", FileName));
                    return File.Exists(sibling) ? sibling : null;
                }

                directory = directory.Parent;
            }

            return null;
        }

        private static ExternalTestFixtureData Load()
        {
            var path = ResolvePath();

            if (path == null)
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<ExternalTestFixtureData>(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public class ExternalTestFixtureData
    {
        [JsonProperty("seriesTitle")]
        public string SeriesTitle { get; set; }

        [JsonProperty("tvdbId")]
        public int TvdbId { get; set; }

        [JsonProperty("sceneAlias")]
        public string SceneAlias { get; set; }

        [JsonProperty("joinedParsedTitle")]
        public string JoinedParsedTitle { get; set; }

        [JsonProperty("parenthesisedRelease")]
        public string ParenthesisedRelease { get; set; }

        [JsonProperty("franchiseParsedTitle")]
        public string FranchiseParsedTitle { get; set; }

        [JsonProperty("franchiseRelease")]
        public string FranchiseRelease { get; set; }
    }
}
