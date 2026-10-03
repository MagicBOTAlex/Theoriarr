using System;
using System.Threading;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public interface ITranscodeRunner
    {
        TranscodeResult Run(TranscodePlan plan, Action<TranscodeProgress> onProgress, CancellationToken cancellationToken);
    }
}
