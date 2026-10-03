using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.SignalR;
using Sonarr.Http.Subsystem;

namespace Sonarr.Http.REST
{
    public abstract class RestControllerWithSignalR<TResource, TModel> : RestController<TResource>, IHandle<ModelEvent<TModel>>
        where TResource : RestResource, new()
        where TModel : ModelBase, new()
    {
        protected string Resource { get; }
        protected int? Version { get; }

        private readonly IBroadcastSignalRMessage _signalRBroadcaster;
        private readonly SignalRSubsystem _signalRSubsystem;

        protected RestControllerWithSignalR(IBroadcastSignalRMessage signalRBroadcaster)
        {
            _signalRBroadcaster = signalRBroadcaster;

            var apiAttribute = GetType().GetCustomAttribute<VersionedApiControllerAttribute>();
            if (apiAttribute != null && apiAttribute.Resource != VersionedApiControllerAttribute.CONTROLLER_RESOURCE)
            {
                Resource = apiAttribute.Resource;
                Version = apiAttribute.Version;
            }
            else
            {
                Resource = new TResource().ResourceName.Trim('/');
                Version = apiAttribute?.Version;
            }

            _signalRSubsystem = ResolveSignalRSubsystem(GetType());
        }

        // Movie controllers are tagged [AppSubsystem(AppSubsystem.Movies)]; shared controllers
        // carry both tags (or none). Untagged controllers in the series base tree are series
        // unless their model belongs to the movies domain. This keeps real-time events
        // isolated per subsystem without touching the API controller sources.
        private static SignalRSubsystem ResolveSignalRSubsystem(Type controllerType)
        {
            var declared = controllerType.GetCustomAttributes(true)
                .OfType<AppSubsystemAttribute>()
                .Select(attribute => attribute.Subsystem)
                .Distinct()
                .ToList();

            if (declared.Count > 0)
            {
                var movies = declared.Contains(AppSubsystem.Movies);
                var series = declared.Contains(AppSubsystem.Series);

                if (movies && series)
                {
                    return SignalRSubsystem.Shared;
                }

                return movies ? SignalRSubsystem.Movies : SignalRSubsystem.Series;
            }

            var modelNamespace = typeof(TModel).Namespace ?? string.Empty;

            if (modelNamespace.StartsWith("NzbDrone.Core.Movies", StringComparison.Ordinal) ||
                typeof(TModel).Name == "MovieFile")
            {
                return SignalRSubsystem.Movies;
            }

            if (modelNamespace.StartsWith("NzbDrone.Core.Tv", StringComparison.Ordinal) ||
                typeof(TModel).Name == "EpisodeFile")
            {
                return SignalRSubsystem.Series;
            }

            return SignalRSubsystem.Shared;
        }

        [NonAction]
        public void Handle(ModelEvent<TModel> message)
        {
            if (!_signalRBroadcaster.IsConnected)
            {
                return;
            }

            if (message.Action == ModelAction.Deleted || message.Action == ModelAction.Sync)
            {
                BroadcastResourceChange(message.Action);
            }

            BroadcastResourceChange(message.Action, message.Model.Id);
        }

        protected void BroadcastResourceChange(ModelAction action, int id)
        {
            if (!_signalRBroadcaster.IsConnected)
            {
                return;
            }

            if (action == ModelAction.Deleted)
            {
                BroadcastResourceChange(action, new TResource { Id = id });
            }
            else
            {
                var resource = GetResourceById(id);
                BroadcastResourceChange(action, resource);
            }
        }

        protected void BroadcastResourceChange(ModelAction action, TResource resource)
        {
            if (!_signalRBroadcaster.IsConnected)
            {
                return;
            }

            var ns = GetType().Namespace;

            if (ns.Contains("V3") || ns.Contains("V5"))
            {
                var signalRMessage = new SignalRMessage
                {
                    Name = Resource,
                    Body = new ResourceChangeMessage<TResource>(resource, action),
                    Action = action,
                    Version = Version,
                    Subsystem = _signalRSubsystem
                };

                _signalRBroadcaster.BroadcastMessage(signalRMessage);
            }
        }

        protected void BroadcastResourceChange(ModelAction action)
        {
            if (!_signalRBroadcaster.IsConnected)
            {
                return;
            }

            var ns = GetType().Namespace;

            if (ns.Contains("V3") || ns.Contains("V5"))
            {
                var signalRMessage = new SignalRMessage
                {
                    Name = Resource,
                    Body = new ResourceChangeMessage<TResource>(action),
                    Action = action,
                    Version = Version,
                    Subsystem = _signalRSubsystem
                };

                _signalRBroadcaster.BroadcastMessage(signalRMessage);
            }
        }
    }
}
