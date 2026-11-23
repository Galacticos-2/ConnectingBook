using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Services;

namespace Project_Web_ConnectingBook.Server.Controllers
{
    [ApiController, Route("api/comments"), Authorize, EnableRateLimiting("interactions")]
    public class CommentsController(InteractionService interactions) : ControllerBase
    {
        [HttpPatch("{id:long}")]
        public async Task<CommentDto?> Edit(long id, CommentRequest request, CancellationToken ct) =>
            await interactions.ChangeAsync(id, User.UserId(), request.Text, false, ct);
        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id, CancellationToken ct) { await interactions.ChangeAsync(id, User.UserId(), null, true, ct); return NoContent(); }
    }
}
