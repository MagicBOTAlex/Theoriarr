using System.Linq;
using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Languages;
using Sonarr.Http;
using Sonarr.Http.REST.Attributes;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.Config
{
    [V3ApiController("config/ui")]
    [AppSubsystem(AppSubsystem.Series)]
    [AppSubsystem(AppSubsystem.Movies)]
    public class UiConfigController : ConfigController<UiConfigResource>
    {
        private readonly IConfigFileProvider _configFileProvider;

        public UiConfigController(IConfigFileProvider configFileProvider, IConfigService configService)
            : base(configService)
        {
            _configFileProvider = configFileProvider;
            SharedValidator.RuleFor(c => c.UILanguage).Custom((value, context) =>
            {
                if (!Language.All.Any(o => o.Id == value))
                {
                    context.AddFailure("Invalid UI Language value");
                }
            });

            SharedValidator.RuleFor(c => c.UILanguage)
                           .GreaterThanOrEqualTo(1)
                           .WithMessage("The UI Language value cannot be less than 1");

            SharedValidator.RuleFor(c => c.MovieInfoLanguage)
                           .GreaterThanOrEqualTo(1)
                           .WithMessage("The Movie Info Language value cannot be less than 1");

            SharedValidator.RuleFor(c => c.MovieInfoLanguage).Custom((value, context) =>
            {
                if (!Language.All.Any(o => o.Id == value))
                {
                    context.AddFailure("Invalid Movie Info Language value");
                }
            });
        }

        [RestPutById]
        public override ActionResult<UiConfigResource> SaveConfig([FromBody] UiConfigResource resource)
        {
            var dictionary = resource.GetType()
                                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                     .ToDictionary(prop => prop.Name, prop => prop.GetValue(resource, null));

            _configFileProvider.SaveConfigDictionary(dictionary);
            _configService.SaveConfigDictionary(dictionary);

            return Accepted(resource.Id);
        }

        protected override UiConfigResource ToResource(IConfigService model)
        {
            return UiConfigResourceMapper.ToResource(_configFileProvider, model);
        }
    }
}
