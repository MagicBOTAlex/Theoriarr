using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Sonarr.Http.Extensions;
using Sonarr.Http.Subsystem;

namespace Sonarr.Http.Middleware
{
    public class VersionMiddleware
    {
        private const string VERSIONHEADER = "X-Application-Version";
        private const string APPLICATIONHEADER = "X-Application";

        private readonly RequestDelegate _next;
        private readonly IApiKeyResolver _apiKeyResolver;
        private readonly ISubsystemInfo _subsystemInfo;

        public VersionMiddleware(RequestDelegate next, IApiKeyResolver apiKeyResolver, ISubsystemInfo subsystemInfo)
        {
            _next = next;
            _apiKeyResolver = apiKeyResolver;
            _subsystemInfo = subsystemInfo;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.IsApiRequest() && !context.Response.Headers.ContainsKey(VERSIONHEADER))
            {
                // The key selects the domain; report that domain's build so a client can
                // detect a wrong-domain key (e.g. a series key added as a Radarr server).
                var subsystem = _apiKeyResolver.ResolveForContext(context);

                context.Response.Headers[VERSIONHEADER] = _subsystemInfo.GetVersion(subsystem).ToString();
                context.Response.Headers[APPLICATIONHEADER] = _subsystemInfo.GetAppName(subsystem);
            }

            await _next(context);
        }
    }
}
