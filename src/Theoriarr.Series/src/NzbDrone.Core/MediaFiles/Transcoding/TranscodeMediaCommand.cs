namespace NzbDrone.Core.MediaFiles.Transcoding
{
    // Thin command entry point so transcode work is visible in System -> Tasks and can be started
    // through POST /api/v3/command. The heavy work runs on the TranscodeService scheduler.
    public class TranscodeMediaCommand : Messaging.Commands.Command
    {
        public override bool SendUpdatesToClient => true;
        public override bool IsLongRunning => true;
        public override string CompletionMessage => "Scheduled";
    }
}
