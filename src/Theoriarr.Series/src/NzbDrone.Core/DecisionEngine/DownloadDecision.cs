using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine
{
    public class DownloadDecision
    {
        public IRemoteSubject Remote { get; private set; }

        public RemoteEpisode RemoteEpisode => Remote as RemoteEpisode;
        public RemoteMovie RemoteMovie => Remote as RemoteMovie;

        public IEnumerable<DownloadRejection> Rejections { get; private set; }

        public bool Approved => !Rejections.Any();

        public bool TemporarilyRejected
        {
            get
            {
                return Rejections.Any() && Rejections.All(r => r.Type == RejectionType.Temporary);
            }
        }

        public bool Rejected
        {
            get
            {
                return Rejections.Any() && Rejections.Any(r => r.Type == RejectionType.Permanent);
            }
        }

        public DownloadDecision(RemoteEpisode episode, params DownloadRejection[] rejections)
        {
            Remote = episode;
            Rejections = rejections.ToList();
        }

        public DownloadDecision(RemoteMovie movie, params DownloadRejection[] rejections)
        {
            Remote = movie;
            Rejections = rejections.ToList();
        }

        public override string ToString()
        {
            if (Approved)
            {
                return "[OK] " + Remote;
            }

            return "[Rejected " + Rejections.Count() + "]" + Remote;
        }
    }
}
