using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Project_Web_ConnectingBook.Server.Contracts;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Services;

namespace Project_Web_ConnectingBook.Server.Controllers
{
    [ApiController, Route("api/posts"), Authorize]
    public class PostsController(PostService posts, InteractionService interactions) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> Create(CreatePostRequest request, CancellationToken ct) => StatusCode(201, await posts.CreateAsync(User.UserId(), request, ct));
        [HttpGet("{id:long}")]
        public Task<PostDto> Get(long id, CancellationToken ct) => posts.GetAsync(id, User.UserId(), ct);
        [HttpPatch("{id:long}")]
        public Task<PostDto> Update(long id, UpdatePostRequest request, CancellationToken ct) => posts.UpdateAsync(id, User.UserId(), Request.Headers.IfMatch, request, ct);
        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id, CancellationToken ct)
        {
            await posts.DeleteAsync(id, User.UserId(), Request.Headers.IfMatch, ct);
            return NoContent();
        }
        [HttpPut("{id:long}/like"), EnableRateLimiting("interactions")]
        public async Task<IActionResult> Like(long id, CancellationToken ct) { await interactions.LikeAsync(id, User.UserId(), true, ct); return NoContent(); }
        [HttpDelete("{id:long}/like"), EnableRateLimiting("interactions")]
        public async Task<IActionResult> Unlike(long id, CancellationToken ct) { await interactions.LikeAsync(id, User.UserId(), false, ct); return NoContent(); }
        [HttpGet("{id:long}/comments")]
        public Task<Page<CommentDto>> Comments(long id, [FromQuery] int limit = 20, [FromQuery] string? cursor = null, CancellationToken ct = default) => interactions.ListAsync(id, limit, cursor, ct);
        [HttpPost("{id:long}/comments"), EnableRateLimiting("interactions")]
        public async Task<IActionResult> Comment(long id, CommentRequest request, CancellationToken ct) => StatusCode(201, await interactions.CreateAsync(id, User.UserId(), request, ct));
    }
}
