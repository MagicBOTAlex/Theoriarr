using System;
using System.Collections.Specialized;
using System.Linq;

namespace NzbDrone.Common.Extensions
{
    public static class StringDictionaryExtensions
    {
        /// <summary>
        /// Adds Theoriarr_* aliases for every legacy Sonarr_*/Radarr_* variable so custom
        /// scripts can migrate without breaking. Existing Theoriarr_* keys are untouched.
        /// </summary>
        /// <returns>True if at least one alias was added.</returns>
        public static bool AddTheoriarrAliases(this StringDictionary variables)
        {
            var added = false;

            foreach (var key in variables.Keys.Cast<string>().ToList())
            {
                string suffix = null;

                if (key.StartsWith("Sonarr_", StringComparison.OrdinalIgnoreCase))
                {
                    suffix = key.Substring("Sonarr_".Length);
                }
                else if (key.StartsWith("Radarr_", StringComparison.OrdinalIgnoreCase))
                {
                    suffix = key.Substring("Radarr_".Length);
                }

                if (suffix == null)
                {
                    continue;
                }

                var alias = "Theoriarr_" + suffix;

                if (!variables.ContainsKey(alias))
                {
                    variables.Add(alias, variables[key]);
                    added = true;
                }
            }

            return added;
        }
    }
}
