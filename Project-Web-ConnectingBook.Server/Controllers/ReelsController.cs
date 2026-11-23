using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Project_Web_ConnectingBook.Server.Contracts;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Services;

namespace Project_Web_ConnectingBook.Server.Controllers
{
    [ApiController, Route("api/reels"), Authorize]
    public class ReelsController(ReelService reels, FeedService feed) : ControllerBase
    {
        [HttpPost, EnableRateLimiting("reels"), RequestSizeLimit(52 * 1024 * 1024)]
        public async Task<IActionResult> Upload(IFormFile file, [FromForm] string caption = "", CancellationToken ct = default) =>
            StatusCode(202, await reels.UploadAsync(User.UserId(), file, caption, ct));
        [HttpGet]
        public Task<Page<PostDto>> List([FromQuery] int limit = 20, [FromQuery] string? cursor = null, CancellationToken ct = default) => feed.ListAsync("reels", User.UserId(), null, limit, cursor, ct);
    }
}
