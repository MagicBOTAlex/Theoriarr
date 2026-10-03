using NzbDrone.Core.Configuration;
using Sonarr.Http;
using Sonarr.Http.Subsystem;

namespace Sonarr.Api.V3.Config
{
    [AppSubsystem(AppSubsystem.Movies)]
    [V3ApiController("config/metadata")]
    public class MetadataConfigController : ConfigController<MetadataConfigResource>
    {
        public MetadataConfigController(IConfigService configService)
            : base(configService)
        {
        }

        protected override MetadataConfigResource ToResource(IConfigService model)
        {
            return MetadataConfigResourceMapper.ToResource(model);
        }
    }
}
