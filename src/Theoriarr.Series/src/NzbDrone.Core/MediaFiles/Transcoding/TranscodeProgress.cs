namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public class TranscodeProgress
    {
        public double Percent { get; set; }
        public string Speed { get; set; }
        public string Fps { get; set; }
        public string Eta { get; set; }
        public long OutputSize { get; set; }
        public bool Completed { get; set; }
    }
}
