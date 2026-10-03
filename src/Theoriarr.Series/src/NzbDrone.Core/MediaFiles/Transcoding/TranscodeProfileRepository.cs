using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public interface ITranscodeProfileRepository : IBasicRepository<TranscodeProfile>
    {
    }

    public class TranscodeProfileRepository : BasicRepository<TranscodeProfile>, ITranscodeProfileRepository
    {
        public TranscodeProfileRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
