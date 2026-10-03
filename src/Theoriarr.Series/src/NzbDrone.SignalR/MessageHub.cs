using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using NzbDrone.Common.EnvironmentInfo;

namespace NzbDrone.SignalR
{
    public class SignalRMessageBroadcaster : IBroadcastSignalRMessage
    {
        private readonly IHubContext<MessageHub> _seriesHubContext;
        private readonly IHubContext<MovieMessageHub> _movieHubContext;

        public SignalRMessageBroadcaster(IHubContext<MessageHub> seriesHubContext,
                                         IHubContext<MovieMessageHub> movieHubContext)
        {
            _seriesHubContext = seriesHubContext;
            _movieHubContext = movieHubContext;
        }

        public async Task BroadcastMessage(SignalRMessage message)
        {
            switch (message.Subsystem)
            {
                case SignalRSubsystem.Movies:
                    await _movieHubContext.Clients.All.SendAsync("receiveMessage", message);
                    break;

                case SignalRSubsystem.Series:
                    await _seriesHubContext.Clients.All.SendAsync("receiveMessage", message);
                    break;

                default:
                    await _seriesHubContext.Clients.All.SendAsync("receiveMessage", message);
                    await _movieHubContext.Clients.All.SendAsync("receiveMessage", message);
                    break;
            }
        }

        public bool IsConnected => SignalRConnectionTracker.IsConnected;
    }

    public abstract class MessageHubBase : Hub
    {
        public static bool IsConnected => SignalRConnectionTracker.IsConnected;

        public override async Task OnConnectedAsync()
        {
            SignalRConnectionTracker.Add(Context.ConnectionId);

            var message = new SignalRMessage
            {
                Name = "version",
                Body = new
                {
                    Version = BuildInfo.Version.ToString()
                }
            };

            await Clients.All.SendAsync("receiveMessage", message);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            SignalRConnectionTracker.Remove(Context.ConnectionId);

            await base.OnDisconnectedAsync(exception);
        }
    }

    // Series (and shared) subsystem hub; mapped at /signalr/series, /signalr/series/messages
    // and the legacy /signalr/messages.
    public class MessageHub : MessageHubBase
    {
    }

    // Movie subsystem hub; mapped at /signalr/movies and /signalr/movies/messages.
    public class MovieMessageHub : MessageHubBase
    {
    }

    internal static class SignalRConnectionTracker
    {
        private static readonly HashSet<string> _connections = new HashSet<string>();

        public static bool IsConnected
        {
            get
            {
                lock (_connections)
                {
                    return _connections.Count != 0;
                }
            }
        }

        public static void Add(string connectionId)
        {
            lock (_connections)
            {
                _connections.Add(connectionId);
            }
        }

        public static void Remove(string connectionId)
        {
            lock (_connections)
            {
                _connections.Remove(connectionId);
            }
        }
    }
}
