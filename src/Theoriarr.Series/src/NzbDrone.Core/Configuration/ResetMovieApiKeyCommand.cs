using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Configuration
{
    public class ResetMovieApiKeyCommand : Command
    {
        public override bool SendUpdatesToClient => true;
    }
}
