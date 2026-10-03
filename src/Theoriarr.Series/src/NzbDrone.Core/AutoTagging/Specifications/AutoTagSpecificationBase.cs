using NzbDrone.Core.Movies;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.AutoTagging.Specifications
{
    public abstract class AutoTaggingSpecificationBase : IAutoTaggingSpecification
    {
        public abstract int Order { get; }
        public abstract string ImplementationName { get; }

        public string Name { get; set; }
        public bool Negate { get; set; }
        public bool Required { get; set; }

        public IAutoTaggingSpecification Clone()
        {
            return (IAutoTaggingSpecification)MemberwiseClone();
        }

        public abstract NzbDroneValidationResult Validate();

        public bool IsSatisfiedBy(Series series)
        {
            var match = IsSatisfiedByWithoutNegate(series);

            if (Negate)
            {
                match = !match;
            }

            return match;
        }

        public virtual bool IsSatisfiedBy(Movie movie)
        {
            var match = IsSatisfiedByWithoutNegate(movie);

            if (Negate)
            {
                match = !match;
            }

            return match;
        }

        protected virtual bool IsSatisfiedByWithoutNegate(Series series)
        {
            return false;
        }

        protected virtual bool IsSatisfiedByWithoutNegate(Movie movie)
        {
            return false;
        }
    }
}
