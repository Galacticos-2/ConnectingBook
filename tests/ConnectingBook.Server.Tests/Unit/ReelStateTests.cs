using Project_Web_ConnectingBook.Server.Models;
using Project_Web_ConnectingBook.Server.Workers;

namespace ConnectingBook.Server.Tests.Unit
{
    public class ReelStateTests
    {
        [Fact]
        public void Active_job_is_not_claimed_twice_and_deleted_reel_never_publishes()
        {
            var now = DateTime.UtcNow;
            var post = new Post { Type = "Reel", Status = "Processing" };
            Assert.True(ReelPolicy.TryClaim(post, now));
            Assert.Equal(1, post.ProcessingAttempts);
            Assert.False(ReelPolicy.TryClaim(post, now.AddMinutes(1)));
            post.Status = "Deleted"; post.DeletedAt = now;
            Assert.False(ReelPolicy.TryClaim(post, now.AddHours(1)));
        }

        [Fact]
        public void Restart_can_reclaim_stale_job_but_total_attempts_are_bounded()
        {
            var now = DateTime.UtcNow;
            var post = new Post { Type = "Reel", Status = "Processing", ProcessingAttempts = 1, ProcessingStartedAt = now.AddMinutes(-11) };
            Assert.True(ReelPolicy.TryClaim(post, now));
            Assert.Equal(2, post.ProcessingAttempts);
            Assert.False(ReelPolicy.TryClaim(post, now.AddMinutes(11)));
            Assert.Equal("Failed", post.Status);
            Assert.NotNull(post.FailureCode);
            Assert.Null(post.PublishedAt);
        }
    }
}
