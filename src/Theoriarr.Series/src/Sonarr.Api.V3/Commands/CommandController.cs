using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Composition;
using NzbDrone.Common.Serializer;
using NzbDrone.Common.TPL;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.MediaFiles.EpisodeImport.Manual;
using NzbDrone.Core.MediaFiles.MovieImport.Manual;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.ProgressMessaging;
using NzbDrone.SignalR;
using Sonarr.Http;
using Sonarr.Http.REST;
using Sonarr.Http.REST.Attributes;
using Sonarr.Http.Subsystem;
using Sonarr.Http.Validation;

namespace Sonarr.Api.V3.Commands
{
    [V3ApiController]
    [AppSubsystem(AppSubsystem.Series)]
    [AppSubsystem(AppSubsystem.Movies)]
    public class CommandController : RestControllerWithSignalR<CommandResource, CommandModel>, IHandle<CommandUpdatedEvent>
    {
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly KnownTypes _knownTypes;
        private readonly IApiKeyResolver _apiKeyResolver;
        private readonly Debouncer _debouncer;
        private readonly Dictionary<int, CommandResource> _pendingUpdates;

        private readonly CommandPriorityComparer _commandPriorityComparer = new CommandPriorityComparer();

        public CommandController(IManageCommandQueue commandQueueManager,
                             IBroadcastSignalRMessage signalRBroadcaster,
                             KnownTypes knownTypes,
                             IApiKeyResolver apiKeyResolver)
            : base(signalRBroadcaster)
        {
            _commandQueueManager = commandQueueManager;
            _knownTypes = knownTypes;
            _apiKeyResolver = apiKeyResolver;

            _debouncer = new Debouncer(SendUpdates, TimeSpan.FromSeconds(0.1));
            _pendingUpdates = new Dictionary<int, CommandResource>();

            PostValidator.RuleFor(c => c.Name).NotBlank();
        }

        protected override CommandResource GetResourceById(int id)
        {
            return _commandQueueManager.Get(id).ToResource();
        }

        [RestPostById]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<CommandResource> StartCommand([FromBody] CommandResource commandResource)
        {
            var commandType = ResolveCommandType(commandResource.Name, _apiKeyResolver.ResolveForContext(HttpContext));

            if (commandType == null)
            {
                return BadRequest($"Unknown command '{commandResource.Name}'");
            }

            Request.Body.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(Request.Body))
            {
                var body = reader.ReadToEnd();
                var priority = commandType == typeof(ManualImportCommand) || commandType == typeof(MovieManualImportCommand)
                    ? CommandPriority.High
                    : CommandPriority.Normal;

                var command = STJson.Deserialize(body, commandType) as Command;

                command.SuppressMessages = !command.SendUpdatesToClient;
                command.SendUpdatesToClient = true;
                command.ClientUserAgent = Request.Headers["UserAgent"];

                var trackedCommand = _commandQueueManager.Push(command, priority, CommandTrigger.Manual);

                return Created(trackedCommand.Id);
            }
        }

        private Type ResolveCommandType(string name, AppSubsystem subsystem)
        {
            // Radarr's manual import command is called `ManualImport`, the same API name as
            // Sonarr's series command. Resolve it per domain so movie-domain clients keep
            // working; the in-repo SPA uses the explicit `MovieManualImport` alias.
            if (subsystem == AppSubsystem.Movies &&
                name.Equals("ManualImport", StringComparison.InvariantCultureIgnoreCase))
            {
                return typeof(MovieManualImportCommand);
            }

            return _knownTypes.GetImplementations(typeof(Command))
                              .FirstOrDefault(c => c.Name.Replace("Command", "")
                                                    .Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        [HttpGet]
        [Produces("application/json")]
        public List<CommandResource> GetStartedCommands()
        {
            return _commandQueueManager.All()
                .OrderBy(c => c.Status, _commandPriorityComparer)
                .ThenByDescending(c => c.Priority)
                .ToResource();
        }

        [RestDeleteById]
        public void CancelCommand(int id)
        {
            _commandQueueManager.Cancel(id);
        }

        [NonAction]
        public void Handle(CommandUpdatedEvent message)
        {
            if (message.Command.Body.SendUpdatesToClient)
            {
                lock (_pendingUpdates)
                {
                    _pendingUpdates[message.Command.Id] = message.Command.ToResource();
                }

                _debouncer.Execute();
            }
        }

        private void SendUpdates()
        {
            lock (_pendingUpdates)
            {
                var pendingUpdates = _pendingUpdates.Values.ToArray();
                _pendingUpdates.Clear();

                foreach (var pendingUpdate in pendingUpdates)
                {
                    BroadcastResourceChange(ModelAction.Updated, pendingUpdate);

                    if (pendingUpdate.Name == typeof(MessagingCleanupCommand).Name.Replace("Command", "") &&
                        pendingUpdate.Status == CommandStatus.Completed)
                    {
                        BroadcastResourceChange(ModelAction.Sync);
                    }
                }
            }
        }
    }
}
