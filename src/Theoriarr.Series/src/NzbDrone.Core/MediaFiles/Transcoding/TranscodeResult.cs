namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public class TranscodeResult
    {
        public bool Success { get; set; }
        public string OutputPath { get; set; }
        public long OutputSize { get; set; }
        public string Error { get; set; }
    }
}
