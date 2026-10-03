using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.Transcoding;

namespace NzbDrone.Core.Test.MediaFiles.Transcoding
{
    [TestFixture]
    public class TranscodeInputFixture
    {
        [TestCase(null, null)]
        [TestCase(0, null)]
        [TestCase(-5, null)]
        [TestCase(1, 2)]
        [TestCase(2, 2)]
        [TestCase(3, 4)]
        [TestCase(1080, 1080)]
        [TestCase(1081, 1082)]
        public void normalize_max_height_should_round_up_to_even_and_floor_at_two(int? input, int? expected)
        {
            TranscodeInput.NormalizeMaxHeight(input).Should().Be(expected);
        }

        [TestCase(null, true)]
        [TestCase("", true)]
        [TestCase("   ", true)]
        [TestCase("-rc-lookahead 20", true)]
        [TestCase("-rc-lookahead \"20\"", false)]
        [TestCase("-x 'y'", false)]
        [TestCase("-qp 25\n-qp 30", false)]
        [TestCase("-qp 25\r\n-qp 30", false)]
        public void is_valid_extra_args_should_reject_quotes_and_line_breaks(string extraArgs, bool expected)
        {
            TranscodeInput.IsValidExtraArgs(extraArgs).Should().Be(expected);
        }
    }
}
