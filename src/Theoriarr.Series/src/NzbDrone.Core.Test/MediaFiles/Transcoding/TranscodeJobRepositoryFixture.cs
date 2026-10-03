using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MediaFiles.Transcoding;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.Transcoding
{
    [TestFixture]
    public class TranscodeJobRepositoryFixture : DbTest<TranscodeJobRepository, TranscodeJob>
    {
        [Test]
        public void get_recent_including_active_should_return_all_active_and_newest_terminal()
        {
            var oldestActive = Insert(TranscodeJobStatus.Queued);
            Insert(TranscodeJobStatus.Completed);
            Insert(TranscodeJobStatus.Failed);
            var newestTerminal = Insert(TranscodeJobStatus.Completed);
            var newestActive = Insert(TranscodeJobStatus.Running);

            var result = Subject.GetRecentIncludingActive(2);

            result.Select(job => job.Id).Should().BeInDescendingOrder();
            result.Select(job => job.Id).Should().Contain(new[] { oldestActive.Id, newestActive.Id, newestTerminal.Id });
            result.Count(job => job.Status == TranscodeJobStatus.Completed).Should().Be(1);
        }

        [Test]
        public void get_recent_including_active_should_return_every_active_job_beyond_the_limit()
        {
            for (var index = 0; index < 5; index++)
            {
                Insert(TranscodeJobStatus.Queued);
            }

            var result = Subject.GetRecentIncludingActive(2);

            result.Should().HaveCount(5);
        }

        [Test]
        public void merge_recent_including_active_should_not_duplicate_a_job_that_finalised_between_reads()
        {
            var active = new[] { new TranscodeJob { Id = 5, Status = TranscodeJobStatus.Running } };
            var terminal = new[] { new TranscodeJob { Id = 5, Status = TranscodeJobStatus.Completed } };

            var result = TranscodeJobRepository.MergeRecentIncludingActive(active, terminal);

            result.Should().HaveCount(1);
            result[0].Id.Should().Be(5);
            result[0].Status.Should().Be(TranscodeJobStatus.Completed);
        }

        [Test]
        public void merge_recent_including_active_should_order_newest_first()
        {
            var active = new[] { new TranscodeJob { Id = 1, Status = TranscodeJobStatus.Queued } };
            var terminal = new[] { new TranscodeJob { Id = 9, Status = TranscodeJobStatus.Completed }, new TranscodeJob { Id = 3, Status = TranscodeJobStatus.Failed } };

            var result = TranscodeJobRepository.MergeRecentIncludingActive(active, terminal);

            result.Select(job => job.Id).Should().ContainInOrder(9, 3, 1);
        }

        [Test]
        public void get_terminal_should_return_only_terminal_statuses()
        {
            Insert(TranscodeJobStatus.Queued);
            Insert(TranscodeJobStatus.Running);
            Insert(TranscodeJobStatus.Transferring);
            Insert(TranscodeJobStatus.AwaitingReview);
            var completed = Insert(TranscodeJobStatus.Completed);
            var failed = Insert(TranscodeJobStatus.Failed);
            var cancelled = Insert(TranscodeJobStatus.Cancelled);
            var skipped = Insert(TranscodeJobStatus.Skipped);

            var result = Subject.GetTerminal();

            result.Select(job => job.Id)
                  .Should()
                  .BeEquivalentTo(new[] { completed.Id, failed.Id, cancelled.Id, skipped.Id });
        }

        private TranscodeJob Insert(TranscodeJobStatus status)
        {
            return Subject.Insert(new TranscodeJob
            {
                Status = status,
                SourcePath = "/media/show/" + status + "-" + Guid.NewGuid() + ".mkv"
            });
        }
    }
}
