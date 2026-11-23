using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Data;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Services;
using Project_Web_ConnectingBook.Server.Storage;

namespace Project_Web_ConnectingBook.Server.Controllers
{
    [ApiController, Route("api/media"), Authorize]
    public class MediaController(ImageService images, ConnectingBookDbContext db, IStorage storage) : ControllerBase
    {
        [HttpPost("images"), EnableRateLimiting("images"), RequestSizeLimit(6 * 1024 * 1024)]
        public async Task<IActionResult> Upload(IFormFile file, [FromForm] string purpose = "post", CancellationToken ct = default)
        {
            if (purpose is not ("post" or "avatar")) throw new ApiException(400, "invalid_purpose", "Loại ảnh không hợp lệ.");
            return StatusCode(201, await images.UploadAsync(User.UserId(), file, purpose == "avatar", ct));
        }

        [HttpGet("{id:guid}")]
        public async Task Get(Guid id, CancellationToken ct)
        {
            var media = await db.Media.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new ApiException(404, "media_not_found", "Không tìm thấy tệp.");
            var permission = media;
            if (media.ParentMediaId is Guid parentId)
                permission = await db.Media.AsNoTracking().SingleOrDefaultAsync(x => x.Id == parentId, ct)
                    ?? throw new ApiException(404, "media_not_found", "Không tìm thấy tệp.");
            var me = User.UserId();
            var allowed = false;
            if (permission.PostId is long postId)
            {
                var post = await db.Posts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == postId, ct);
                allowed = post is not null && post.Status != "Deleted" && (post.Status == "Visible" || post.AuthorId == me ||
                    (User.IsInRole("Admin") && await db.Reports.AnyAsync(x => x.PostId == postId, ct)));
            }
            else
                allowed = permission.OwnerId == me || (permission.Kind == "Avatar" && await db.Users.AnyAsync(x => x.AvatarMediaId == permission.Id, ct));
            if (!allowed) throw new ApiException(404, "media_not_found", "Không tìm thấy tệp.");
            await MediaResponse.WriteAsync(HttpContext, storage, media.StorageKey, media.ContentType, ct);
        }
    }

    public static class MediaResponse
    {
        public static async Task WriteAsync(HttpContext context, IStorage storage, string key, string contentType, CancellationToken ct)
        {
            var stat = await storage.StatAsync(key, ct) ?? throw new ApiException(404, "media_not_found", "Không tìm thấy tệp.");
            var etag = '"' + stat.ETag + '"';
            context.Response.Headers.CacheControl = "private, max-age=60";
            context.Response.Headers.ETag = etag;
            context.Response.Headers.AcceptRanges = "bytes";
            if (context.Request.Headers.IfNoneMatch.ToString() == etag) { context.Response.StatusCode = 304; return; }
            long offset = 0, length = stat.Size;
            var range = context.Request.Headers.Range.ToString();
            if (!string.IsNullOrEmpty(range) && (context.Request.Headers.IfRange.Count == 0 || context.Request.Headers.IfRange.ToString() == etag))
            {
                if (!Microsoft.Net.Http.Headers.RangeHeaderValue.TryParse(range, out var parsed) || parsed.Unit != "bytes" || parsed.Ranges.Count != 1)
                { context.Response.StatusCode = 416; context.Response.Headers.ContentRange = $"bytes */{stat.Size}"; return; }
                var item = parsed.Ranges.Single();
                offset = item.From ?? Math.Max(0, stat.Size - (item.To ?? 0));
                var end = item.From is null ? stat.Size - 1 : Math.Min(item.To ?? stat.Size - 1, stat.Size - 1);
                if (offset >= stat.Size || end < offset || (item.From is null && item.To == 0))
                { context.Response.StatusCode = 416; context.Response.Headers.ContentRange = $"bytes */{stat.Size}"; return; }
                length = end - offset + 1;
                context.Response.StatusCode = 206;
                context.Response.Headers.ContentRange = $"bytes {offset}-{end}/{stat.Size}";
            }
            context.Response.ContentType = contentType;
            context.Response.ContentLength = length;
            await storage.CopyToAsync(key, context.Response.Body, offset, length, ct);
        }
    }
}
