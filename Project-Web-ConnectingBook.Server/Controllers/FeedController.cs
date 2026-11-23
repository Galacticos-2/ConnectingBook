using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_Web_ConnectingBook.Server.Contracts;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Services;

namespace Project_Web_ConnectingBook.Server.Controllers
{
    [ApiController, Route("api/feed"), Authorize]
    public class FeedController(FeedService feed) : ControllerBase
    {
        [HttpGet]
        public Task<Page<PostDto>> List([FromQuery] int limit = 20, [FromQuery] string? cursor = null, CancellationToken ct = default) =>
            feed.ListAsync("feed", User.UserId(), null, limit, cursor, ct);
    }
}
