using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Sonarr.Http.Subsystem
{
    // Defaults every controller in the configured assembly that carries no explicit
    // [AppSubsystem] metadata to the given subsystem. The metadata has to be added to the
    // per-selector endpoint metadata: that is what ControllerActionDescriptorBuilder copies
    // onto the endpoint and what SubsystemMatcherPolicy reads. Adding to
    // ControllerModel.Attributes would not reach the endpoint.
    public class AppSubsystemConvention : IApplicationModelConvention
    {
        private readonly AppSubsystem _subsystem;
        private readonly Assembly _assembly;

        public AppSubsystemConvention(AppSubsystem subsystem, Assembly assembly = null)
        {
            _subsystem = subsystem;
            _assembly = assembly;
        }

        public void Apply(ApplicationModel application)
        {
            var defaultAttribute = new AppSubsystemAttribute(_subsystem);

            foreach (var controller in application.Controllers)
            {
                if (_assembly != null && controller.ControllerType.Assembly != _assembly)
                {
                    continue;
                }

                // Explicit controller-level metadata (including inherited) always wins.
                if (controller.Attributes.OfType<IAppSubsystemMetadata>().Any())
                {
                    continue;
                }

                foreach (var action in controller.Actions)
                {
                    // Leave actions that were tagged explicitly on their own.
                    if (action.Attributes.OfType<IAppSubsystemMetadata>().Any())
                    {
                        continue;
                    }

                    foreach (var selector in action.Selectors)
                    {
                        selector.EndpointMetadata.Add(defaultAttribute);
                    }
                }
            }
        }
    }
}
