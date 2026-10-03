using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Movies;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.DiskSpace
{
    public interface IDiskSpaceService
    {
        List<DiskSpace> GetFreeSpace();
        List<DiskSpaceContent> GetContent(string path);
    }

    public class DiskSpaceService : IDiskSpaceService
    {
        private readonly ISeriesService _seriesService;
        private readonly IMovieService _movieService;
        private readonly IRootFolderService _rootFolderService;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        private static readonly Regex _regexSpecialDrive = new Regex(@"^/var/lib/(docker|rancher|kubelet)(/|$)|^/(boot|etc)(/|$)|/docker(/var)?/aufs(/|$)|/\.timemachine", RegexOptions.Compiled);

        private static readonly HashSet<string> _ignoredContentDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "proc",
            "sys",
            "dev",
            "run"
        };

        public DiskSpaceService(ISeriesService seriesService, IMovieService movieService, IRootFolderService rootFolderService, IDiskProvider diskProvider, Logger logger)
        {
            _seriesService = seriesService;
            _movieService = movieService;
            _rootFolderService = rootFolderService;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public List<DiskSpace> GetFreeSpace()
        {
            var importantRootFolders = GetMediaRootPaths().Distinct().ToList();

            var optionalRootFolders = GetFixedDisksRootPaths().Except(importantRootFolders).Distinct().ToList();

            var diskSpace = GetDiskSpace(importantRootFolders)
                .Concat(GetDiskSpace(optionalRootFolders, true))
                .OrderBy(d => d.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return diskSpace;
        }

        public List<DiskSpaceContent> GetContent(string path)
        {
            var contents = new List<DiskSpaceContent>();

            if (string.IsNullOrWhiteSpace(path) || !_diskProvider.FolderExists(path))
            {
                return contents;
            }

            var mediaRootFolders = _rootFolderService.All()
                .Select(r => r.Path)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            foreach (var directory in _diskProvider.GetDirectories(path))
            {
                var name = GetName(directory);

                if (_ignoredContentDirectories.Contains(name))
                {
                    continue;
                }

                if (IsMediaPath(directory, mediaRootFolders))
                {
                    continue;
                }

                var size = GetSize(() => _diskProvider.GetFolderSize(directory), directory);

                if (!size.HasValue)
                {
                    continue;
                }

                contents.Add(new DiskSpaceContent
                {
                    Path = directory,
                    Name = name,
                    IsFile = false,
                    Size = size.Value
                });
            }

            foreach (var file in _diskProvider.GetFiles(path, false))
            {
                var size = GetSize(() => _diskProvider.GetFileSize(file), file);

                if (!size.HasValue)
                {
                    continue;
                }

                contents.Add(new DiskSpaceContent
                {
                    Path = file,
                    Name = GetName(file),
                    IsFile = true,
                    Size = size.Value
                });
            }

            return contents.OrderByDescending(c => c.Size).ToList();
        }

        private static bool IsMediaPath(string path, List<string> mediaRootFolders)
        {
            return mediaRootFolders.Any(root =>
                path.PathEquals(root) || root.IsParentPath(path) || path.IsParentPath(root));
        }

        private static string GetName(string path)
        {
            var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return Path.GetFileName(trimmed);
        }

        private long? GetSize(Func<long> getSize, string path)
        {
            try
            {
                return getSize();
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to get size of: {0}", path);

                return null;
            }
        }

        private IEnumerable<string> GetMediaRootPaths()
        {
            // Get all series and movie paths and find the correct root folder for each. For each unique root
            // folder path, ensure the path exists and get its path root and return all unique path roots.

            var mediaPaths = _seriesService.GetAllSeriesPaths().Values
                .Concat(_movieService.AllMoviePaths().Values);

            return mediaPaths
                .Where(s => s.IsPathValid(PathValidationType.CurrentOs))
                .Select(s => _rootFolderService.GetBestRootFolderPath(s))
                .Distinct()
                .Where(r => _diskProvider.FolderExists(r))
                .Select(r => _diskProvider.GetPathRoot(r))
                .Distinct();
        }

        private IEnumerable<string> GetFixedDisksRootPaths()
        {
            return _diskProvider.GetMounts()
                .Where(d => d.DriveType is DriveType.Fixed or DriveType.Network)
                .Where(d => !_regexSpecialDrive.IsMatch(d.RootDirectory))
                .Select(d => d.RootDirectory);
        }

        private IEnumerable<DiskSpace> GetDiskSpace(IEnumerable<string> paths, bool suppressWarnings = false)
        {
            foreach (var path in paths)
            {
                DiskSpace diskSpace = null;

                try
                {
                    var freeSpace = _diskProvider.GetAvailableSpace(path);
                    var totalSpace = _diskProvider.GetTotalSize(path);

                    if (!freeSpace.HasValue || !totalSpace.HasValue)
                    {
                        continue;
                    }

                    diskSpace = new DiskSpace
                    {
                        Path = path,
                        FreeSpace = freeSpace.Value,
                        TotalSpace = totalSpace.Value
                    };

                    diskSpace.Label = _diskProvider.GetVolumeLabel(path);
                }
                catch (Exception ex)
                {
                    if (!suppressWarnings)
                    {
                        _logger.Warn(ex, "Unable to get free space for: " + path);
                    }
                }

                if (diskSpace != null)
                {
                    yield return diskSpace;
                }
            }
        }
    }
}
