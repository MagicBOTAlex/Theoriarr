using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Sonarr.Http.Subsystem
{
    public static class SubsystemServiceCollectionExtensions
    {
        public static IServiceCollection AddAppSubsystemRouting(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddSingleton<ISubsystemConfig, SubsystemConfig>();
            services.AddSingleton<IApiKeyResolver, ApiKeyResolver>();
            services.AddSingleton<ISubsystemInfo, SubsystemInfo>();
            services.AddScoped<ISubsystemAccessor, SubsystemAccessor>();
            services.AddSingleton<MatcherPolicy, SubsystemMatcherPolicy>();

            return services;
        }

        public static IServiceCollection AddAppSubsystemConvention(this IServiceCollection services, AppSubsystem subsystem, Assembly assembly = null)
        {
            services.Configure<MvcOptions>(options => options.Conventions.Add(new AppSubsystemConvention(subsystem, assembly)));

            return services;
        }
    }
}
