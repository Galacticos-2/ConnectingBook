using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Data;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Models;

namespace Project_Web_ConnectingBook.Server.Services
{
    public record CommentRequest(string Text);
    public record CommentDto(string Id, string PostId, object Author, string Text, DateTime CreatedAt, DateTime? EditedAt);

    public class InteractionService(ConnectingBookDbContext db, PostService posts)
    {
        public async Task LikeAsync(long id, Guid me, bool like, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            EnsureVisible(await posts.LockAsync(id, ct));
            var existing = await db.Reactions.FindAsync([id, me], ct);
            if (like && existing is null) db.Reactions.Add(new Reaction { PostId = id, UserId = me });
            if (!like && existing is not null) db.Reactions.Remove(existing);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        public async Task<Page<CommentDto>> ListAsync(long postId, int limit, string? cursor, CancellationToken ct)
        {
            limit = CursorCodec.Limit(limit);
            var marker = CursorCodec.Decode(cursor, "comments");
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            EnsureVisible(await posts.LockAsync(postId, ct));
            var query = db.Comments.AsNoTracking().Where(x => x.PostId == postId && x.DeletedAt == null);
            if (marker is not null) { var id = long.Parse(marker.Id); query = query.Where(x => x.CreatedAt > marker.At || (x.CreatedAt == marker.At && x.Id > id)); }
            var rows = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(limit + 1).ToListAsync(ct);
            var more = rows.Count > limit;
            if (more) rows.RemoveAt(limit);
            var result = new List<CommentDto>();
            foreach (var row in rows) result.Add(await ViewAsync(row, ct));
            await transaction.CommitAsync(ct);
            return new Page<CommentDto>(result, more ? CursorCodec.Encode("comments", rows[^1].CreatedAt, rows[^1].Id) : null);
        }

        public async Task<CommentDto> CreateAsync(long id, Guid me, CommentRequest request, CancellationToken ct)
        {
            var text = Validate(request.Text);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            EnsureVisible(await posts.LockAsync(id, ct));
            var comment = new Comment { PostId = id, AuthorId = me, Text = text };
            db.Comments.Add(comment);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return await ViewAsync(comment, ct);
        }

        public async Task<CommentDto?> ChangeAsync(long id, Guid me, string? text, bool delete, CancellationToken ct)
        {
            var postId = await db.Comments.Where(x => x.Id == id && x.DeletedAt == null).Select(x => (long?)x.PostId).SingleOrDefaultAsync(ct)
                ?? throw new ApiException(404, "comment_not_found", "Không tìm thấy bình luận.");
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            EnsureVisible(await posts.LockAsync(postId, ct));
            var comment = await db.Comments.SingleAsync(x => x.Id == id, ct);
            if (comment.DeletedAt is not null) throw new ApiException(404, "comment_not_found", "Không tìm thấy bình luận.");
            if (comment.AuthorId != me) throw new ApiException(403, "not_owner", "Bạn không có quyền sửa bình luận này.");
            if (delete) comment.DeletedAt = DateTime.UtcNow;
            else { comment.Text = Validate(text); comment.EditedAt = DateTime.UtcNow; }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return delete ? null : await ViewAsync(comment, ct);
        }

        private async Task<CommentDto> ViewAsync(Comment comment, CancellationToken ct)
        {
            var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == comment.AuthorId, ct);
            return new CommentDto(comment.Id.ToString(), comment.PostId.ToString(), new { user.Id, user.DisplayName, user.AvatarMediaId }, comment.Text, comment.CreatedAt, comment.EditedAt);
        }
        private static string Validate(string? text) => text?.Trim() is string value && value.Length is >= 1 and <= 1000
            ? value : throw new ApiException(400, "invalid_comment", "Bình luận phải có từ 1 đến 1000 ký tự.");
        private static void EnsureVisible(Post post) { if (post.Status != "Visible") throw new ApiException(404, "post_not_found", "Không tìm thấy nội dung."); }
    }
}
