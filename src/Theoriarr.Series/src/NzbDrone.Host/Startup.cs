using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DryIoc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using NLog.Extensions.Logging;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Common.Processes;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Instrumentation;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Host.AccessControl;
using NzbDrone.Http.Authentication;
using NzbDrone.SignalR;
using Sonarr.Api.V3.System;
using Sonarr.Api.V5.Series;
using Sonarr.Http;
using Sonarr.Http.Authentication;
using Sonarr.Http.ClientSchema;
using Sonarr.Http.ErrorManagement;
using Sonarr.Http.Frontend;
using Sonarr.Http.Middleware;
using Sonarr.Http.Subsystem;
using StackExchange.Profiling;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace NzbDrone.Host
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddLogging(b =>
            {
                b.ClearProviders();
                b.SetMinimumLevel(LogLevel.Trace);
                b.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
                b.AddFilter("Microsoft.AspNetCore.HostFiltering", LogLevel.Information);
                b.AddFilter("Microsoft.AspNetCore.HttpOverrides", LogLevel.Debug);
                b.AddFilter("Sonarr.Http.Authentication.ApiKeyAuthenticationHandler", LogLevel.Information);
                b.AddFilter("Microsoft.AspNetCore.DataProtection.KeyManagement.XmlKeyManager", LogLevel.Error);
                b.AddNLog();
            });

            services.AddOptions<ForwardedHeadersOptions>()
                    .Configure<IConfigFileProvider>(ForwardedHeadersConfigurator.Configure);

            services.AddRouting(options => options.LowercaseUrls = true);

            services.AddResponseCompression();

            services.AddCors(options =>
            {
                options.AddPolicy(VersionedApiControllerAttribute.API_CORS_POLICY,
                    builder =>
                    builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .WithExposedHeaders("X-Application", "X-Application-Version"));

                options.AddPolicy("AllowGet",
                    builder =>
                    builder.AllowAnyOrigin()
                    .WithMethods("GET", "OPTIONS")
                    .AllowAnyHeader()
                    .WithExposedHeaders("X-Application", "X-Application-Version"));
            });

            services
            .AddControllers(options =>
            {
                options.ReturnHttpNotAcceptable = true;
            })

            // Register all controllers from the unified API and HTTP projects.
            // Subsystem ownership is explicit: series-only and movie-only controllers carry
            // [AppSubsystem(...)], and controllers shared by both domains carry both attributes.
            // Controllers in the V5 assembly default to Series via AddAppSubsystemConvention
            // below, so a new V5 controller cannot silently become reachable with the movie key.
            // V3 is not defaulted because it deliberately mixes series, movie and dual controllers;
            // if a separate movie assembly is ever introduced it must be added as an application
            // part plus AddAppSubsystemConvention(AppSubsystem.Movies, movieAssembly).
            .AddApplicationPart(typeof(SystemController).Assembly)
            .AddApplicationPart(typeof(SeriesLookupController).Assembly)
            .AddApplicationPart(typeof(StaticResourceController).Assembly)
            .AddJsonOptions(options =>
            {
                STJson.ApplySerializerSettings(options.JsonSerializerOptions);
            })
            .AddControllersAsServices();

            services.ConfigureHttpJsonOptions(options =>
            {
                STJson.ApplySerializerSettings(options.SerializerOptions);
            });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v3", new OpenApiInfo
                {
                    Version = "3.0.0",
                    Title = "Theoriarr",
                    Description = "Theoriarr API docs - The v3 API docs apply to both v3 and v4 versions of Theoriarr. Some functionality may only be available in v4 of the Theoriarr application.",
                    License = new OpenApiLicense
                    {
                        Name = "GPL-3.0",
                        Url = new Uri("https://github.com/Sonarr/Sonarr/blob/develop/LICENSE")
                    }
                });

                c.SwaggerDoc("v5", new OpenApiInfo
                {
                    Version = "5.0.0",
                    Title = "Theoriarr",
                    Description = "Theoriarr API docs - The v5 API docs apply to Theoriarr v5 only.",
                    License = new OpenApiLicense
                    {
                        Name = "GPL-3.0",
                        Url = new Uri("https://github.com/Sonarr/Sonarr/blob/develop/LICENSE")
                    }
                });

                var apiKeyHeader = new OpenApiSecurityScheme
                {
                    Name = "X-Api-Key",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "apiKey",
                    Description = "Apikey passed as header",
                    In = ParameterLocation.Header,
                };

                c.AddSecurityDefinition("X-Api-Key", apiKeyHeader);

                c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(apiKeyHeader.Name, document)] = new List<string>(),
                });

                var apikeyQuery = new OpenApiSecurityScheme
                {
                    Name = "apikey",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "apiKey",
                    Description = "Apikey passed as query parameter",
                    In = ParameterLocation.Query,
                };

                c.AddServer(new OpenApiServer
                {
                    Url = "{protocol}://{hostpath}",
                    Variables = new Dictionary<string, OpenApiServerVariable>
                    {
                        { "protocol", new OpenApiServerVariable { Default = "http", Enum = new List<string> { "http", "https" } } },
                        { "hostpath", new OpenApiServerVariable { Default = "localhost:6868" } }
                    }
                });

                c.AddSecurityDefinition("apikey", apikeyQuery);

                c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(apikeyQuery.Name, document)] = new List<string>(),
                });

                c.DescribeAllParametersInCamelCase();

                // Generate docs based on the controller's API version
                c.DocInclusionPredicate((docName, apiDesc) =>
                {
                    Type type = null;

                    if (apiDesc.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
                    {
                        type = controllerActionDescriptor.ControllerTypeInfo;
                    }

                    if (type == null)
                    {
                        return false;
                    }

                    var versions = new List<int>();

                    versions.AddRange(type
                        .GetCustomAttributes(true)
                        .OfType<VersionedApiControllerAttribute>()
                        .Select(attr => attr.Version));

                    versions.AddRange(type
                        .GetCustomAttributes(true)
                        .OfType<VersionedFeedControllerAttribute>()
                        .Select(attr => attr.Version));

                    // Return anything with no version or a matching version
                    return !versions.Any() || versions.Any(v => $"v{v}" == docName);
                });
            });

            services
            .AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions = STJson.GetSerializerSettings();
            });

            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Configuration["dataProtectionFolder"]));

            services.AddSingleton<IAuthorizationPolicyProvider, UiAuthorizationPolicyProvider>();
            services.AddSingleton<IAuthorizationHandler, UiAuthorizationHandler>();
            services.AddSingleton<IAuthorizationHandler, BootstrapKeyAccessAuthorizationHandler>();

            services.AddAuthorization(options =>
            {
                options.AddPolicy("SignalR", policy =>
                {
                    policy.AuthenticationSchemes.Add("SignalR");
                    policy.RequireAuthenticatedUser();
                });

                // Require auth on everything except those marked [AllowAnonymous]
                options.FallbackPolicy = new AuthorizationPolicyBuilder("API")
                .RequireAuthenticatedUser()
                .Build();
            });

            services.AddAppAuthentication();

            // D1: resolve the presented API key (series or movie) to an AppSubsystem and select
            // the matching [AppSubsystem]-tagged endpoints. Registers SubsystemMatcherPolicy as a
            // MatcherPolicy, which endpoint routing picks up via GetServices<MatcherPolicy>() in
            // UseEndpoints/MapControllers - no explicit UseEndpoints change is required.
            services.AddAppSubsystemRouting();

            // Default the whole V5 surface to the Series subsystem so a V5 controller that is
            // not tagged explicitly still cannot be reached with the movie key. V3 is left
            // untouched because it mixes series, movie and dual-tagged controllers.
            services.AddAppSubsystemConvention(AppSubsystem.Series, typeof(SeriesLookupController).Assembly);

            services.AddOptions<MiniProfilerOptions>()
                .Configure<IConfigFileProvider>((options, configFileProvider) =>
                {
                    options.RouteBasePath = "/profiler";

                    switch (configFileProvider.Theme)
                    {
                        case "light":
                            options.ColorScheme = ColorScheme.Light;
                            break;
                        case "dark":
                            options.ColorScheme = ColorScheme.Dark;
                            break;
                        default:
                            options.ColorScheme = ColorScheme.Auto;
                            break;
                    }

                    switch (configFileProvider.ProfilerPosition)
                    {
                        case "top-left":
                            options.PopupRenderPosition = RenderPosition.Left;
                            break;
                        case "top-right":
                            options.PopupRenderPosition = RenderPosition.Right;
                            break;
                        case "bottom-left":
                            options.PopupRenderPosition = RenderPosition.BottomLeft;
                            break;
                        default:
                            options.PopupRenderPosition = RenderPosition.BottomRight;
                            break;
                    }

                    options.IgnoredPaths.Add("/MediaCover");
                });

            services.AddMiniProfiler();
        }

        public void Configure(IApplicationBuilder app,
                              IContainer container,
                              IStartupContext startupContext,
                              Lazy<IMainDatabase> mainDatabaseFactory,
                              Lazy<ILogDatabase> logDatabaseFactory,
                              DatabaseTarget dbTarget,
                              ISingleInstancePolicy singleInstancePolicy,
                              InitializeLogger initializeLogger,
                              ReconfigureLogging reconfigureLogging,
                              IAppFolderFactory appFolderFactory,
                              IProvidePidFile pidFileProvider,
                              IConfigFileProvider configFileProvider,
                              IRuntimeInfo runtimeInfo,
                              IFirewallAdapter firewallAdapter,
                              IEventAggregator eventAggregator,
                              TheoriarrErrorPipeline errorHandler)
        {
            initializeLogger.Initialize();
            appFolderFactory.Register();
            pidFileProvider.Write();

            configFileProvider.EnsureDefaultConfigFile();

            reconfigureLogging.Reconfigure();

            EnsureSingleInstance(false, startupContext, singleInstancePolicy);

            // instantiate the databases to initialize/migrate them
            _ = mainDatabaseFactory.Value;

            if (configFileProvider.LogDbEnabled)
            {
                _ = logDatabaseFactory.Value;
                dbTarget.Register();
            }

            SchemaBuilder.Initialize(container);

            if (OsInfo.IsNotWindows)
            {
                Console.CancelKeyPress += (sender, eventArgs) => NLog.LogManager.Configuration = null;
            }

            eventAggregator.PublishEvent(new ApplicationStartingEvent());

            if (OsInfo.IsWindows && runtimeInfo.IsAdmin)
            {
                firewallAdapter.MakeAccessible();
            }

            app.UseForwardedHeaders();
            app.UseMiddleware<AllowedHostsMiddleware>();
            app.UseMiddleware<LoggingMiddleware>();
            app.UsePathBase(new PathString(configFileProvider.UrlBase));
            app.UseExceptionHandler(new ExceptionHandlerOptions
            {
                AllowStatusCode404Response = true,
                ExceptionHandler = errorHandler.HandleException
            });

            app.UseRouting();
            app.UseCors();

            // Emit the domain headers before authentication so they are present even on the
            // 401/403 short-circuit a wrong-domain client receives.
            app.UseMiddleware<VersionMiddleware>();

            app.UseAuthentication();
            app.UseAuthorization();
            app.UseResponseCompression();
            app.Properties["host.AppName"] = BuildInfo.AppName;

            app.UseMiddleware<UrlBaseMiddleware>(configFileProvider.UrlBase);
            app.UseMiddleware<StartingUpMiddleware>();
            app.UseMiddleware<CacheHeaderMiddleware>();
            app.UseMiddleware<IfModifiedMiddleware>();
            app.UseMiddleware<BufferingMiddleware>(new List<string> { "/api/v3/command", "/api/v5/command" });

            app.UseWebSockets();

            if (configFileProvider.ProfilerEnabled)
            {
                app.UseMiniProfiler();
            }

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            if (BuildInfo.IsDebug)
            {
                app.UseSwagger(c =>
                {
                    c.RouteTemplate = "docs/{documentName}/openapi.json";
                });
            }

            app.UseEndpoints(x =>
            {
                // Per-subsystem hubs. The SPA appends "/messages" to the bootstrap signalrRoot,
                // so the effective paths are /signalr/series/messages (series events) and
                // /signalr/movies/messages (movie events); the bare and legacy series routes are
                // kept as well. SignalRMessage.Subsystem decides which hub a broadcast reaches.
                x.MapHub<MessageHub>("/signalr/messages").RequireAuthorization("SignalR");
                x.MapHub<MessageHub>("/signalr/series").RequireAuthorization("SignalR");
                x.MapHub<MessageHub>("/signalr/series/messages").RequireAuthorization("SignalR");
                x.MapHub<MovieMessageHub>("/signalr/movies").RequireAuthorization("SignalR");
                x.MapHub<MovieMessageHub>("/signalr/movies/messages").RequireAuthorization("SignalR");

                if (configFileProvider.ProfilerEnabled)
                {
                    x.MapPost("/profiler/results", context => Task.CompletedTask).RequireAuthorization("UI");
                }

                x.MapControllers();
            });
        }

        private void EnsureSingleInstance(bool isService, IStartupContext startupContext, ISingleInstancePolicy instancePolicy)
        {
            if (startupContext.Flags.Contains(StartupContext.NO_SINGLE_INSTANCE_CHECK))
            {
                return;
            }

            if (startupContext.Flags.Contains(StartupContext.TERMINATE))
            {
                instancePolicy.KillAllOtherInstance();
            }
            else if (startupContext.Args.ContainsKey(StartupContext.APPDATA))
            {
                instancePolicy.WarnIfAlreadyRunning();
            }
            else if (isService)
            {
                instancePolicy.KillAllOtherInstance();
            }
            else
            {
                instancePolicy.PreventStartIfAlreadyRunning();
            }
        }
    }
}
