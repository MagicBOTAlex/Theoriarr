using System;
using System.Collections.Generic;
using System.IO;
using FluentValidation.Results;
using NzbDrone.Core.Extras.Metadata.Files;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Movies;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Extras.Metadata
{
    public abstract class MetadataBase<TSettings> : IMetadata
        where TSettings : IProviderConfig, new()
    {
        public abstract string Name { get; }

        public Type ConfigContract => typeof(TSettings);

        public virtual ProviderMessage Message => null;

        public IEnumerable<ProviderDefinition> DefaultDefinitions => new List<ProviderDefinition>();

        public ProviderDefinition Definition { get; set; }

        public ValidationResult Test()
        {
            return new ValidationResult();
        }

        public virtual string GetFilenameAfterMove(Series series, EpisodeFile episodeFile, MetadataFile metadataFile)
        {
            var existingFilename = Path.Combine(series.Path, metadataFile.RelativePath);
            var extension = Path.GetExtension(existingFilename).TrimStart('.');
            var newFileName = Path.ChangeExtension(Path.Combine(series.Path, episodeFile.RelativePath), extension);

            return newFileName;
        }

        public virtual MetadataFile FindMetadataFile(Series series, string path)
        {
            return null;
        }

        public virtual MetadataFileResult SeriesMetadata(Series series, SeriesMetadataReason reason)
        {
            return null;
        }

        public virtual MetadataFileResult EpisodeMetadata(Series series, EpisodeFile episodeFile)
        {
            return null;
        }

        public virtual List<ImageFileResult> SeriesImages(Series series)
        {
            return new List<ImageFileResult>();
        }

        public virtual List<ImageFileResult> SeasonImages(Series series, Season season)
        {
            return new List<ImageFileResult>();
        }

        public virtual List<ImageFileResult> EpisodeImages(Series series, EpisodeFile episodeFile)
        {
            return new List<ImageFileResult>();
        }

        public virtual string GetFilenameAfterMove(Movie movie, MovieFile movieFile, MetadataFile metadataFile)
        {
            var existingFilename = Path.Combine(movie.Path, metadataFile.RelativePath);
            var extension = Path.GetExtension(existingFilename).TrimStart('.');
            var newFileName = Path.ChangeExtension(Path.Combine(movie.Path, movieFile.RelativePath), extension);

            return newFileName;
        }

        public virtual MetadataFile FindMetadataFile(Movie movie, string path)
        {
            return null;
        }

        public virtual MetadataFileResult MovieMetadata(Movie movie, MovieFile movieFile)
        {
            return null;
        }

        public virtual List<ImageFileResult> MovieImages(Movie movie)
        {
            return new List<ImageFileResult>();
        }

        public virtual object RequestAction(string action, IDictionary<string, string> query)
        {
            return null;
        }

        protected TSettings Settings => (TSettings)Definition.Settings;

        public override string ToString()
        {
            return GetType().Name;
        }
    }
}
