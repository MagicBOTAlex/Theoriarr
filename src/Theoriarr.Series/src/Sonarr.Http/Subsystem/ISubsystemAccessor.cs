using Microsoft.AspNetCore.Http;

namespace Sonarr.Http.Subsystem
{
    public interface ISubsystemAccessor
    {
        AppSubsystem Subsystem { get; }
        string AppName { get; }
    }

    public class SubsystemAccessor : ISubsystemAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IApiKeyResolver _apiKeyResolver;

        public SubsystemAccessor(IHttpContextAccessor httpContextAccessor, IApiKeyResolver apiKeyResolver)
        {
            _httpContextAccessor = httpContextAccessor;
            _apiKeyResolver = apiKeyResolver;
        }

        public AppSubsystem Subsystem => _apiKeyResolver.ResolveForContext(_httpContextAccessor.HttpContext);

        public string AppName => Subsystem.ToAppName();
    }
}
