namespace NzbDrone.Core.MediaFiles.Transcoding
{
    public enum TranscodeReviewAction
    {
        Overwrite = 0,
        KeepBoth = 1,
        Discard = 2
    }

    public enum TranscodeCodec
    {
        H264 = 0,
        Hevc = 1,
        Av1 = 2
    }
}
