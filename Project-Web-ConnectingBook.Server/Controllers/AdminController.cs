using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Contracts;
using Project_Web_ConnectingBook.Server.Data;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Services;
using Project_Web_ConnectingBook.Server.Storage;

namespace Project_Web_ConnectingBook.Server.Controllers
{
    [ApiController, Route("api/admin"), Authorize(Roles = "Admin")]
    public class AdminController(ModerationService moderation, ConnectingBookDbContext db, IStorage storage) : ControllerBase
    {
        [HttpGet("report-groups")]
        public Task<Page<object>> Groups([FromQuery] int limit = 20, [FromQuery] string? cursor = null, CancellationToken ct = default) => moderation.QueueAsync(limit, cursor, ct);
        [HttpGet("report-groups/{postId:long}")]
        public Task<object> Detail(long postId, CancellationToken ct) => moderation.DetailAsync(postId, User.UserId(), ct);
        [HttpPost("report-groups/{postId:long}/decision")]
        public Task<object> Decision(long postId, DecisionRequest request, CancellationToken ct) => moderation.DecideAsync(postId, User.UserId(), request, ct);
        [HttpPost("posts/{id:long}/restore")]
        public Task<object> Restore(long id, RestoreRequest request, CancellationToken ct) => moderation.RestoreAsync(id, User.UserId(), Request.Headers.IfMatch, request.Reason, ct);
        [HttpGet("posts/{id:long}/moderation-history")]
        public Task<Page<object>> History(long id, [FromQuery] int limit = 20, [FromQuery] string? cursor = null, CancellationToken ct = default) => moderation.HistoryAsync(id, limit, cursor, ct);
        [HttpGet("reports/{reportId:long}/media/{mediaId:guid}")]
        public async Task Snapshot(long reportId, Guid mediaId, CancellationToken ct)
        {
            var report = await db.Reports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == reportId, ct);
            if (report is null || !report.SnapshotMediaIds.Contains(mediaId)) throw new ApiException(404, "snapshot_not_found", "Không tìm thấy tệp bản chụp.");
            var media = await db.Media.AsNoTracking().SingleOrDefaultAsync(x => x.Id == mediaId, ct)
                ?? throw new ApiException(404, "snapshot_not_found", "Không tìm thấy tệp bản chụp.");
            await MediaResponse.WriteAsync(HttpContext, storage, media.StorageKey, media.ContentType, ct);
        }
    }
}
