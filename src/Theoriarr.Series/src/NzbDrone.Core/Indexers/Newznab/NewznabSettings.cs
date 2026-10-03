using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Equ;
using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.Newznab
{
    public class NewznabSettingsValidator : AbstractValidator<NewznabSettings>
    {
        private static readonly string[] ApiKeyAllowList =
        {
            "nzbs.org",
            "nzb.life",
            "dognzb.cr",
            "nzbplanet.net",
            "nzbid.org",
            "nzbndx.com",
            "nzbindex.in"
        };

        private static bool ShouldHaveApiKey(NewznabSettings settings)
        {
            return settings.BaseUrl != null && ApiKeyAllowList.Any(c => settings.BaseUrl.ToLowerInvariant().Contains(c));
        }

        private static readonly Regex AdditionalParametersRegex = new(@"(&.+?\=.+?)+", RegexOptions.Compiled);

        public NewznabSettingsValidator()
        {
            RuleFor(c => c).Custom((c, context) =>
            {
                if (c.Categories.Empty() && c.AnimeCategories.Empty() && c.MovieCategories.Empty())
                {
                    context.AddFailure("Either 'Categories', 'Anime Categories' or 'Movie Categories' must be provided");
                }
            });

            RuleFor(c => c.BaseUrl).ValidRootUrl();
            RuleFor(c => c.ApiPath).ValidUrlBase("/api");
            RuleFor(c => c.ApiKey).NotEmpty().When(ShouldHaveApiKey);
            RuleFor(c => c.AdditionalParameters).Matches(AdditionalParametersRegex)
                                                .When(c => !c.AdditionalParameters.IsNullOrWhiteSpace());
        }
    }

    public class NewznabSettings : PropertywiseEquatable<NewznabSettings>, IIndexerSettings
    {
        // The standard Radarr movie categories, used as a search-time fallback (never persisted).
        public static readonly IReadOnlyList<int> DefaultMovieCategories = new[] { 2000, 2010, 2020, 2030, 2040, 2045, 2050, 2060 };

        private static readonly NewznabSettingsValidator Validator = new();

        public NewznabSettings()
        {
            ApiPath = "/api";
            Categories = new[] { 5030, 5040 };
            AnimeCategories = Enumerable.Empty<int>();
            MovieCategories = Enumerable.Empty<int>();
            MultiLanguages = Array.Empty<int>();
            FailDownloads = Array.Empty<int>();
        }

        [FieldDefinition(0, Label = "URL")]
        public string BaseUrl { get; set; }

        [FieldDefinition(1, Label = "IndexerSettingsApiPath", HelpText = "IndexerSettingsApiPathHelpText", Advanced = true)]
        [FieldToken(TokenField.HelpText, "IndexerSettingsApiPath", "url", "/api")]
        public string ApiPath { get; set; }

        [FieldDefinition(2, Label = "ApiKey", Privacy = PrivacyLevel.ApiKey)]
        public string ApiKey { get; set; }

        [FieldDefinition(3, Label = "IndexerSettingsCategories", Type = FieldType.Select, SelectOptionsProviderAction = "newznabCategories", HelpText = "IndexerSettingsCategoriesHelpText")]
        public IEnumerable<int> Categories { get; set; }

        [FieldDefinition(4, Label = "IndexerSettingsAnimeCategories", Type = FieldType.Select, SelectOptionsProviderAction = "newznabCategories", HelpText = "IndexerSettingsAnimeCategoriesHelpText")]
        public IEnumerable<int> AnimeCategories { get; set; }

        [FieldDefinition(15, Label = "IndexerSettingsMovieCategories", Type = FieldType.Select, SelectOptionsProviderAction = "newznabCategories", HelpText = "IndexerSettingsMovieCategoriesHelpText")]
        public IEnumerable<int> MovieCategories { get; set; }

        [FieldDefinition(5, Label = "IndexerSettingsAnimeStandardFormatSearch", Type = FieldType.Checkbox, HelpText = "IndexerSettingsAnimeStandardFormatSearchHelpText")]
        public bool AnimeStandardFormatSearch { get; set; }

        [FieldDefinition(6, Label = "IndexerSettingsAdditionalParameters", HelpText = "IndexerSettingsAdditionalNewznabParametersHelpText", Advanced = true)]
        public string AdditionalParameters { get; set; }

        [FieldDefinition(7, Type = FieldType.Select, SelectOptions = typeof(RealLanguageFieldConverter), Label = "IndexerSettingsMultiLanguageRelease", HelpText = "IndexerSettingsMultiLanguageReleaseHelpText", Advanced = true)]
        public IEnumerable<int> MultiLanguages { get; set; }

        [FieldDefinition(8, Type = FieldType.Select, SelectOptions = typeof(FailDownloads), Label = "IndexerSettingsFailDownloads", HelpText = "IndexerSettingsFailDownloadsHelpText", Advanced = true)]
        public IEnumerable<int> FailDownloads { get; set; }

        [FieldDefinition(13, Type = FieldType.Checkbox, Label = "IndexerSettingsRemoveYear", HelpText = "IndexerSettingsRemoveYearHelpText", Advanced = true)]
        public bool RemoveYear { get; set; }

        // Field 8 is used by TorznabSettings MinimumSeeders
        // If you need to add another field here, update TorznabSettings as well and this comment

        // Prowlarr's Sonarr/Radarr clients only round-trip the shared `categories` field. Keep the
        // stored value byte-identical to what the client sent and split the 2xxx (movie) range out
        // at search time so a Radarr-shaped write cannot pollute the TV category list.
        public static IEnumerable<int> ExtractMovieCategories(IEnumerable<int> categories)
        {
            return (categories ?? Enumerable.Empty<int>())
                .Where(c => c >= 2000 && c < 3000)
                .Distinct()
                .ToList();
        }

        public static bool IsMovieCategory(int category)
        {
            return category >= 2000 && category < 3000;
        }

        // Prowlarr's Sonarr and Radarr clients each round-trip only the shared `categories` field,
        // so a write from one media type would otherwise wipe the other's ranges. Keep the range the
        // incoming write does not own (a movies-shaped write owns 2xxx, a series-shaped write owns
        // everything else) while still letting the owning media type shrink its own categories.
        public static IEnumerable<int> MergeScopedCategories(IEnumerable<int> incoming, IEnumerable<int> existing, bool incomingOwnsMovieRange)
        {
            var incomingCategories = incoming ?? Enumerable.Empty<int>();
            var existingCategories = existing ?? Enumerable.Empty<int>();

            if (incomingOwnsMovieRange)
            {
                return incomingCategories.Where(IsMovieCategory)
                                         .Concat(existingCategories.Where(c => !IsMovieCategory(c)))
                                         .Distinct()
                                         .ToList();
            }

            return incomingCategories.Where(c => !IsMovieCategory(c))
                                     .Concat(existingCategories.Where(IsMovieCategory))
                                     .Distinct()
                                     .ToList();
        }

        public IReadOnlyList<int> GetTvCategories()
        {
            return (Categories ?? Enumerable.Empty<int>())
                .Where(c => c < 2000 || c >= 3000)
                .Distinct()
                .ToList();
        }

        public IReadOnlyList<int> GetMovieCategories()
        {
            var categories = (MovieCategories ?? Enumerable.Empty<int>())
                .Concat(ExtractMovieCategories(Categories))
                .Distinct()
                .ToList();

            // Radarr seeds an indexer with the standard movie categories. The unified store starts
            // from the Sonarr TV defaults, and Prowlarr's Sonarr client never sends movie categories,
            // so a series-created indexer would otherwise produce no movie request at all
            // (GetPagedRequests yields nothing for an empty category list). Fall back at search time
            // only - the stored value stays untouched so Prowlarr's round-trip stays byte-identical
            // and an explicit subset (set from the Movies side) still wins.
            return categories.Count > 0 ? categories : DefaultMovieCategories;
        }

        public virtual NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
