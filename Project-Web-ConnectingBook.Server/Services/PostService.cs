using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Contracts;
using Project_Web_ConnectingBook.Server.Data;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Models;

namespace Project_Web_ConnectingBook.Server.Services
{
    public class PostService(ConnectingBookDbContext db)
    {
        public async Task<Post> LockAsync(long id, CancellationToken ct) =>
            await db.Posts.FromSqlInterpolated($"SELECT * FROM [Posts] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id}").SingleOrDefaultAsync(ct)
                ?? throw new ApiException(404, "post_not_found", "Không tìm thấy nội dung.");

        public static void MatchVersion(Post post, string? version)
        {
            if (string.IsNullOrWhiteSpace(version)) throw new ApiException(400, "version_required", "Thiếu phiên bản nội dung.");
            if (version.Trim('"') != Convert.ToBase64String(post.Version)) throw new ApiException(409, "write_conflict", "Nội dung đã thay đổi. Vui lòng tải lại.");
        }

        public async Task<PostDto> GetAsync(long id, Guid me, CancellationToken ct)
        {
            var post = await db.Posts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Status == "Visible", ct)
                ?? throw new ApiException(404, "post_not_found", "Không tìm thấy nội dung.");
            return (await ViewsAsync([post], me, false, ct))[0];
        }

