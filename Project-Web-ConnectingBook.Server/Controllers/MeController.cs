using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project_Web_ConnectingBook.Server.Contracts;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Services;

namespace Project_Web_ConnectingBook.Server.Controllers
{
    [ApiController, Route("api/me"), Authorize]
    public class MeController(ProfileService profiles, FeedService feed) : ControllerBase
    {
        [HttpPatch("profile")]
        public Task<object> Update(ProfileRequest request, CancellationToken ct) => profiles.UpdateAsync(User.UserId(), request, ct);
        [HttpGet("posts")]
        public Task<Page<PostDto>> Posts([FromQuery] int limit = 20, [FromQuery] string? cursor = null, CancellationToken ct = default) =>
            feed.ListAsync("own", User.UserId(), null, limit, cursor, ct);
    }
}
