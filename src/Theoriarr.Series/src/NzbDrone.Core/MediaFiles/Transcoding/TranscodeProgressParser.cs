using System;
using System.Globalization;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.MediaFiles.Transcoding
{
    // Parses ffmpeg's `-progress pipe:1` key=value stream into progress snapshots. Stateful because
    // ffmpeg emits one block per interval, then emits `progress=continue|end` to close the block.
    public class TranscodeProgressParser
    {
        private readonly double _durationSeconds;

        private long _outTimeUs;
        private string _speed;
        private string _fps;
        private long _outputSize;

        public TranscodeProgressParser(double durationSeconds)
        {
            _durationSeconds = durationSeconds;
        }

        public TranscodeProgress Parse(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            var separator = line.IndexOf('=');

            if (separator <= 0)
            {
                return null;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            switch (key)
            {
                case "out_time_us":
                case "out_time_ms":
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var time))
                    {
                        _outTimeUs = time;
                    }

                    break;
                case "speed":
                    _speed = value;
                    break;
                case "fps":
                    _fps = value;
                    break;
                case "total_size":
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
                    {
                        _outputSize = size;
                    }

                    break;
                case "progress":
                    return Build(value == "end");
            }

            return null;
        }

        private TranscodeProgress Build(bool completed)
        {
            var seconds = _outTimeUs / 1_000_000.0;
            var percent = _durationSeconds > 0
                ? Math.Min(100, Math.Max(0, seconds / _durationSeconds * 100))
                : 0;

            return new TranscodeProgress
            {
                Percent = percent,
                Speed = _speed,
                Fps = _fps,
                Eta = BuildEta(seconds),
                OutputSize = _outputSize,
                Completed = completed
            };
        }

        private string BuildEta(double currentSeconds)
        {
            if (_durationSeconds <= 0 || _speed.IsNullOrWhiteSpace())
            {
                return null;
            }

            var multiplier = double.TryParse(_speed.TrimEnd('x'), NumberStyles.Float, CultureInfo.InvariantCulture, out var speed) && speed > 0
                ? speed
                : 0;

            if (multiplier <= 0)
            {
                return null;
            }

            var remaining = Math.Max(0, (_durationSeconds - currentSeconds) / multiplier);

            return TimeSpan.FromSeconds(remaining).ToString(@"hh\:mm\:ss");
        }
    }
}