        public async Task<PostDto> CreateAsync(Guid me, CreatePostRequest request, CancellationToken ct)
        {
            var images = request.ImageIds ?? [];
            var text = request.Text?.Trim() ?? "";
            ValidatePost(text, images);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var post = new Post { AuthorId = me, Text = text, PublishedAt = DateTime.UtcNow };
            db.Posts.Add(post);
            await db.SaveChangesAsync(ct);
            await SetImagesAsync(post, images, me, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (await ViewsAsync([post], me, true, ct))[0];
        }

        public async Task<PostDto> UpdateAsync(long id, Guid me, string? version, UpdatePostRequest request, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var post = await LockAsync(id, ct);
            EnsureOwner(post, me);
            if (post.Status == "Hidden") throw new ApiException(409, "post_hidden", "Không thể sửa nội dung đang bị ẩn.");
            if (post.Status != "Visible") throw new ApiException(409, "post_not_published", "Nội dung chưa được phát hành.");
            MatchVersion(post, version);
            if (post.Type == "Reel")
            {
                if (request.Text is not null || request.ImageIds is not null || request.Caption is null)
                    throw new ApiException(400, "invalid_reel_update", "Reel chỉ cho phép sửa mô tả.");
                post.Text = request.Caption.Trim();
            }
            else
            {
                if (request.Caption is not null) throw new ApiException(400, "invalid_post_update", "Bài viết không có trường caption.");
                var images = request.ImageIds ?? await db.Media.Where(x => x.PostId == id && x.Kind == "Image").OrderBy(x => x.Position).Select(x => x.Id).ToArrayAsync(ct);
                var text = request.Text?.Trim() ?? post.Text;
                ValidatePost(text, images);
                if (request.ImageIds is not null) await SetImagesAsync(post, images, me, ct);
                post.Text = text;
            }
            post.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (await ViewsAsync([post], me, true, ct))[0];
        }

        public async Task DeleteAsync(long id, Guid me, string? version, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var post = await LockAsync(id, ct);
            EnsureOwner(post, me);
            MatchVersion(post, version);
            var prior = post.Status;
            post.Status = "Deleted"; post.DeletedAt = DateTime.UtcNow;
            var pending = await db.Reports.Where(x => x.PostId == id && x.Status == "Pending").ToListAsync(ct);
            if (pending.Count > 0)
            {
                var action = new ModerationAction { PostId = id, Decision = "AuthorDeleted", Reason = "Tác giả đã xóa nội dung.", PreviousStatus = prior, NextStatus = "Deleted" };
                db.ModerationActions.Add(action);
                await db.SaveChangesAsync(ct);
                foreach (var report in pending) { report.Status = "ClosedAuthorDeleted"; report.ClosedAt = DateTime.UtcNow; report.ModerationActionId = action.Id; }
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }

        private static void EnsureOwner(Post post, Guid me)
        {
            if (post.Status == "Deleted" || (post.AuthorId != me && post.Status != "Visible")) throw new ApiException(404, "post_not_found", "Không tìm thấy nội dung.");
            if (post.AuthorId != me) throw new ApiException(403, "not_owner", "Bạn không có quyền sửa nội dung này.");
        }

        private static void ValidatePost(string text, Guid[] images)
        {
            if (text.Length > 5000 || images.Length > 4 || images.Distinct().Count() != images.Length || (text.Length == 0 && images.Length == 0))
                throw new ApiException(400, "invalid_post", "Bài viết cần nội dung hoặc ảnh, tối đa 4 ảnh và 5000 ký tự.");
        }

        private async Task SetImagesAsync(Post post, Guid[] imageIds, Guid me, CancellationToken ct)
        {
            var images = await db.Media.FromSqlRaw("SELECT * FROM [Media] WITH (UPDLOCK, ROWLOCK)")
                .Where(x => imageIds.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(ct);
            if (images.Count != imageIds.Length || images.Any(x => x.OwnerId != me || x.Kind != "Image" || (x.PostId != null && x.PostId != post.Id)))
                throw new ApiException(400, "invalid_images", "Ảnh không hợp lệ hoặc không thuộc về bạn.");
            var old = await db.Media.Where(x => x.PostId == post.Id && x.Kind == "Image").ToListAsync(ct);
            foreach (var image in old) { image.PostId = null; image.Position = null; }
            await db.SaveChangesAsync(ct);
            for (var i = 0; i < imageIds.Length; i++)
            {
                var image = images.Single(x => x.Id == imageIds[i]); image.PostId = post.Id; image.Position = i;
            }
        }

        public async Task<IReadOnlyList<PostDto>> ViewsAsync(IReadOnlyList<Post> posts, Guid me, bool own, CancellationToken ct)
        {
            var ids = posts.Select(x => x.Id).ToArray();
            var authorIds = posts.Select(x => x.AuthorId).Distinct().ToArray();
            var authors = await db.Users.AsNoTracking().Where(x => authorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            var media = await db.Media.AsNoTracking().Where(x => x.PostId != null && ids.Contains(x.PostId.Value) && (x.Kind == "Image" || x.Kind == "Video")).OrderBy(x => x.Position).ToListAsync(ct);
            var mediaIds = media.Select(x => x.Id).ToArray();
            var thumbs = await db.Media.AsNoTracking().Where(x => x.ParentMediaId != null && mediaIds.Contains(x.ParentMediaId.Value)).ToListAsync(ct);
            var likes = await db.Reactions.Where(x => ids.Contains(x.PostId)).GroupBy(x => x.PostId).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
            var comments = await db.Comments.Where(x => ids.Contains(x.PostId) && x.DeletedAt == null).GroupBy(x => x.PostId).Select(x => new { Id = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count, ct);
            var myLikes = await db.Reactions.Where(x => ids.Contains(x.PostId) && x.UserId == me).Select(x => x.PostId).ToListAsync(ct);
            return posts.Select(post => new PostDto(post.Id.ToString(), new AuthorDto(post.AuthorId, authors[post.AuthorId].DisplayName, authors[post.AuthorId].AvatarMediaId), post.Type, post.Text,
                media.Where(x => x.PostId == post.Id).Select(x => new MediaDto(x.Id, thumbs.FirstOrDefault(t => t.ParentMediaId == x.Id)?.Id, x.Kind, x.Width, x.Height)).ToList(),
                post.PublishedAt, post.CreatedAt, post.UpdatedAt, likes.GetValueOrDefault(post.Id), comments.GetValueOrDefault(post.Id), myLikes.Contains(post.Id), Convert.ToBase64String(post.Version),
                own ? post.Status : null, own ? post.ModerationReason : null, own ? post.FailureCode : null)).ToList();
        }
    }
}
