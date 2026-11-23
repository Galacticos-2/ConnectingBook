using System.Threading.Channels;
using Project_Web_ConnectingBook.Server.Models;

namespace Project_Web_ConnectingBook.Server.Workers
{
    public class ReelQueue
    {
        public Channel<long> Channel { get; } = System.Threading.Channels.Channel.CreateBounded<long>(new BoundedChannelOptions(100)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public static class ReelPolicy
    {
        public static bool TryClaim(Post post, DateTime now)
        {
            if (post.Type != "Reel" || post.Status != "Processing" || post.ProcessingStartedAt > now.AddMinutes(-10)) return false;
            if (post.ProcessingAttempts >= 2)
            {
                post.Status = "Failed"; post.FailureCode = "processing_attempts_exhausted"; post.UpdatedAt = now;
                return false;
            }
            post.ProcessingAttempts++; post.ProcessingStartedAt = now;
            return true;
        }
    }
}
