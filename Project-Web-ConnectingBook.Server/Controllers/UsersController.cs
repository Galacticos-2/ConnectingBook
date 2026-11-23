using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Contracts;
using Project_Web_ConnectingBook.Server.Services;

namespace Project_Web_ConnectingBook.Server.Controllers
{
    [ApiController, Route("api/users"), Authorize]
    public class UsersController(ProfileService profiles, FollowService follows, FeedService feed) : ControllerBase
    {
        [HttpGet("{id:guid}")]
        public Task<object> Get(Guid id, CancellationToken ct) => profiles.GetAsync(id, User.UserId(), ct);
        [HttpGet("{id:guid}/posts")]
        public Task<Page<PostDto>> Posts(Guid id, [FromQuery] int limit = 20, [FromQuery] string? cursor = null, CancellationToken ct = default) =>
            feed.ListAsync("profile", User.UserId(), id, limit, cursor, ct);
        [HttpPut("{id:guid}/follow"), EnableRateLimiting("interactions")]
        public async Task<IActionResult> Follow(Guid id, CancellationToken ct) { await follows.SetAsync(User.UserId(), id, true, ct); return NoContent(); }
        [HttpDelete("{id:guid}/follow"), EnableRateLimiting("interactions")]
        public async Task<IActionResult> Unfollow(Guid id, CancellationToken ct) { await follows.SetAsync(User.UserId(), id, false, ct); return NoContent(); }
    }
}
