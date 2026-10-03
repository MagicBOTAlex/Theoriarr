using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Parser
{
    public static class Parser
    {
        private static readonly Logger Logger = NzbDroneLogger.GetLogger(typeof(Parser));

        // Untrusted release titles are matched against many regexes; a match timeout stops a
        // pathological title from pinning a parse thread with catastrophic backtracking.
        private static readonly TimeSpan RegexMatchTimeout = TimeSpan.FromSeconds(5);

        private static Regex R(string pattern, RegexOptions options = RegexOptions.None)
        {
            return new Regex(pattern, options, RegexMatchTimeout);
        }

        private static readonly Regex[] ReportTitleRegex = new[]
            {
                // Anime - Absolute Episode Number + Title + Season+Episode
                // Todo: This currently breaks series that start with numbers
//                new Regex(@"^(?:(?<absoluteepisode>\d{2,3})(?:_|-|\s|\.)+)+(?<title>.+?)(?:\W|_)+(?:S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:\-|[ex]|\W[ex]){1,2}(?<episode>\d{2}(?!\d+)))+)",
//                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Daily episode with year in series title and air time after date (Plex DVR format)
                R(@"^^(?<title>.+?\((?<titleyear>\d{4})\))[-_. ]+(?<airyear>19[4-9]\d|20\d\d)(?<sep>[-_]?)(?<airmonth>0\d|1[0-2])\k<sep>(?<airday>[0-2]\d|3[01])[-_. ]\d{2}[-_. ]\d{2}[-_. ]\d{2}",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Daily episodes without title (2018-10-12, 20181012) (Strict pattern to avoid false matches)
                R(@"^(?<airyear>19[6-9]\d|20\d\d)(?<sep>[-_]?)(?<airmonth>0\d|1[0-2])\k<sep>(?<airday>[0-2]\d|3[01])(?!\d)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Multi-Part episodes without a title (S01E05.S01E06)
                R(@"^(?:\W*S(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:e{1,2}(?<episode>\d{1,3}(?!\d+)))+){2,}",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Multi-Part episodes without a title (1x05.1x06)
                R(@"^(?:\W*(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:x{1,2}(?<episode>\d{1,3}(?!\d+)))+){2,}",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes without a title, Multi (S01E04E05, 1x04x05, etc)
                R(@"^(?:S?(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:(?:[-_]|[ex]){1,2}(?<episode>\d{2,3}(?!\d+))){2,})",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Split episodes (S01E05a, S01E05b, etc)
                R(@"^(?<title>.+?)(?:S?(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:(?:[-_ ]?[ex])(?<episode>\d{2,3}(?!\d+))(?<splitepisode>[a-d])(?:[ _.])))",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes without a title, Single (S01E05, 1x05)
                R(@"^(?:S?(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:(?:[-_ ]?[ex])(?<episode>\d{2,3}(?!\d+))))",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title Absolute (Season+Episode)
                R(@"^(?:\[(?<subgroup>.+?)\](?:_|-|\s|\.)?)(?<title>.+?)[-_. ]+(?<absoluteepisode>(?<!\d+)\d{2,3}(\.\d{1,2})?(?!\d+))(?:[-_. ])+\((?:S(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:[ex]|\W[ex]){1,2}(?<episode>\d{2}(?!\d+))))(?:v\d+)?(?:\)(?!\d+)).*?(?<hash>[(\[][0-9A-F]{8}[)\]])?$",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title Absolute - Season+Episode
                R(@"^(?:\[(?<subgroup>.+?)\](?:_|-|\s|\.)?)(?<title>.+?)[-_. ]+(?<absoluteepisode>(?<!\d+)\d{2,3}(\.\d{1,2})?(?!\d+))(?:[-_. ](?<![()\[!]))+(?:S(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:[ex]|\W[ex]){1,2}(?<episode>\d{2}(?!\d+))))(?:v\d+)?(?:[_. ](?!\d+)).*?(?<hash>[(\[][0-9A-F]{8}[)\]])$",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title Season+Episode
                R(@"^(?:\[(?<subgroup>.+?)\](?:_|-|\s|\.)?)(?<title>.+?)(?:[-_\W](?<![()\[!]))+(?:S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:[ex]|\W[ex]){1,2}(?<episode>\d{2}(?!\d+)))+)(?:v\d+)?(?:[_. ](?!\d+)).*?(?<hash>[(\[][0-9A-F]{8}[)\]])?$",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title Episode Absolute Episode Number ([SubGroup] Series Title Episode 01)
                R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)(?<title>.+?)[-_. ]+?(?:Episode)(?:[-_. ]+(?<absoluteepisode>(?<!\d+)\d{2,3}(\.\d{1,2})?(?!\d+)))+.*?(?<hash>[(\[][0-9A-F]{8}[)\]])?$",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title Absolute Episode Number + Season+Episode
                R(@"^(?:\[(?<subgroup>.+?)\](?:_|-|\s|\.)?)(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+(?<absoluteepisode>\d{2,3}(\.\d{1,2})?))+(?:_|-|\s|\.)+(?:S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:\-|[ex]|\W[ex]){1,2}(?<episode>\d{2}(?!\d+)))+).*?(?<hash>[(\[][0-9A-F]{8}[)\]])?(?:$|\.)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title Multi-episode + optional Absolute Episode Number
                R(@"^(?:\[(?<subgroup>.+?)\](?:_|-|\s|\.)?)(?<title>.+?)(?:[-_\W](?<![()\[!]))+(?:S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:\-|[ex]|\W[ex]){1,2}(?<episode>\d{2}(?!\d+))){2,})(?:(?:_|-|\s|\.)+(?<absoluteepisode>(?<!\d+)\d{2,3}(\.\d{1,2})?(?!\d+|\-[a-z])))*.*?(?<hash>[(\[][0-9A-F]{8}[)\]])?$",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title Single episode + Absolute Episode Number
                R(@"^(?:\[(?<subgroup>.+?)\](?:_|-|\s|\.)?)(?<title>.+?)(?:[-_\W](?<![()\[!]))+(?:S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:\-|[ex]|\W[ex]){1,2}(?<episode>\d{2}(?!\d+))))(?:(?:_|-|\s|\.)+(?<absoluteepisode>(?<!\d+)\d{2,3}(\.\d{1,2})?(?!\d+|\-[a-z])))+.*?(?<hash>[(\[][0-9A-F]{8}[)\]])?$",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title with trailing number Absolute Episode Number - Batch separated with tilde
                R(@"^\[(?<subgroup>.+?)\][-_. ]?(?<title>.+?[^-]+?)(?:(?<![-_. ]|\b[0]\d+) - )[-_. ]?(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+))\s?~\s?(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+))(?:[-_. ]+(?<special>special|ova|ovd))?.*?(?<hash>[(\[][0-9A-F]{8}[)\]])?(?:$|\.mkv)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title with season number in brackets Absolute Episode Number
                R(@"^\[(?<subgroup>.+?)\][-_. ]?(?<title>[^-]+?)[_. ]+?\(Season[_. ](?<season>\d+)\)[-_. ]+?(?:[-_. ]?(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+)))+(?:[-_. ]+(?<special>special|ova|ovd))?.*?(?<hash>[(\[][0-9A-F]{8}[)\]])?(?:$|\.mkv)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title with trailing 3-digit number and sub title - Absolute Episode Number
                R(@"^\[(?<subgroup>[^\]]+?)\][-_. ]?(?<title>[^]]+?)(?:[-_. ]{3}?(?<absoluteepisode>\d{2}(\.\d{1,2})?(?!-?\d+|-[a-z]+)))+(?:[-_. ]+(?<special>special|ova|ovd))?.*?(?<hash>[(\[][0-9A-F]{8}[)\]])?(?:$|\.mkv)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title with trailing number Absolute Episode Number
                R(@"^\[(?<subgroup>.+?)\][-_. ]?(?<title>[^-]+?)(?:(?<![-_. ]|\b[0]\d+) - )(?:[-_. ]?(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+)))+(?:[-_. ]+(?<special>special|ova|ovd))?.*?(?<hash>[(\[][0-9A-F]{8}[)\]])?(?:$|\.mkv)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title with trailing number S## (Full season)
                R(@"^\[(?<subgroup>.+?)\][-_. ]?(?<title>.+?)[-_. ]+(?:S(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?![ex]?\d+))).+?(?:$|\.mkv)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title with trailing number Absolute Episode Number
                R(@"^\[(?<subgroup>.+?)\][-_. ]?(?<title>[^-]+?)(?:(?<![-_. ]|\b[0]\d+)[_ ]+)(?:[-_. ]?(?<absoluteepisode>\d{3}(\.\d{1,2})?(?!\d+|-[a-z]+)))+(?:[-_. ]+(?<special>special|ova|ovd))?.*?(?<hash>[(\[][0-9A-F]{8}[)\]])?(?:$|\.mkv)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title - Absolute Episode Number
                R(@"^\[(?<subgroup>.+?)\][-_. ]?(?<title>.+?)(?:(?<!\b[0]\d+))(?:[. ]-[. ](?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+|[-])))+(?:[-_. ]+(?<special>special|ova|ovd))?.*?(?<hash>[(\[][0-9A-F]{8}[)\]])?(?:$|\.mkv)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title Absolute Episode Number - Absolute Episode Number (batches without full separator between title and absolute episode numbers)
                R(@"^\[(?<subgroup>.+?)\][-_. ]?(?<title>.+?)(?:(?<!\b[0]\d+))(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+|[-]))[. ]-[. ](?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+|[-]))(?:[-_. ]+(?<special>special|ova|ovd))?.*?(?<hash>[(\[][0-9A-F]{8}[)\]])?(?:$|\.mkv)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title (Season only)
                R(@"^(?:\[(?<subgroup>.+?)\][-_. ])?(?<title>.+?)[-_. ]+?[\[(](?:S|Season|Saison|Series|Stagione|Sezon)[-_. ]?(?<season>\d{1,2}(?![-_. ]?\d+))(?:[-_. )\]]|$)+(?<extras>EXTRAS|SUBPACK)?(?!\\)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - [SubGroup] Title Absolute Episode Number
                R(@"^\[(?<subgroup>.+?)\][-_. ]?(?<title>.+?)[-_. ]+\(?(?:[-_. ]?#?(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+|-[a-z]+)))+\)?(?:[-_. ]+(?<special>special|ova|ovd))?.*?(?<hash>[(\[][0-9A-F]{8}[)\]])?(?:$|\.mkv)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Multi-episode Repeated (S01E05 - S01E06)
                R(@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+S(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:(?:e|[-_. ]e){1,2}(?<episode>\d{1,3}(?!\d+)))+){2,}",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Multi-episode Repeated (1x05 - 1x06)
                R(@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:x{1,2}(?<episode>\d{1,3}(?!\d+)))+){2,}",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Single episodes with a title (S01E05, 1x05, etc) followed by ".5 [SP]"
                R(@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+S?(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))(?:[ex]|\W[ex]|_){1,2}(?<episode>\d{2,3}(?!\d+|(?:[ex]|\W[ex]|_|-){1,2}\d+))(?<special>\.5[ .]\[SP\]))",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Single episodes with a title (S01E05, 1x05, etc) and trailing info in slashes
                R(@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+S?(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))(?:[ex]|\W[ex]|_){1,2}(?<episode>\d{2,3}(?!\d+|(?:[ex]|\W[ex]|_|-){1,2}\d+))).+?(?:\[.+?\])(?!\\)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - Title Season EpisodeNumber + Absolute Episode Number [SubGroup]
                R(@"^(?<title>.+?)(?:[-_\W](?<![()\[!]))+(?:S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:[ex]|\W[ex]|-){1,2}(?<episode>(?<!\d+)\d{2}(?!\d+)))+)[-_. (]+?(?:[-_. ]?(?<absoluteepisode>(?<!\d+)\d{3}(\.\d{1,2})?(?!\d+|[pi])))+.+?\[(?<subgroup>.+?)\](?:$|\.mkv)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Multi-Episode with a title (S01E05E06, S01E05-06, S01E05 E06, etc) and trailing info in slashes
                R(@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+S?(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))(?:[ex]|\W[ex]|_){1,2}(?<episode>\d{2,3}(?!\d+))(?:(?:\-|[ex]|\W[ex]|_){1,2}(?<episode>\d{2,3}(?!\d+)))+).+?(?:\[.+?\])(?!\\)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - Title Absolute Episode Number [SubGroup] [Hash]? (Series Title Episode 99-100 [RlsGroup] [ABCD1234])
                R(@"^(?<title>.+?)[-_. ]Episode(?:[-_. ]+(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+)))+(?:.+?)\[(?<subgroup>.+?)\].*?(?<hash>[(\[][0-9A-F]{8}[)\]])?$",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - Title Absolute Episode Number [SubGroup] [Hash]
                R(@"^(?<title>.+?)(?:(?:_|-|\s|\.)+(?<absoluteepisode>\d{3}(\.\d{1,2})(?!\d+)))+(?:.+?)\[(?<subgroup>.+?)\].*?(?<hash>[(\[][0-9A-F]{8}[)\]])?$",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - Title Absolute Episode Number (Year) [SubGroup]
                R(@"^(?<title>.+?)[-_. ]+(?<absoluteepisode>(?<!\d+)\d{2}(?!\d+))[-_. ](\(\d{4}\))[-_. ]\[(?<subgroup>.+?)\]",
                            RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - Title with trailing number, Absolute Episode Number and hash
                R(@"^(?<title>[^-]+?)(?:(?<![-_. ]|\b[0]\d+) - )(?:[-_. ]?(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+)))+(?:[-_. ]+(?<special>special|ova|ovd))?.*?(?<hash>[(\[][0-9A-F]{8}[)\]])(?:$|\.mkv)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - Title Absolute Episode Number [Hash]
                R(@"^(?<title>.+?)(?:(?:_|-|\s|\.)+(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+)))+(?:[-_. ]+(?<special>special|ova|ovd))?[-_. ]+.*?(?<hash>[(\[][0-9A-F]{8}[)\]])$",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes with airdate AND season/episode number, capture season/episode only
                R(@"^(?<title>.+?)?\W*(?<airdate>\d{4}\W+[0-1][0-9]\W+[0-3][0-9])(?!\W+[0-3][0-9])[-_. ](?:s?(?<season>(?<!\d+)(?:\d{1,2})(?!\d+)))(?:[ex](?<episode>(?<!\d+)(?:\d{1,3})(?!\d+)))",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes with airdate AND season/episode number
                R(@"^(?<title>.+?)?\W*(?<airyear>\d{4})\W+(?<airmonth>[0-1][0-9])\W+(?<airday>[0-3][0-9])(?!\W+[0-3][0-9]).+?(?:s?(?<season>(?<!\d+)(?:\d{1,2})(?!\d+)))(?:[ex](?<episode>(?<!\d+)(?:\d{1,3})(?!\d+)))",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes with absolute episode number AND airdate (TJET Wrestling)
                R(@"^(?<title>.+?)?[-_. ](?:e\d{2,3}(?!\d+))[-_. ](?<airyear>\d{4})\W+(?<airmonth>[0-1][0-9])\W+(?<airday>[0-3][0-9])(?!\W+[0-3][0-9]).+?(?:\[[a-z]+\])",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Single or multi episode releases with multiple titles, then season and episode numbers after the last title. (Title1 / Title2 / ... / S1E1-2 of 6)
                R(@"^((?<title>.*?)[ ._]\/[ ._])+\(?S(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:\W|_)?E?[ ._]?(?<episode>(?<!\d+)\d{1,2}(?!\d+))(?:-(?<episode>(?<!\d+)\d{1,2}(?!\d+)))?(?:[ ._]of[ ._](?<episodecount>\d{1,2}))?\)?[ ._][\(\[]",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Multi-episode with title (S01E99-100, S01E05-06)
                R(@"^(?<title>.+?)(?:[-_\W](?<![()\[!]))+S(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))E(?<episode>\d{2,3}(?!\d+))(?:-(?<episode>\d{2,3}(?!\d+)))+(?:[-_. ]|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Multi-episode with title (S01E05-06, S01E05-6)
                R(@"^(?<title>.+?)(?:[-_\W](?<![()\[!]))+S(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))E(?<episode>\d{1,2}(?!\d+))(?:-(?<episode>\d{1,2}(?!\d+)))+(?:[-_. ]|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes with a Chinese title and English title, Single episodes (S01E05, 1x05, etc) & Multi-episode (S01E05E06, S01E05-06, S01E05 E06, etc)
                R(@"^(?<title>[\u4E00-\u9FCC]+)[_. ](?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+S?(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))(?:[ex]|\W[ex]){1,2}(?<episode>\d{2,3}(?!\d+))(?:(?:\-|[ex]|\W[ex]|_){1,2}(?<episode>\d{2,3}(?!\d+)))*)(?:[-_. ]|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes with a title, Single episodes (S01E05, 1x05, etc) & Multi-episode (S01E05E06, S01E05-06, S01E05 E06, etc)
                R(@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+S?(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))(?:[ex]|\W[ex]){1,2}(?<episode>\d{2,3}(?!\d+))(?:(?:\-|[ex]|\W[ex]|_){1,2}(?<episode>\d{2,3}(?!\d+)))*)(?:[-_. ]|$)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes with a title, 4 digit season number, Single episodes (S2016E05, etc) & Multi-episode (S2016E05E06, S2016E05-06, S2016E05 E06, etc)
                R(@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+S(?<season>(?<!\d+)(?:\d{4})(?!\d+))(?:e|\We|_){1,2}(?<episode>\d{2,4}(?!\d+))(?:(?:\-|e|\We|_){1,2}(?<episode>\d{2,3}(?!\d+)))*)\W?(?!\\)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes with a title, 4 digit season number, Single episodes (2016x05, etc) & Multi-episode (2016x05x06, 2016x05-06, 2016x05 x06, etc)
                R(@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+(?<season>(?<!\d+)(?:\d{4})(?!\d+))(?:x|\Wx){1,2}(?<episode>\d{2,4}(?!\d+))(?:(?:\-|x|\Wx|_){1,2}(?<episode>\d{2,3}(?!\d+)))*)\W?(?!\\)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes with a title, Single episodes (s01.05)
                R(@"^(?<title>.+?)(?:[-_\W](?<![()\[!]))+S(?<season>(?<!\d+)(?:\d{2})(?!\d+))(?:\.)(?<episode>\d{2,3}(?!\d+))(?:[-_. ]|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Multi-season pack
                R(@"^(?<title>.+?)(Complete Series)?[-_. ]+(?:S|(?:Season|Saison|Series|Stagione)[_. ])(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))(?:(?<seasonrange>[-_. ]{1}|[-_. ]{3})(?:S|(?:Season|Saison|Series|Stagione)[_. ])?(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))){1,}",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Partial season pack
                R(@"^(?<title>.+?)(?:\W+S(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))\W+(?:(?:(?:Part|Vol)\W?|(?<!\d+\W+)e|p)(?<seasonpart>\d{1,2}(?!\d+)))+)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - 4 digit absolute episode number in batch (1017-1088) or 1017-1088
                R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)(?<title>.+?)[-_. ]+?\(?(?<absoluteepisode>\d{4}(\.\d{1,2})?(?!\d+))[-](?<absoluteepisode>\d{4}(\.\d{1,2})?(?!\d+))\)?",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - 4 digit absolute episode number
                R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)(?<title>.+?)[-_. ]+?(?<absoluteepisode>\d{4}(\.\d{1,2})?(?!\d+))",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Mini-Series, treated as season 1, multi episodes are labelled as E1-E2
                R(@"^(?<title>.+?)(?:[-._ ][e])(?<episode>\d{2,3}(?!\d+))(?:(?:\-?[e])(?<episode>\d{2,3}(?!\d+)))+",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // 5 digit episode number with a title
                R(@"^(?:(?<title>.+?)(?:_|-|\s|\.)+)(?:S?(?<season>(?<!\d+)\d{1,2}(?!\d+)))(?:(?:\-|[ex]|\W[ex]|_){1,2}(?<episode>(?<!\d+)\d{5}(?!\d+)))",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // 5 digit multi-episode with a title
                R(@"^(?:(?<title>.+?)(?:_|-|\s|\.)+)(?:S?(?<season>(?<!\d+)\d{1,2}(?!\d+)))(?:(?:[-_. ]{1,3}ep){1,2}(?<episode>(?<!\d+)\d{5}(?!\d+)))+",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes with airdate and part (2018.04.28.Part.2)
                R(@"^(?<title>.+?)?\W*(?<airyear>\d{4})[-_. ]+(?<airmonth>[0-1][0-9])[-_. ]+(?<airday>[0-3][0-9])(?![-_. ]+[0-3][0-9])[-_. ]+Part[-_. ]?(?<part>[1-9])",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes with airdate (2018.04.28)
                R(@"^(?<title>.+?)?\W*(?<airyear>\d{4})[-_. ]+(?<airmonth>[0-1][0-9])[-_. ]+(?<airday>[0-3][0-9])(?![-_. ]+[0-3][0-9])",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - Title 4-digit Absolute Episode Number [SubGroup]
                R(@"^(?<title>.+?)[-_. ]+(?<absoluteepisode>(?<!\d+)\d{4}(?!\d+))[-_. ]\[(?<subgroup>.+?)\]",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Mini-Series with year in title, treated as season 1, episodes are labelled as Part01, Part 01, Part.1
                R(@"^(?<title>.+?\d{4})(?:\W+(?:(?:Part\W?|e)(?<episode>\d{1,2}(?!\d+)))+)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // 3-digit season number
                // Single episode: Title.S001E01
                // Multi episode: Title.S001E01-E02
                R(@"^(?<title>.+?)[-_. ]+S(?<season>(?<!\d+)\d{3}(?!\d+))E(?<episode>(?<!\d+)\d{2}(?!\d+))(?:-[ex](?<episode>(?<!\d+)\d{2}(?!\d+)))?([_. ]|$)(?!\\)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Mini-Series, treated as season 1, episodes are labelled as XofY
                R(@"^(?<title>.+?)(?:\W+(?:(?<episode>(?<!\d+)\d{1,2}(?!\d+))of\d+)+)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Supports Season 01 Episode 03
                R(@"(?:.*(?:\""|^))(?<title>.*?)(?:[-_\W](?<![()\[]))+(?:\W?Season\W?)(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:\W|_)+(?:Episode\W)(?:[-_. ]?(?<episode>(?<!\d+)\d{1,2}(?!\d+)))+",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Multi-episode with episodes in square brackets (Series Title [S01E11E12] or Series Title [S01E11-12])
                R(@"(?:.*(?:^))(?<title>.*?)[-._ ]+\[S(?<season>(?<!\d+)\d{2}(?!\d+))(?:[E-]{1,2}(?<episode>(?<!\d+)\d{2}(?!\d+)))+\]",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Multi-episode with episodes in brackets (Series Title (S01E11E12) or Series Title (S01E11-12) or Series Title (S01E1-3 of 12))
                R(@"(?:.*(?:^))(?<title>.*?)[-._ ]+\(S(?<season>(?<!\d+)\d{2}(?!\d+))(?:[E-]{1,2}(?<episode>(?<!\d+)\d{1,2}(?!\d+)))+([._ ]+of[._ ]+[\dx]+)?\)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Multi-episode release with no space between series title and season (S01E11E12)
                R(@"(?:.*(?:^))(?<title>.*?)S(?<season>(?<!\d+)\d{2}(?!\d+))(?:E(?<episode>(?<!\d+)\d{2}(?!\d+)))+",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Multi-episode with single episode numbers (S6.E1-E2, S6.E1E2, S6E1E2, etc)
                R(@"^(?<title>.+?)[-_. ]S(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:[-_. ]?[ex]?(?<episode>(?<!\d+)\d{1,2}(?!\d+)))+",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Single or multi episode releases with multiple titles, each followed by season and episode numbers in brackets
                R(@"^(?<title>.*?)[ ._]\(S(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:\W|_)?E?[ ._]?(?<episode>(?<!\d+)\d{1,2}(?!\d+))(?:-(?<episode>(?<!\d+)\d{1,2}(?!\d+)))?\)(?:[ ._]\/[ ._])(?<title>.*?)[ ._]\(",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Multi-episode with episodes in brackets (Series Title (S01E11E12) or Series Title (S1E11-12) or Series Title (S1E1-3 of 12))
                R(@"(?:.*(?:^))(?<title>.*?)[-._ ]+\(S(?<season>(?<!\d+)\d{1}(?!\d+))(?:[E-]{1,2}(?<episode>(?<!\d+)\d{1,2}(?!\d+)))+([._ ]+of[._ ]+[\dx]+)?\)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Single episode season or episode S1E1 or S1-E1 or S1.Ep1 or S01.Ep.01
                R(@"(?:.*(?:\""|^))(?<title>.*?)(?:\W?|_)S(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:\W|_)?Ep?[ ._]?(?<episode>(?<!\d+)\d{1,2}(?!\d+))",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // 3 digit season S010E05
                R(@"(?:.*(?:\""|^))(?<title>.*?)(?:\W?|_)S(?<season>(?<!\d+)\d{3}(?!\d+))(?:\W|_)?E(?<episode>(?<!\d+)\d{1,2}(?!\d+))",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Separated season and episode numbers S01 - E01
                R(@"^(?<title>.+?)(?:_|-|\s|\.)+S(?<season>\d{2}(?!\d+))(\W-\W)E(?<episode>(?<!\d+)\d{2}(?!\d+))(?!\\)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Season and episode numbers in square brackets (single and multi-episode)
                // Series Title - [02x01] - Episode 1
                // Series Title - [02x01x02] - Episode 1
                R(@"^(?<title>.+?)?(?:[-_\W](?<![()\[!]))+\[(?:s)?(?<season>(?<!\d+)\d{1,2})(?:(?:[ex])(?<episode>\d{2}))(?:(?:[-ex]){1,2}(?<episode>\d{2}))*\].+?(?:\.|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - Title with season number - Absolute Episode Number (Title S01 - EP14)
                R(@"^(?<title>.+?S\d{1,2})[-_. ]{3,}(?:EP)?(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+|[-]))",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - French titles with single episode numbers, with or without leading sub group ([RlsGroup] Title - Episode 1)
                R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)?(?<title>.+?)[-_. ]+?(?:Episode[-_. ]+?)(?<absoluteepisode>\d{1}(\.\d{1,2})?(?!\d+))",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - Absolute episode number in square brackets
                R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)(?<title>.+?)[-_. ]+?\[(?<absoluteepisode>\d{2,3}(\.\d{1,2})?(?!\d+))\]",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Japanese variety shows with leading date
                R(@"^(?<airyear>\d{2})(?<airmonth>[0-1][0-9])(?<airday>[0-3][0-9])(?![-_. ]+[0-3][0-9])[-_. ](?<title>.+?)[-_. ](?:Season[-_. ]?(?<season>\d{1,2})[-_. ])?(?:ep|#)(?<episode>\d{2,3})",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Season only releases followed by year
                R(@"^(?<title>.+?)[-_. ]+?(?:S|Season|Saison|Series|Stagione)[-_. ]?(?<season>\d{1,2}(?=[-_. ]\d{4}[-_. ]+))(?<extras>EXTRAS|SUBPACK)?(?!\\)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Season only releases
                R(@"^(?<title>.+?)[-_. ]+?(?:S|Season|Saison|Series|Stagione)[-_. ]?(?<season>\d{1,2}(?![-_. ]?\d+))(?:[-_. ]|$)+(?<extras>EXTRAS|SUBPACK)?(?!\\)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // 4 digit season only releases
                R(@"^(?<title>.+?)[-_. ]+?(?:S|Season|Saison|Series|Stagione)[-_. ]?(?<season>\d{4}(?![-_. ]?\d+))(\W+|_|$)(?<extras>EXTRAS|SUBPACK)?(?!\\)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Spanish tracker releases
                R(@"^(?<title>.+?)(?:(?:[-_. ]+?Temporada.+?|\[.+?\])\[Cap)(?:[-_. ]+(?<season>(?<!\d+)\d{1,2})(?<episode>(?<!e|x)(?:[1-9][0-9]|[0][1-9])))+(?:\])",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Supports 103/113 naming
                R(@"^(?<title>.+?)?(?:(?:[_.-](?<![()\[!]))+(?<season>(?<!\d+)[1-9])(?<episode>[1-9][0-9]|[0][1-9])(?![a-z]|\d+))+(?:[_.]|$)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // 4 digit episode number
                // Episodes without a title, Single (S01E05, 1x05) AND Multi (S01E04E05, 1x04x05, etc)
                R(@"^(?:S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:\-|[ex]|\W[ex]|_){1,2}(?<episode>\d{4}(?!\d+|i|p)))+)(\W+|_|$)(?!\\)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // 4 digit episode number
                // Episodes with a title, Single episodes (S01E05, 1x05, etc) & Multi-episode (S01E05E06, S01E05-06, S01E05 E06, etc)
                R(@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]|\d{1,2}-))+S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:\-|[ex]|\W[ex]|_){1,2}(?<episode>\d{4}(?!\d+|i|p)))+)\W?(?!\\)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Turkish tracker releases (01 BLM, 3. Blm, 04.Bolum, etc)
                R(@"^(?<title>.+?)[_. ](?<absoluteepisode>\d{1,4})(?:[_. ]+)(?:BLM|B[oö]l[uü]m)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes with airdate (04.28.2018)
                R(@"^(?<title>.+?)?\W*(?<ambiguousairmonth>[0-1][0-9])[-_. ]+(?<ambiguousairday>[0-3][0-9])[-_. ]+(?<airyear>\d{4})(?!\d+)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes with airdate (28.04.2018)
                R(@"^(?<title>.+?)?\W*(?<ambiguousairday>[0-3][0-9])[-_. ]+(?<ambiguousairmonth>[0-1][0-9])[-_. ]+(?<airyear>\d{4})(?!\d+)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes with airdate (20180428)
                R(@"^(?<title>.+?)?\W*(?<!\d+)(?<airyear>\d{4})(?<airmonth>[0-1][0-9])(?<airday>[0-3][0-9])(?!\d+)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - Title Absolute Episode Number (E195 or E1206)
                R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)?(?<title>.+?)(?:(?:_|-|\s|\.)+(?:e|ep)(?<absoluteepisode>(\d{3}|\d{4})(\.\d{1,2})?))+[-_. ].*?(?<hash>[(\[][0-9A-F]{8}[)\]])?$",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Daily episodes that use short month format instead of number
                R(@"^(?<title>.+?)[-_. ]+(?<airday>[1-2]\d|3[01]|[1-9])(?:th|st|rd)[-_. ](?<shortairmonth>jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[-_. ](?<airyear>19[4-9]\d|20\d\d)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Supports 1103/1113 naming
                R(@"^(?<title>.+?)?(?:(?:[-_. ](?<![()\[!]))*(?<!\d{1,2}-)(?<season>(?<!\d+|\(|\[|e|x)\d{2})(?<episode>(?<!e|x)(?:[1-9][0-9]|[0][1-9])(?!p|i|\d+|\)|\]|\W\d+|\W(?:e|ep|x)\d+)))+([-_. ]+|$)(?!\\)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Dutch/Flemish release titles
                R(@"^(?<title>.+?)[-_. ](?:Se\.(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:(?:[-_ ]?afl\.)(?<episode>\d{1,3}(?!\d+))(?:(?:[-]|[-_ ]en[-_ ])(?<episode>\d{1,3}(?!\d+)))*))",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes with single digit episode number (S01E1, S01E5E6, etc)
                R(@"^(?<title>.*?)(?:(?:[-_\W](?<![()\[!]))+S?(?<season>(?<!\d+)\d{1,2}(?!\d+))(?:(?:\-|[ex]){1,2}(?<episode>\d{1}))+)+(\W+|_|$)(?!\\)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // iTunes Season 1\05 Title (Quality).ext
                R(@"^(?:Season(?:_|-|\s|\.)(?<season>(?<!\d+)\d{1,2}(?!\d+)))(?:_|-|\s|\.)(?<episode>(?<!\d+)\d{1,2}(?!\d+))",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // iTunes 1-05 Title (Quality).ext
                R(@"^(?:(?<season>(?<!\d+)(?:\d{1,2})(?!\d+))(?:-(?<episode>\d{2,3}(?!\d+))))",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Mini-Series, treated as season 1, episodes are labelled as Part01, Part 01, Part.1
                R(@"^(?<title>.+?)(?:\W+(?:(?:(?<!\()Part\W?|(?<!\d+\W+)e)(?<episode>\d{1,2}(?!\d+|\))))+)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Mini-Series, treated as season 1, episodes are labelled as Part One/Two/Three/...Nine, Part.One, Part_One
                R(@"^(?<title>.+?)(?:\W+(?:Part[-._ ](?<episode>One|Two|Three|Four|Five|Six|Seven|Eight|Nine)(?>[-._ ])))",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime Range - Title Absolute Episode Number (ep01-12)
                R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)?(?<title>.+?)(?:_|\s|\.)+(?:e|ep)(?<absoluteepisode>\d{2,3}(\.\d{1,2})?)-(?<absoluteepisode>(?<!\d+)\d{1,2}(\.\d{1,2})?(?!\d+|-)).*?(?<hash>[(\[][0-9A-F]{8}[)\]])?$",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - Title Absolute Episode Number (e66)
                R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)?(?<title>.+?)(?:(?:_|-|\s|\.)+(?:e|ep)(?<absoluteepisode>\d{2,4}(\.\d{1,2})?))+[-_. ].*?(?<hash>[(\[][0-9A-F]{8}[)\]])?$",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - Title Episode Absolute Episode Number (Series Title Episode 01)
                R(@"^(?<title>.+?)[-_. ](?:Episode)(?:[-_. ]+(?<absoluteepisode>(?<!\d+)\d{2,3}(\.\d{1,2})?(?!\d+)))+.*?(?<hash>[(\[][0-9A-F]{8}[)\]])?$",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime Range - Title Absolute Episode Number (1 or 2 digit absolute episode numbers in a range, 1-10)
                R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)?(?<title>.+?)[_. ]+(?<absoluteepisode>(?<!\d+)\d{1,2}(\.\d{1,2})?(?!\d+))-(?<absoluteepisode>(?<!\d+)\d{1,2}(\.\d{1,2})?(?!\d+|-)).*?(?<hash>[(\[][0-9A-F]{8}[)\]])?$",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - Title Episode/Episodio Absolute Episode Number
                R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)?(?<title>.+?)[-_. ]+(?:Episode|Episodio)(?:[-_. ]+(?<absoluteepisode>(?<!\d+)\d{2,4}(\.\d{1,2})?(?!\d+|[ip])))+.*?(?<hash>[(\[][0-9A-F]{8}[)\]])?$",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - Title [Absolute Episode Number] from AniLibriaTV
                R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)?(?<title>.+?)(?:[-_. ]\[)(?:(?:-?)(?<absoluteepisode>(?<!\d+)\d{2,3}(\.\d{1,2})?(?!\d+|[ip])))+(?:\][-_. ]).*?(?<hash>[(\[][0-9A-F]{8}[)\]])?$",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Simple season releases in the format 'Series (year) - Temporada season'
                R(@"^(?<title>.+?)[_. ]-[_. ](?:Temporada)[-_. ]?(?<season>\d{1,2}(?![_. ]?\d+))(?:[_. ]|$)+(?<extras>EXTRAS|SUBPACK)?(?!\\)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - Title Absolute Episode Number
                R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)?(?<title>.+?)(?:[-_. ]+(?<absoluteepisode>(?<!\d+)\d{2,4}(\.\d{1,2})?(?!\d+|[ip])))+.*?(?<hash>[(\[][0-9A-F]{8}[)\]])?$",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime - Title {Absolute Episode Number}
                R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)?(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+(?<absoluteepisode>(?<!\d+)\d{2,3}(\.\d{1,2})?(?!\d+|[ip])))+.*?(?<hash>[(\[][0-9A-F]{8}[)\]])?$",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Extant, terrible multi-episode naming (extant.10708.hdtv-lol.mp4)
                R(@"^(?<title>.+?)[-_. ](?<season>[0]?\d?)(?:(?<episode>\d{2}){2}(?!\d+))[-_. ]",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes without a title, Single episode numbers (S1E1, 1x1)
                R(@"^(?:S?(?<season>(?<!\d+)(?:\d{1,2}|\d{4})(?!\d+))(?:(?:[-_ ]?[ex])(?<episode>\d{1}(?!\d+))))",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Anime OVA special
                R(@"^\[(?<subgroup>.+?)\][-_. ]?(?<title>.+?)(?:[-_. ]+(?<special>special|ova|ovd|ncop|nced)).*?(?<hash>[(\[][0-9A-F]{8}[)\]])?(?:$|\.mkv)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),

                // Episodes with a title, 3 digit season number, Single episodes (S001E05, 1x05, etc) & Multi-episode (S001E05E06, S001E05-06, S001E05 E06, etc)
                R(@"^(?<title>.+?)(?:(?:[-_\W](?<![()\[!]))+S?(?<season>(?<!\d+)(?:\d{3})(?!\d+))(?:[ex]|\W[ex]){1,2}(?<episode>\d{2,3}(?!\d+))(?:(?:\-|[ex]|\W[ex]|_){1,2}(?<episode>\d{2,3}(?!\d+)))*)(?:[-_. ]|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled),
            };

        private static readonly Regex[] SpecialEpisodeTitleRegex = new Regex[]
            {
                R(@"\.S\d+E00\.(?<episodetitle>.+?)(?:\.(?:720p|1080p|2160p|HDTV|WEB|WEBRip|WEB-DL)\.|$)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

                R(@"\.S\d+\.Special\.(?<episodetitle>.+?)(?:\.(?:720p|1080p|2160p|HDTV|WEB|WEBRip|WEB-DL)\.|$)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled)
            };

        private static readonly Regex[] RejectHashedReleasesRegexes = new Regex[]
            {
                // Generic match for md5 and mixed-case hashes.
                R(@"^[0-9a-zA-Z]{32}", RegexOptions.Compiled),

                // Generic match for shorter lower-case hashes.
                R(@"^[a-z0-9]{24}$", RegexOptions.Compiled),

                // Format seen on some NZBGeek releases
                // Be very strict with these coz they are very close to the valid 101 ep numbering.
                R(@"^[A-Z]{11}\d{3}$", RegexOptions.Compiled),
                R(@"^[a-z]{12}\d{3}$", RegexOptions.Compiled),

                // Backup filename (Unknown origins)
                R(@"^Backup_\d{5,}S\d{2}-\d{2}$", RegexOptions.Compiled),

                // 123 - Started appearing December 2014
                R(@"^123$", RegexOptions.Compiled),

                // abc - Started appearing January 2015
                R(@"^abc$", RegexOptions.Compiled | RegexOptions.IgnoreCase),

                // abc - Started appearing 2020
                R(@"^abc[-_. ]xyz", RegexOptions.Compiled | RegexOptions.IgnoreCase),

                // b00bs - Started appearing January 2015
                R(@"^b00bs$", RegexOptions.Compiled | RegexOptions.IgnoreCase),

                // 170424_26 - Started appearing August 2018
                R(@"^\d{6}_\d{2}$"),

                // additional Generic match for mixed-case hashes. - Started appearing Dec 2020
                R(@"^[0-9a-zA-Z]{30}", RegexOptions.Compiled),

                // additional Generic match for mixed-case hashes. - Started appearing Jan 2021
                R(@"^[0-9a-zA-Z]{26}", RegexOptions.Compiled),

                // additional Generic match for mixed-case hashes. - Started appearing Jan 2021
                R(@"^[0-9a-zA-Z]{39}", RegexOptions.Compiled),

                // additional Generic match for mixed-case hashes. - Started appearing Jan 2021
                R(@"^[0-9a-zA-Z]{24}", RegexOptions.Compiled),
            };

        private static readonly Regex[] SeasonFolderRegexes = new Regex[]
            {
                R(@"^(Season[ ._-]*\d+|Specials)$", RegexOptions.Compiled)
            };

        // Regex to detect whether the title was reversed.
        private static readonly Regex ReversedTitleRegex = R(@"(?:^|[-._ ])(p027|p0801|\d{2,3}E-?\d{2}S)[-._ ]", RegexOptions.Compiled);

        private static readonly Regex AnimeSeasonReleaseMetadataRegex = R(@"\[[^\[\]]+\]|\([^()]+\)|\{[^{}]+\}", RegexOptions.Compiled);

        private static readonly Regex AnimeSpecialMarkerRegex = R(@"\b(special|ova|ovd|ncop|nced)\b",
                                                                          RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly RegexReplace NormalizeRegex = new RegexReplace(@"((?:\b|_)(?<!^)([aà](?!$)|an|the|and|or|of)(?!$)(?:\b|_))|\W|_",
                                                                string.Empty,
                                                                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PercentRegex = R(@"(?<=\b\d+)%", RegexOptions.Compiled);

        private static readonly RegexReplace SimpleTitleRegex = new RegexReplace(@"(?:(480|540|576|720|1080|1440|2160)[ip]|[xh][\W_]?26[456]|DD\W?5\W1|[<>?*]|848x480|1280x720|1920x1080|3840x2160|4096x2160|(?<![a-f0-9])(8|10)[ -]?(b(?![a-z0-9])|bit))\s*?",
                                                                string.Empty,
                                                                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Valid TLDs http://data.iana.org/TLD/tlds-alpha-by-domain.txt

        private static readonly Regex SixDigitAirDateRegex = R(@"(?<=[_.-])(?<airdate>(?<!\d)(?<airyear>[1-9]\d{1})(?<airmonth>[0-1][0-9])(?<airday>[0-3][0-9]))(?=[_.-])",
                                                                        RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CleanQualityBracketsRegex = R(@"\[[a-z0-9 ._-]+\]$",
                                                                   RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex YearInTitleRegex = R(@"^(?<title>.+?)[-_. ]+?[\(\[]?(?<year>\d{4})[\]\)]?",
                                                                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex TitleComponentsRegex = R(@"^(?:(?<title>.+?) \((?<title>.+?)\)|(?<title>.+?) \| (?<title>.+?)|(?<title>.+?) AKA (?<title>.+?)|(?<title>.+?) / (?<title>.+?))$",
                                                                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PartRegex = R(@"\(\d+\)$", RegexOptions.Compiled);
        private static readonly Regex PunctuationRegex = R(@"[^\w\s]", RegexOptions.Compiled);
        private static readonly Regex ArticleWordRegex = R(@"^(a|an|the)\s", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SpecialEpisodeWordRegex = R(@"\b(part|special|edition|christmas)\b\s?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DuplicateSpacesRegex = R(@"\s{2,}", RegexOptions.Compiled);
        private static readonly Regex SeasonFolderRegex = R(@"^(?:S|Season|Saison|Series|Stagione|Sezon)[-_. ]*(?<season>(?<!\d+)\d{1,4}(?!\d+))(?:[_. ]+(?!\d+)|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SimpleEpisodeNumberRegex = R(@"^[ex]?(?<episode>(?<!\d+)\d{1,3}(?!\d+))(?:[ex-](?<episode>(?<!\d+)\d{1,3}(?!\d+)))?(?:[_. ](?!\d+)(?<remaining>.+)|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RequestInfoRegex = R(@"^(?:\[.+?\])+", RegexOptions.Compiled);

        private static readonly string[] Numbers = new[] { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };

        private static readonly Regex MultiRegex = new(@"[-_. \[](?<multi>multi|multilang|multilanguage)[-_. \]]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // MOVIES parser (ported per D7). Movie-only fields; SERIES uses its own sets above.
        private static readonly Regex EditionRegex = R(@"\(?\b(?<edition>(((Recut.|Extended.|Ultimate.)?(Director.?s|Collector.?s|Theatrical|Ultimate|Extended|Despecialized|(Special|Rouge|Final|Assembly|Imperial|Diamond|Signature|Hunter|Rekall)(?=(.(Cut|Edition|Version)))|\d{2,3}(th)?.Anniversary)(?:.(Cut|Edition|Version))?(.(Extended|Uncensored|Remastered|Unrated|Uncut|Open.?Matte|IMAX|Fan.?Edit))?|((Uncensored|Remastered|Unrated|Uncut|Open?.Matte|IMAX|Fan.?Edit|Restored|((2|3|4)in1))))))\b\)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ReportEditionRegex = R(@"^.+?" + EditionRegex, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex HardcodedSubsRegex = R(@"\b((?<hcsub>(\w+(?<!SOFT|MULTI|HORRIBLE)SUBS?))|(?<hc>(HC|SUBBED)))\b",
                                                        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        private static readonly Regex[] ReportMovieTitleRegex = new[]
        {
            // Anime [Subgroup] with year explicitly in parentheses, e.g. [Sub] Title 1993 (1993) [VHS]
            // This must come before the generic anime+year regex to correctly handle titles that contain a year as part of the name
            R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)(?<title>(?![(\[]).+?)\s*\((?<year>(1(8|9)|20)\d{2})\).*?(?<hash>\[\w{8}\])?(?:$|\.)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // Anime [Subgroup] and Year
            R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)(?<title>(?![(\[]).+?)?(?:(?:[-_\W](?<![)\[!]))*(?<year>(1(8|9)|20)\d{2}(?!p|i|x|\d+|\]|\W\d+)))+.*?(?<hash>\[\w{8}\])?(?:$|\.)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // Anime [Subgroup] no year, versioned title, hash
            R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)(?<title>(?![(\[]).+?)((v)(?:\d{1,2})(?:([-_. ])))(\[.*)?(?:[\[(][^])])?.*?(?<hash>\[\w{8}\])(?:$|\.)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // Anime [Subgroup] no year, info in double sets of brackets, hash
            R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)(?<title>(?![(\[]).+?)(\[.*).*?(?<hash>\[\w{8}\])(?:$|\.)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // Anime [Subgroup] no year, info in parentheses or brackets, hash
            R(@"^(?:\[(?<subgroup>.+?)\][-_. ]?)(?<title>(?![(\[]).+)(?:[\[(][^])]).*?(?<hash>\[\w{8}\])(?:$|\.)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // Some german or french tracker formats (missing year, ...) (Only applies to german and TrueFrench releases)
            R(@"^(?<title>(?![(\[]).+?)((\W|_))(" + EditionRegex + @".{1,3})?(?:(?<!(19|20)\d{2}.*?)(?<!(?:Good|The)[_ .-])(German|TrueFrench))(.+?)(?=((19|20)\d{2}|$))(?<year>(19|20)\d{2}(?!p|i|\d+|\]|\W\d+))?(\W+|_|$)(?!\\)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // Special, Despecialized, etc. Edition Movies, e.g: Mission.Impossible.3.Special.Edition.2011
            R(@"^(?<title>(?![(\[]).+?)?(?:(?:[-_\W](?<![)\[!]))*" + EditionRegex + @".{1,3}(?<year>(1(8|9)|20)\d{2}(?!p|i|\d+|\]|\W\d+)))+(\W+|_|$)(?!\\)",
                          RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // Normal movie format, e.g: Mission.Impossible.3.2011
            R(@"^(?<title>(?![(\[]).+?)?(?:(?:[-_\W](?<![)\[!]))*(?<year>(1(8|9)|20)\d{2}(?!p|i|(1(8|9)|20)\d{2}|\]|\W(1(8|9)|20)\d{2})))+(\W+|_|$)(?!\\)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // PassThePopcorn Torrent names: Star.Wars[PassThePopcorn]
            R(@"^(?<title>.+?)?(?:(?:[-_\W](?<![()\[!]))*(?<year>(\[\w *\])))+(\W+|_|$)(?!\\)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // That did not work? Maybe some tool uses [] for years. Who would do that?
            R(@"^(?<title>(?![(\[]).+?)?(?:(?:[-_\W](?<![)!]))*(?<year>(1(8|9)|20)\d{2}(?!p|i|\d+|\W\d+)))+(\W+|_|$)(?!\\)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

            // As a last resort for movies that have ( or [ in their title.
            R(@"^(?<title>.+?)?(?:(?:[-_\W](?<![)\[!]))*(?<year>(1(8|9)|20)\d{2}(?!p|i|\d+|\]|\W\d+)))+(\W+|_|$)(?!\\)", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };

        private static readonly Regex[] ReportMovieTitleFolderRegex = new[]
        {
            // When year comes first.
            R(@"^(?:(?:[-_\W](?<![)!]))*(?<year>(19|20)\d{2}(?!p|i|\d+|\W\d+)))+(\W+|_|$)(?<title>.+?)?$")
        };

        // Regex to split movie titles that contain `AKA`.
        private static readonly Regex AlternativeTitleRegex = R(@"[ ]+(?:AKA|\/)[ ]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Regex to unbracket alternative titles.
        private static readonly Regex BracketedAlternativeTitleRegex = R(@"(.*) \([ ]*AKA[ ]+(.*)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex NormalizeAlternativeTitleRegex = R(@"[ ]+(?:A\.K\.A\.)[ ]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex MovieNormalizeRegex = R(@"((?:\b|_)(?<!^|[^a-zA-Z0-9_']\w[^a-zA-Z0-9_'])([aà](?!$|[^a-zA-Z0-9_']\w[^a-zA-Z0-9_'])|an|the|and|or|of)(?!$)(?:\b|_))|\W|_",
                                                                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ReportImdbId = R(@"(?<imdbid>tt\d{7,8})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ReportTmdbId = R(@"tmdb(id)?-(?<tmdbid>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SimpleReleaseTitleRegex = R(@"\s*(?:[<>?*|])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Dictionary<string, int> ShortMonths = new()
        {
            { "jan", 1 },
            { "feb", 2 },
            { "mar", 3 },
            { "apr", 4 },
            { "may", 5 },
            { "jun", 6 },
            { "jul", 7 },
            { "aug", 8 },
            { "sep", 9 },
            { "oct", 10 },
            { "nov", 11 },
            { "dec", 12 },
        };

        public static ParsedEpisodeInfo ParsePath(string path)
        {
            var fileInfo = new FileInfo(path);
            var fileName = fileInfo.Name;

            foreach (var replace in ParserCommon.PathPreSubstitutionRegex)
            {
                if (replace.TryReplace(ref fileName))
                {
                    Logger.Trace($"Replace regex: {replace}");
                    Logger.Debug("Substituted with " + fileName);
                }
            }

            var result = ParseTitle(fileName);

            // Parse using the folder and file separately, but combine if they both parse correctly.
            var episodeNumberMatch = SimpleEpisodeNumberRegex.Match(fileName);

            if (episodeNumberMatch.Success && fileInfo.Directory?.Name != null && (result == null || result.IsMiniSeries || result.AbsoluteEpisodeNumbers.Any()))
            {
                var seasonMatch = SeasonFolderRegex.Match(fileInfo.Directory.Name);

                if (seasonMatch.Success && seasonMatch.Groups["season"].Success)
                {
                    var episodeCaptures = episodeNumberMatch.Groups["episode"].Captures.Cast<Capture>().ToList();
                    var first = ParseNumber(episodeCaptures.First().Value);
                    var last = ParseNumber(episodeCaptures.Last().Value);
                    var pathTitle = $"S{seasonMatch.Groups["season"].Value}E{first:00}";

                    if (first != last)
                    {
                        pathTitle += $"-E{last:00}";
                    }

                    if (episodeNumberMatch.Groups["remaining"].Success)
                    {
                        pathTitle += $" {episodeNumberMatch.Groups["remaining"].Value}";
                    }

                    var parsedFileInfo = ParseTitle(pathTitle);

                    Logger.Debug("Episode parsed from file and folder names. {0}", parsedFileInfo);

                    return parsedFileInfo;
                }
            }

            if (result == null && int.TryParse(Path.GetFileNameWithoutExtension(fileName), out var number))
            {
                Logger.Debug("Attempting to parse episode info using directory and file names. {0}", fileInfo.Directory.Name);
                result = ParseTitle(fileInfo.Directory.Name);

                if (result != null && result.AbsoluteEpisodeNumbers.Contains(number))
                {
                    result.AbsoluteEpisodeNumbers = new[] { number };
                }
                else if (result != null && result.EpisodeNumbers.Contains(number))
                {
                    result.EpisodeNumbers = new[] { number };
                }
                else
                {
                    result = null;
                }
            }

            // TODO: Skip for anime specials

            if (result == null)
            {
                Logger.Debug("Attempting to parse episode info using combined directory and file names. {0}", fileInfo.Directory.Name);
                result = ParseTitle(fileInfo.Directory.Name + " " + fileName);
            }

            if (result == null)
            {
                Logger.Debug("Attempting to parse episode info using directory name. {0}", fileInfo.Directory.Name);
                result = ParseTitle(fileInfo.Directory.Name + fileInfo.Extension);
            }

            return result;
        }

        public static string SimplifyTitle(string title)
        {
            if (!ValidateBeforeParsing(title))
            {
                return title;
            }

            Logger.Debug("Parsing string '{0}'", title);

            if (ReversedTitleRegex.IsMatch(title))
            {
                var titleWithoutExtension = FileExtensions.RemoveFileExtension(title).ToCharArray();
                Array.Reverse(titleWithoutExtension);

                title = string.Concat(new string(titleWithoutExtension), title.AsSpan(titleWithoutExtension.Length));

                Logger.Debug("Reversed name detected. Converted to '{0}'", title);
            }

            var simpleTitle = title;

            simpleTitle = ParserCommon.WebsitePrefixRegex.Replace(simpleTitle);
            simpleTitle = ParserCommon.WebsitePostfixRegex.Replace(simpleTitle);
            simpleTitle = ParserCommon.CleanTorrentSuffixRegex.Replace(simpleTitle);

            return simpleTitle;
        }

        public static ParsedEpisodeInfo ParseTitle(string title)
        {
            try
            {
                if (!ValidateBeforeParsing(title))
                {
                    return null;
                }

                Logger.Debug("Parsing string '{0}'", title);

                if (ReversedTitleRegex.IsMatch(title))
                {
                    var titleWithoutExtension = FileExtensions.RemoveFileExtension(title).ToCharArray();
                    Array.Reverse(titleWithoutExtension);

                    title = string.Concat(new string(titleWithoutExtension), title.AsSpan(titleWithoutExtension.Length));

                    Logger.Debug("Reversed name detected. Converted to '{0}'", title);
                }

                var releaseTitle = FileExtensions.RemoveFileExtension(title);

                releaseTitle = releaseTitle.Replace("【", "[").Replace("】", "]");

                foreach (var replace in ParserCommon.PreSubstitutionRegex)
                {
                    if (replace.TryReplace(ref releaseTitle))
                    {
                        Logger.Trace($"Replace regex: {replace}");
                        Logger.Debug("Substituted with " + releaseTitle);
                    }
                }

                var simpleTitle = SimpleTitleRegex.Replace(releaseTitle);

                // TODO: Quick fix stripping [url] - prefixes and postfixes.
                simpleTitle = ParserCommon.WebsitePrefixRegex.Replace(simpleTitle);
                simpleTitle = ParserCommon.WebsitePostfixRegex.Replace(simpleTitle);
                simpleTitle = ParserCommon.CleanTorrentSuffixRegex.Replace(simpleTitle);

                simpleTitle = CleanQualityBracketsRegex.Replace(simpleTitle, m =>
                {
                    if (QualityParser.ParseQualityName(m.Value).Quality != Qualities.Quality.Unknown)
                    {
                        return string.Empty;
                    }

                    return m.Value;
                });

                var sixDigitAirDateMatch = SixDigitAirDateRegex.Match(simpleTitle);
                if (sixDigitAirDateMatch.Success)
                {
                    var airYear = sixDigitAirDateMatch.Groups["airyear"].Value;
                    var airMonth = sixDigitAirDateMatch.Groups["airmonth"].Value;
                    var airDay = sixDigitAirDateMatch.Groups["airday"].Value;

                    if (airMonth != "00" || airDay != "00")
                    {
                        var fixedDate = string.Format("20{0}.{1}.{2}", airYear, airMonth, airDay);

                        simpleTitle = simpleTitle.Replace(sixDigitAirDateMatch.Groups["airdate"].Value, fixedDate);
                    }
                }

                foreach (var regex in ReportTitleRegex)
                {
                    var match = regex.Matches(simpleTitle);

                    if (match.Count != 0)
                    {
                        Logger.Trace(regex);
                        try
                        {
                            var result = ParseMatchCollection(match, releaseTitle);

                            if (result != null)
                            {
                                if (result.FullSeason && result.ReleaseTokens.ContainsIgnoreCase("Special"))
                                {
                                    result.FullSeason = false;
                                    result.Special = true;
                                }

                                result.Languages = LanguageParser.ParseLanguages(result.ReleaseTokens);
                                Logger.Debug("Languages parsed: {0}", string.Join(", ", result.Languages));

                                result.Quality = QualityParser.ParseQuality(title);
                                Logger.Debug("Quality parsed: {0}", result.Quality);

                                result.ReleaseGroup = ReleaseGroupParser.ParseReleaseGroup(releaseTitle);

                                var subGroup = GetSubGroup(match);
                                if (!subGroup.IsNullOrWhiteSpace())
                                {
                                    result.ReleaseGroup = subGroup;
                                }

                                Logger.Debug("Release Group parsed: {0}", result.ReleaseGroup);

                                result.ReleaseHash = GetReleaseHash(match);
                                if (!result.ReleaseHash.IsNullOrWhiteSpace())
                                {
                                    Logger.Debug("Release Hash parsed: {0}", result.ReleaseHash);
                                }

                                return result;
                            }
                        }
                        catch (InvalidDateException ex)
                        {
                            Logger.Debug(ex, ex.Message);
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (!title.ToLowerInvariant().Contains("password") && !title.ToLowerInvariant().Contains("yenc"))
                {
                    Logger.Error(e, "An error has occurred while trying to parse {0}", title);
                }
            }

            Logger.Debug("Unable to parse {0}", title);
            return null;
        }

        public static ParsedEpisodeInfo ParseSeasonTitle(string title)
        {
            var releaseTitle = FileExtensions.RemoveFileExtension(title);

            if (AnimeSpecialMarkerRegex.IsMatch(releaseTitle))
            {
                return null;
            }

            // Anime season packs are commonly named after the season's own title with no season or
            // episode numbers. ParsingService will determine the correct series and season number
            // from scene mappings.
            var seriesTitle = AnimeSeasonReleaseMetadataRegex.Replace(releaseTitle, " ");
            seriesTitle = DuplicateSpacesRegex.Replace(seriesTitle, " ");
            seriesTitle = seriesTitle.Trim();

            // If any brackets remain we were unable to extract the season title correctly.
            if (seriesTitle.IsNullOrWhiteSpace() || seriesTitle.IndexOfAny(['[', ']', '(', ')', '{', '}']) >= 0)
            {
                return null;
            }

            var result = new ParsedEpisodeInfo
            {
                ReleaseTitle = releaseTitle,
                SeriesTitle = seriesTitle,
                SeriesTitleInfo = new SeriesTitleInfo
                {
                    Title = seriesTitle,
                    TitleWithoutYear = seriesTitle
                },
                SeasonNumber = null,
                EpisodeNumbers = [],
                AbsoluteEpisodeNumbers = [],
                FullSeason = true,
                IsSeasonTitle = true,
                Languages = LanguageParser.ParseLanguages(releaseTitle),
                Quality = QualityParser.ParseQuality(title),
                ReleaseGroup = ReleaseGroupParser.ParseReleaseGroup(releaseTitle)
            };

            Logger.Debug("Title only parse. {0}", result);

            return result;
        }

        public static string ParseSeriesName(string title)
        {
            Logger.Debug("Parsing string '{0}'", title);

            var parseResult = ParseTitle(title);

            if (parseResult == null)
            {
                return CleanSeriesTitle(title);
            }

            return parseResult.SeriesTitle;
        }

        public static string CleanSeriesTitle(this string title)
        {
            if (title.IsNullOrWhiteSpace())
            {
                return title;
            }

            // If Title only contains numbers return it as is.
            if (long.TryParse(title, out _))
            {
                return title;
            }

            // Replace `%` with `percent` to deal with the 3% case
            title = PercentRegex.Replace(title, "percent");

            return NormalizeRegex.Replace(title).ToLowerInvariant().RemoveDiacritics();
        }

        public static string NormalizeEpisodeTitle(string title)
        {
            var match = SpecialEpisodeTitleRegex
                        .Select(v => v.Match(title))
                        .FirstOrDefault(v => v.Success);

            if (match != null)
            {
                title = match.Groups["episodetitle"].Value;
            }

            // Disabled, Until we run into specific testcases for the removal of these words.
            // title = SpecialEpisodeWordRegex.Replace(title, string.Empty);

            title = PartRegex.Replace(title, "");
            title = PunctuationRegex.Replace(title, " ");
            title = DuplicateSpacesRegex.Replace(title, " ");

            return title.Trim().ToLowerInvariant();
        }

        public static string NormalizeTitle(string title)
        {
            title = PunctuationRegex.Replace(title, string.Empty);
            title = ArticleWordRegex.Replace(title, string.Empty);
            title = DuplicateSpacesRegex.Replace(title, " ");

            return title.Trim().ToLowerInvariant();
        }

        public static string NormalizeImdbId(string imdbId)
        {
            var imdbRegex = R(@"^(\d{1,10}|(tt)\d{1,10})$");

            if (!imdbRegex.IsMatch(imdbId))
            {
                return null;
            }

            if (imdbId.Length > 2)
            {
                imdbId = imdbId.Replace("tt", "").PadLeft(7, '0');
                return $"tt{imdbId}";
            }

            return null;
        }

        public static string RemoveFileExtension(string title)
        {
            return FileExtensions.RemoveFileExtension(title);
        }

        public static bool HasMultipleLanguages(string title)
        {
            return MultiRegex.IsMatch(title);
        }

        private static SeriesTitleInfo GetSeriesTitleInfo(string title, MatchCollection matchCollection)
        {
            var seriesTitleInfo = new SeriesTitleInfo();
            seriesTitleInfo.Title = title;

            var match = YearInTitleRegex.Match(title);

            if (!match.Success)
            {
                seriesTitleInfo.TitleWithoutYear = title;
            }
            else
            {
                seriesTitleInfo.TitleWithoutYear = match.Groups["title"].Value;
                seriesTitleInfo.Year = Convert.ToInt32(match.Groups["year"].Value);
            }

            var matchComponents = TitleComponentsRegex.Match(seriesTitleInfo.TitleWithoutYear);

            if (matchComponents.Success)
            {
                seriesTitleInfo.AllTitles = matchComponents.Groups["title"].Captures.OfType<Capture>().Select(v => v.Value).ToArray();
            }
            else if (matchCollection[0].Groups["title"].Captures.Count > 1)
            {
                seriesTitleInfo.AllTitles = matchCollection[0].Groups["title"].Captures.Select(c => c.Value.Replace('.', ' ').Replace('_', ' ')).ToArray();
            }

            return seriesTitleInfo;
        }

        private static ParsedEpisodeInfo ParseMatchCollection(MatchCollection matchCollection, string releaseTitle)
        {
            var seriesName = matchCollection[0].Groups["title"].Value.Replace('.', ' ').Replace('_', ' ');
            seriesName = RequestInfoRegex.Replace(seriesName, "").Trim(' ');

            int.TryParse(matchCollection[0].Groups["airyear"].Value, out var airYear);

            var lastSeasonEpisodeStringIndex = matchCollection[0].Groups["title"].EndIndex();

            ParsedEpisodeInfo result;

            if (airYear < 1900)
            {
                result = new ParsedEpisodeInfo
                {
                    ReleaseTitle = releaseTitle,
                    SeasonNumber = 0,
                    EpisodeNumbers = Array.Empty<int>(),
                    AbsoluteEpisodeNumbers = Array.Empty<int>()
                };

                foreach (Match matchGroup in matchCollection)
                {
                    var episodeCaptures = matchGroup.Groups["episode"].Captures.Cast<Capture>().ToList();
                    var absoluteEpisodeCaptures = matchGroup.Groups["absoluteepisode"].Captures.Cast<Capture>().ToList();

                    // Allows use to return a list of 0 episodes (We can handle that as a full season release)
                    if (episodeCaptures.Any())
                    {
                        var first = ParseNumber(episodeCaptures.First().Value);
                        var last = ParseNumber(episodeCaptures.Last().Value);

                        if (first > last)
                        {
                            return null;
                        }

                        var count = last - first + 1;
                        result.EpisodeNumbers = Enumerable.Range(first, count).ToArray();

                        lastSeasonEpisodeStringIndex = Math.Max(lastSeasonEpisodeStringIndex, episodeCaptures.Last().EndIndex());

                        if (matchGroup.Groups["special"].Success)
                        {
                            result.Special = true;
                        }

                        if (matchGroup.Groups["splitepisode"].Success)
                        {
                            result.IsSplitEpisode = true;
                        }
                    }

                    if (absoluteEpisodeCaptures.Any())
                    {
                        var first = ParseDecimal(absoluteEpisodeCaptures.First().Value);
                        var last = ParseDecimal(absoluteEpisodeCaptures.Last().Value);

                        if (first > last)
                        {
                            return null;
                        }

                        if ((first % 1) != 0 || (last % 1) != 0)
                        {
                            if (absoluteEpisodeCaptures.Count != 1)
                            {
                                return null; // Multiple matches not allowed for specials
                            }

                            result.SpecialAbsoluteEpisodeNumbers = new decimal[] { first };
                            result.Special = true;

                            lastSeasonEpisodeStringIndex = Math.Max(lastSeasonEpisodeStringIndex, absoluteEpisodeCaptures.First().EndIndex());
                        }
                        else
                        {
                            var count = last - first + 1;
                            result.AbsoluteEpisodeNumbers = Enumerable.Range((int)first, (int)count).ToArray();

                            if (matchGroup.Groups["special"].Success)
                            {
                                result.Special = true;
                            }

                            lastSeasonEpisodeStringIndex = Math.Max(lastSeasonEpisodeStringIndex, absoluteEpisodeCaptures.Last().EndIndex());
                        }
                    }

                    if (!episodeCaptures.Any() && !absoluteEpisodeCaptures.Any())
                    {
                        // Check to see if this is an "Extras" or "SUBPACK" release, if it is, set
                        // IsSeasonExtra so they can be filtered out
                        if (!matchCollection[0].Groups["extras"].Value.IsNullOrWhiteSpace())
                        {
                            result.IsSeasonExtra = true;
                        }

                        // Partial season packs will have a seasonpart group so they can be differentiated
                        // from a full season/single episode release
                        var seasonPart = matchCollection[0].Groups["seasonpart"].Value;

                        if (seasonPart.IsNotNullOrWhiteSpace())
                        {
                            result.SeasonPart = Convert.ToInt32(seasonPart);
                            result.IsPartialSeason = true;
                        }
                        else if (matchCollection[0].Groups["special"].Success)
                        {
                            result.Special = true;
                        }
                        else
                        {
                            result.FullSeason = true;
                        }
                    }

                    if (episodeCaptures.Count == 2 && matchCollection[0].Groups["episodecount"].Success && episodeCaptures.Last().Value == matchCollection[0].Groups["episodecount"].Value)
                    {
                        result.EpisodeNumbers = Array.Empty<int>();
                        result.FullSeason = true;
                    }
                }

                var seasons = new List<int>();

                foreach (Capture seasonCapture in matchCollection[0].Groups["season"].Captures)
                {
                    if (int.TryParse(seasonCapture.Value, out var parsedSeason))
                    {
                        seasons.Add(parsedSeason);

                        lastSeasonEpisodeStringIndex = Math.Max(lastSeasonEpisodeStringIndex, seasonCapture.EndIndex());
                    }
                }

                var distinctSeasons = seasons.Distinct().OrderBy(s => s).ToArray();

                if (distinctSeasons.Length is 1 or > 2)
                {
                    result.SeasonNumbers = distinctSeasons;
                }
                else if (distinctSeasons.Length == 2 && matchCollection[0].Groups["seasonrange"].Success)
                {
                    // Only expand two seasons into a range when the release actually used a range
                    // separator (the multi-season pack regex). Two seasons captured by some other
                    // regex are kept as-is so an unnamed season is never invented.
                    result.SeasonNumbers = Enumerable.Range(distinctSeasons.First(), distinctSeasons.Last() - distinctSeasons.First() + 1).ToArray();
                }
                else if (distinctSeasons.Length == 2)
                {
                    result.SeasonNumbers = distinctSeasons;
                }
                else if (!result.AbsoluteEpisodeNumbers.Any() && result.EpisodeNumbers.Any())
                {
                    // If no season was found and it's not an absolute only release it should be treated as a mini series and season 1
                    result.SeasonNumbers = [1];
                    result.IsMiniSeries = true;
                }
            }
            else
            {
                // Try to Parse as a daily show

                var airmonth = 0;
                var airday = 0;

                if (matchCollection[0].Groups["ambiguousairmonth"].Success &&
                    matchCollection[0].Groups["ambiguousairday"].Success)
                {
                    var ambiguousAirMonth = Convert.ToInt32(matchCollection[0].Groups["ambiguousairmonth"].Value);
                    var ambiguousAirDay = Convert.ToInt32(matchCollection[0].Groups["ambiguousairday"].Value);

                    if (ambiguousAirDay <= 12 && ambiguousAirMonth <= 12)
                    {
                        throw new InvalidDateException("Ambiguous Date, cannot validate month and day with {0} and {1}", ambiguousAirMonth, ambiguousAirDay);
                    }

                    airmonth = ambiguousAirMonth;
                    airday = ambiguousAirDay;
                }
                else if (matchCollection[0].Groups["shortairmonth"].Success)
                {
                    var shortMonthValue = matchCollection[0].Groups["shortairmonth"].Value;

                    if (ShortMonths.TryGetValue(shortMonthValue.ToLowerInvariant(), out var shortMonth))
                    {
                        airmonth = shortMonth;
                        airday = Convert.ToInt32(matchCollection[0].Groups["airday"].Value);
                    }
                    else
                    {
                        throw new InvalidDateException("Unable to determine air month from month: {0}", shortMonthValue);
                    }
                }
                else
                {
                    airmonth = Convert.ToInt32(matchCollection[0].Groups["airmonth"].Value);
                    airday = Convert.ToInt32(matchCollection[0].Groups["airday"].Value);
                }

                // Swap day and month if month is bigger than 12 (scene fail)
                if (airmonth > 12)
                {
                    (airday, airmonth) = (airmonth, airday);
                }

                DateTime airDate;

                try
                {
                    airDate = new DateTime(airYear, airmonth, airday);
                }
                catch (Exception)
                {
                    throw new InvalidDateException("Invalid date found: {0}-{1}-{2}", airYear, airmonth, airday);
                }

                // Check if episode is in the future (most likely a parse error)
                if (airDate > DateTime.Now.AddDays(1).Date)
                {
                    throw new InvalidDateException("Invalid date found: {0}", airDate);
                }

                // If the parsed air date is before 1970 and the title year wasn't matched (not a match for the Plex DVR format) throw an error
                if (airDate < new DateTime(1970, 1, 1) && matchCollection[0].Groups["titleyear"].Value.IsNullOrWhiteSpace())
                {
                    throw new InvalidDateException("Invalid date found: {0}", airDate);
                }

                lastSeasonEpisodeStringIndex = Math.Max(lastSeasonEpisodeStringIndex, matchCollection[0].Groups["airyear"].EndIndex());
                lastSeasonEpisodeStringIndex = Math.Max(lastSeasonEpisodeStringIndex, matchCollection[0].Groups["airmonth"].EndIndex());
                lastSeasonEpisodeStringIndex = Math.Max(lastSeasonEpisodeStringIndex, matchCollection[0].Groups["airday"].EndIndex());

                result = new ParsedEpisodeInfo
                {
                    ReleaseTitle = releaseTitle,
                    SeasonNumber = 0,
                    AirDate = airDate.ToString(Episode.AIR_DATE_FORMAT),
                };

                var partMatch = matchCollection[0].Groups["part"];

                if (partMatch.Success)
                {
                    result.DailyPart = Convert.ToInt32(partMatch.Value);
                }
            }

            if (lastSeasonEpisodeStringIndex < releaseTitle.Length)
            {
                result.ReleaseTokens = releaseTitle.Substring(lastSeasonEpisodeStringIndex);
            }
            else
            {
                result.ReleaseTokens = releaseTitle;
            }

            result.SeriesTitle = seriesName;
            result.SeriesTitleInfo = GetSeriesTitleInfo(result.SeriesTitle, matchCollection);

            Logger.Debug("Episode Parsed. {0}", result);

            return result;
        }

        // MOVIES parser methods (ported per D7). Shared helpers (ValidateBeforeParsing,
        // GetSubGroup, GetReleaseHash, NormalizeImdbId, HasMultipleLanguages) are reused.
        public static ParsedMovieInfo ParseMoviePath(string path)
        {
            var fileInfo = new FileInfo(path);

            var result = ParseMovieTitle(fileInfo.Name, true);

            if (result == null)
            {
                Logger.Debug("Attempting to parse movie info using directory and file names. {0}", fileInfo.Directory.Name);
                result = ParseMovieTitle(fileInfo.Directory.Name + " " + fileInfo.Name);
            }

            if (result == null)
            {
                Logger.Debug("Attempting to parse movie info using directory name. {0}", fileInfo.Directory.Name);
                result = ParseMovieTitle(fileInfo.Directory.Name + fileInfo.Extension);
            }

            return result;
        }

        public static ParsedMovieInfo ParseMovieTitle(string title, bool isDir = false)
        {
            var originalTitle = title;
            try
            {
                if (!ValidateBeforeParsing(title))
                {
                    return null;
                }

                Logger.Debug("Parsing string '{0}'", title);

                if (ReversedTitleRegex.IsMatch(title))
                {
                    var titleWithoutExtension = FileExtensions.RemoveFileExtension(title).ToCharArray();
                    Array.Reverse(titleWithoutExtension);

                    title = $"{titleWithoutExtension}{title.Substring(titleWithoutExtension.Length)}";

                    Logger.Debug("Reversed name detected. Converted to '{0}'", title);
                }

                var releaseTitle = FileExtensions.RemoveFileExtension(title);

                // Trim dashes from end
                releaseTitle = releaseTitle.Trim('-', '_');

                releaseTitle = releaseTitle.Replace("【", "[").Replace("】", "]");

                foreach (var replace in ParserCommon.PreSubstitutionRegex)
                {
                    if (replace.TryReplace(ref releaseTitle))
                    {
                        Logger.Trace($"Replace regex: {replace}");
                        Logger.Debug("Substituted with " + releaseTitle);
                    }
                }

                var simpleTitle = SimpleTitleRegex.Replace(releaseTitle);

                // TODO: Quick fix stripping [url] - prefixes.
                simpleTitle = ParserCommon.WebsitePrefixRegex.Replace(simpleTitle);
                simpleTitle = ParserCommon.WebsitePostfixRegex.Replace(simpleTitle);

                simpleTitle = ParserCommon.CleanTorrentSuffixRegex.Replace(simpleTitle);

                simpleTitle = CleanQualityBracketsRegex.Replace(simpleTitle, m =>
                {
                    if (QualityParser.ParseQualityName(m.Value, isMovie: true).Quality != Qualities.Quality.Unknown)
                    {
                        return string.Empty;
                    }

                    return m.Value;
                });

                var allRegexes = ReportMovieTitleRegex.ToList();

                if (isDir)
                {
                    allRegexes.AddRange(ReportMovieTitleFolderRegex);
                }

                foreach (var regex in allRegexes)
                {
                    var match = regex.Matches(simpleTitle);

                    if (match.Count != 0)
                    {
                        Logger.Trace(regex);
                        try
                        {
                            var result = ParseMovieMatchCollection(match);

                            if (result != null)
                            {
                                var simpleReleaseTitle = SimpleReleaseTitleRegex.Replace(releaseTitle, string.Empty);

                                var simpleTitleReplaceString = match[0].Groups["title"].Success ? match[0].Groups["title"].Value : result.PrimaryMovieTitle;

                                if (simpleTitleReplaceString.IsNotNullOrWhiteSpace())
                                {
                                    if (match[0].Groups["title"].Success)
                                    {
                                        simpleReleaseTitle = simpleReleaseTitle.Remove(match[0].Groups["title"].Index, match[0].Groups["title"].Length)
                                                                               .Insert(match[0].Groups["title"].Index, simpleTitleReplaceString.Contains('.') ? "A.Movie" : "A Movie");
                                    }
                                    else
                                    {
                                        simpleReleaseTitle = simpleReleaseTitle.Replace(simpleTitleReplaceString, simpleTitleReplaceString.Contains('.') ? "A.Movie" : "A Movie");
                                    }
                                }

                                result.ReleaseGroup = ReleaseGroupParser.ParseReleaseGroup(simpleReleaseTitle);

                                var subGroup = GetSubGroup(match);
                                if (!subGroup.IsNullOrWhiteSpace())
                                {
                                    result.ReleaseGroup = subGroup;
                                }

                                result.HardcodedSubs = ParseHardcodeSubs(title);

                                Logger.Debug("Release Group parsed: {0}", result.ReleaseGroup);

                                result.Languages = LanguageParser.ParseLanguages(result.ReleaseGroup.IsNotNullOrWhiteSpace() ? simpleReleaseTitle.Replace(result.ReleaseGroup, "RlsGrp") : simpleReleaseTitle);
                                Logger.Debug("Languages parsed: {0}", string.Join(", ", result.Languages));

                                result.Quality = QualityParser.ParseQuality(title, isMovie: true);
                                Logger.Debug("Quality parsed: {0}", result.Quality);

                                if (result.Edition.IsNullOrWhiteSpace())
                                {
                                    result.Edition = ParseEdition(simpleReleaseTitle);
                                    Logger.Debug("Edition parsed: {0}", result.Edition);
                                }

                                result.ReleaseHash = GetReleaseHash(match);
                                if (!result.ReleaseHash.IsNullOrWhiteSpace())
                                {
                                    Logger.Debug("Release Hash parsed: {0}", result.ReleaseHash);
                                }

                                result.OriginalTitle = originalTitle;
                                result.ReleaseTitle = releaseTitle;
                                result.SimpleReleaseTitle = simpleReleaseTitle;

                                result.ImdbId = ParseImdbId(simpleReleaseTitle);
                                result.TmdbId = ParseTmdbId(simpleReleaseTitle);

                                return result;
                            }
                        }
                        catch (InvalidDateException ex)
                        {
                            Logger.Debug(ex, ex.Message);
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                if (!title.ToLowerInvariant().Contains("password") && !title.ToLowerInvariant().Contains("yenc"))
                {
                    Logger.Error(e, "An error has occurred while trying to parse {0}", title);
                }
            }

            Logger.Debug("Unable to parse {0}", title);
            return null;
        }

        public static string ParseImdbId(string title)
        {
            var match = ReportImdbId.Match(title);
            if (match.Success)
            {
                if (match.Groups["imdbid"].Value != null)
                {
                    if (match.Groups["imdbid"].Length == 9 || match.Groups["imdbid"].Length == 10)
                    {
                        return match.Groups["imdbid"].Value.ToLowerInvariant();
                    }
                }
            }

            return "";
        }

        public static int ParseTmdbId(string title)
        {
            var match = ReportTmdbId.Match(title);
            if (match.Success)
            {
                if (match.Groups["tmdbid"].Value != null)
                {
                    return int.TryParse(match.Groups["tmdbid"].Value, out var tmdbId) ? tmdbId : 0;
                }
            }

            return 0;
        }

        public static string ParseEdition(string languageTitle)
        {
            var editionMatch = ReportEditionRegex.Match(languageTitle);

            if (editionMatch.Success && editionMatch.Groups["edition"].Value != null &&
                editionMatch.Groups["edition"].Value.IsNotNullOrWhiteSpace())
            {
                return editionMatch.Groups["edition"].Value.Replace(".", " ");
            }

            return "";
        }

        public static string ReplaceGermanUmlauts(string s)
        {
            var t = s;
            t = t.Replace("ä", "ae");
            t = t.Replace("ö", "oe");
            t = t.Replace("ü", "ue");
            t = t.Replace("Ä", "Ae");
            t = t.Replace("Ö", "Oe");
            t = t.Replace("Ü", "Ue");
            t = t.Replace("ß", "ss");
            return t;
        }

        public static string ToUrlSlug(string value, bool invalidDashReplacement = false, string trimEndChars = "-_", string deduplicateChars = "-_")
        {
            // First to lower case
            value = value.ToLowerInvariant();

            // Remove all accents
            value = value.RemoveAccent();

            // Replace spaces
            value = Regex.Replace(value, @"\s", "-", RegexOptions.Compiled);

            // Should invalid characters be replaced with dash or empty string?
            var replaceCharacter = invalidDashReplacement ? "-" : string.Empty;

            // Remove invalid chars
            value = Regex.Replace(value, @"[^a-z0-9\s-_]", replaceCharacter, RegexOptions.Compiled);

            // Trim dashes or underscores from end, or user defined character set
            if (!string.IsNullOrEmpty(trimEndChars))
            {
                value = value.Trim(trimEndChars.ToCharArray());
            }

            // Replace double occurrences of - or _, or user defined character set
            if (!string.IsNullOrEmpty(deduplicateChars))
            {
                value = Regex.Replace(value, @"([" + deduplicateChars + "]){2,}", "$1", RegexOptions.Compiled);
            }

            return value;
        }

        public static string CleanMovieTitle(this string title)
        {
            if (title.IsNullOrWhiteSpace())
            {
                return title;
            }

            // If Title only contains numbers return it as is.
            if (long.TryParse(title, out _))
            {
                return title;
            }

            return ReplaceGermanUmlauts(MovieNormalizeRegex.Replace(title, string.Empty).ToLowerInvariant()).RemoveAccent();
        }

        public static string NormalizeMovieTitle(string title)
        {
            title = SpecialEpisodeWordRegex.Replace(title, string.Empty);
            title = PunctuationRegex.Replace(title, " ");
            title = DuplicateSpacesRegex.Replace(title, " ");

            return title.Trim().ToLowerInvariant();
        }

        public static string SimplifyReleaseTitle(this string title)
        {
            return SimpleReleaseTitleRegex.Replace(title, string.Empty);
        }

        public static string ParseHardcodeSubs(string title)
        {
            var subMatch = HardcodedSubsRegex.Matches(title).OfType<Match>().LastOrDefault();

            if (subMatch != null && subMatch.Success)
            {
                if (subMatch.Groups["hcsub"].Success)
                {
                    return subMatch.Groups["hcsub"].Value;
                }
                else if (subMatch.Groups["hc"].Success)
                {
                    return "Generic Hardcoded Subs";
                }
            }

            return null;
        }

        private static ParsedMovieInfo ParseMovieMatchCollection(MatchCollection matchCollection)
        {
            if (!matchCollection[0].Groups["title"].Success || matchCollection[0].Groups["title"].Value == "(")
            {
                return null;
            }

            var movieName = matchCollection[0].Groups["title"].Value.Replace('_', ' ');
            movieName = NormalizeAlternativeTitleRegex.Replace(movieName, " AKA ");
            movieName = RequestInfoRegex.Replace(movieName, "").Trim(' ');

            var parts = movieName.Split('.');
            movieName = "";
            var n = 0;
            var previousAcronym = false;
            var nextPart = "";
            foreach (var part in parts)
            {
                if (parts.Length >= n + 2)
                {
                    nextPart = parts[n + 1];
                }
                else
                {
                    nextPart = "";
                }

                if (part.Length == 1 && part.ToLowerInvariant() != "a" && !int.TryParse(part, out _) &&
                    (previousAcronym || n < parts.Length - 1) &&
                    (previousAcronym || nextPart.Length != 1 || !int.TryParse(nextPart, out _)))
                {
                    movieName += part + ".";
                    previousAcronym = true;
                }
                else if (part.ToLowerInvariant() == "a" && (previousAcronym || nextPart.Length == 1))
                {
                    movieName += part + ".";
                    previousAcronym = true;
                }
                else if (part.ToLowerInvariant() == "dr")
                {
                    movieName += part + ".";
                    previousAcronym = true;
                }
                else
                {
                    if (previousAcronym)
                    {
                        movieName += " ";
                        previousAcronym = false;
                    }

                    movieName += part + " ";
                }

                n++;
            }

            movieName = movieName.Trim(' ');

            int.TryParse(matchCollection[0].Groups["year"].Value, out var airYear);

            var result = new ParsedMovieInfo { Year = airYear };

            if (matchCollection[0].Groups["edition"].Success)
            {
                result.Edition = matchCollection[0].Groups["edition"].Value.Replace(".", " ");
            }

            var movieTitles = new List<string>();
            movieTitles.Add(movieName);

            // Delete parentheses of the form (aka ...).
            var unbracketedName = BracketedAlternativeTitleRegex.Replace(movieName, "$1 AKA $2");

            // Split by AKA and filter out empty and duplicate names.
            movieTitles
                .AddRange(AlternativeTitleRegex
                        .Split(unbracketedName)
                        .Where(alternativeName => alternativeName.IsNotNullOrWhiteSpace() && alternativeName != movieName));

            result.MovieTitles = movieTitles;

            Logger.Debug("Movie Parsed. {0}", result);

            return result;
        }

        private static bool ValidateBeforeParsing(string title)
        {
            if (title.ToLowerInvariant().Contains("password") && title.ToLowerInvariant().Contains("yenc"))
            {
                Logger.Debug("");
                return false;
            }

            if (!title.Any(char.IsLetterOrDigit))
            {
                return false;
            }

            var titleWithoutExtension = FileExtensions.RemoveFileExtension(title);

            if (RejectHashedReleasesRegexes.Any(v => v.IsMatch(titleWithoutExtension)))
            {
                Logger.Debug("Rejected Hashed Release Title: " + title);
                return false;
            }

            if (SeasonFolderRegexes.Any(v => v.IsMatch(titleWithoutExtension)))
            {
                Logger.Debug("Rejected Season Folder Release Title: " + title);
                return false;
            }

            return true;
        }

        private static string GetSubGroup(MatchCollection matchCollection)
        {
            var subGroup = matchCollection[0].Groups["subgroup"];

            if (subGroup.Success)
            {
                return subGroup.Value;
            }

            return string.Empty;
        }

        private static string GetReleaseHash(MatchCollection matchCollection)
        {
            var hash = matchCollection[0].Groups["hash"];

            if (hash.Success)
            {
                var hashValue = hash.Value.Trim('[', ']', '(', ')');

                if (hashValue.Equals("1280x720"))
                {
                    return string.Empty;
                }

                return hashValue;
            }

            return string.Empty;
        }

        private static int ParseNumber(string value)
        {
            var normalized = ConvertToNumerals(value.Normalize(NormalizationForm.FormKC));

            if (int.TryParse(normalized, out var number))
            {
                return number;
            }

            number = Array.IndexOf(Numbers, value.ToLowerInvariant());

            if (number != -1)
            {
                return number;
            }

            throw new FormatException(string.Format("{0} isn't a number", value));
        }

        private static decimal ParseDecimal(string value)
        {
            var normalized = ConvertToNumerals(value.Normalize(NormalizationForm.FormKC));

            if (decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                return number;
            }

            throw new FormatException(string.Format("{0} isn't a number", value));
        }

        private static string ConvertToNumerals(string input)
        {
            var result = new StringBuilder(input.Length);

            foreach (var c in input.ToCharArray())
            {
                if (char.IsNumber(c))
                {
                    result.Append(char.GetNumericValue(c));
                }
                else
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }
    }
}
