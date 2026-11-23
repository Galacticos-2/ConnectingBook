using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Contracts;
using Project_Web_ConnectingBook.Server.Data;
using Project_Web_ConnectingBook.Server.Infrastructure;

namespace Project_Web_ConnectingBook.Server.Services
{
    public class FeedService(ConnectingBookDbContext db, PostService posts)
    {
        public async Task<Page<PostDto>> ListAsync(string kind, Guid me, Guid? author, int limit, string? cursor, CancellationToken ct)
        {
            limit = CursorCodec.Limit(limit);
            var marker = CursorCodec.Decode(cursor, kind);
            var query = db.Posts.AsNoTracking().AsQueryable();
            if (kind == "own") query = query.Where(x => x.AuthorId == me && x.Status != "Deleted");
            else
            {
                query = query.Where(x => x.Status == "Visible");
                if (kind == "feed") query = query.Where(x => x.AuthorId == me || db.Follows.Any(f => f.FollowerId == me && f.FollowedId == x.AuthorId));
                if (kind == "reels") query = query.Where(x => x.Type == "Reel");
                if (kind == "profile")
                {
                    if (!await db.Users.AnyAsync(x => x.Id == author, ct)) throw new ApiException(404, "user_not_found", "Không tìm thấy hồ sơ.");
                    query = query.Where(x => x.AuthorId == author);
                }
            }
            if (marker is not null)
            {
                var id = long.Parse(marker.Id);
                query = kind == "own"
                    ? query.Where(x => x.CreatedAt < marker.At || (x.CreatedAt == marker.At && x.Id < id))
                    : query.Where(x => x.PublishedAt < marker.At || (x.PublishedAt == marker.At && x.Id < id));
            }
            query = kind == "own" ? query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                : query.OrderByDescending(x => x.PublishedAt).ThenByDescending(x => x.Id);
            var items = await query.Take(limit + 1).ToListAsync(ct);
            var hasNext = items.Count > limit;
            if (hasNext) items.RemoveAt(limit);
            var last = items.LastOrDefault();
            var next = hasNext && last is not null ? CursorCodec.Encode(kind, kind == "own" ? last.CreatedAt : last.PublishedAt!.Value, last.Id) : null;
            return new Page<PostDto>(await posts.ViewsAsync(items, me, kind == "own", ct), next);
        }
    }
}
