using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Project_Web_ConnectingBook.Server.Contracts;
using Project_Web_ConnectingBook.Server.Data;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Models;

namespace Project_Web_ConnectingBook.Server.Services
{
    public class ModerationService(ConnectingBookDbContext db, PostService posts)
    {
        public async Task<object> ReportAsync(long id, Guid me, ReportRequest request, CancellationToken ct)
        {
            if (request.Reason is not ("Spam" or "Harassment" or "Inappropriate" or "Other") ||
                (request.Reason == "Other" && string.IsNullOrWhiteSpace(request.Description)))
                throw new ApiException(400, "invalid_report", "Vui lòng chọn lý do và mô tả cho lý do khác.");
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var post = await posts.LockAsync(id, ct);
            if (post.Status != "Visible") throw new ApiException(404, "post_not_found", "Không tìm thấy nội dung.");
            if (post.AuthorId == me) throw new ApiException(403, "self_report", "Không thể báo cáo nội dung của chính mình.");
            if (await db.Reports.AnyAsync(x => x.PostId == id && x.ReporterId == me && x.Status == "Pending", ct))
                throw new ApiException(409, "duplicate_report", "Báo cáo trước của bạn đang chờ xử lý.");
            var media = await db.Media.Where(x => x.PostId == id && (x.Kind == "Image" || x.Kind == "Video")).OrderBy(x => x.Position).Select(x => x.Id).ToArrayAsync(ct);
            var report = new Report { PostId = id, ReporterId = me, Reason = request.Reason, Description = request.Description?.Trim(), SnapshotText = post.Text, SnapshotMediaIds = media };
            db.Reports.Add(report);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new { Id = report.Id.ToString(), report.Status, report.CreatedAt };
        }

        public async Task<Page<object>> MyReportsAsync(Guid me, int limit, string? cursor, CancellationToken ct)
        {
            limit = CursorCodec.Limit(limit);
            var marker = CursorCodec.Decode(cursor, "reports");
            var query = db.Reports.AsNoTracking().Where(x => x.ReporterId == me);
            if (marker is not null) { var id = long.Parse(marker.Id); query = query.Where(x => x.CreatedAt < marker.At || (x.CreatedAt == marker.At && x.Id < id)); }
            var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(limit + 1).ToListAsync(ct);
            var more = rows.Count > limit; if (more) rows.RemoveAt(limit);
            return new Page<object>(rows.Select(x => (object)new { Id = x.Id.ToString(), PostId = x.PostId.ToString(), x.Reason, x.Description, x.Status, x.CreatedAt, x.ClosedAt }).ToList(),
                more ? CursorCodec.Encode("reports", rows[^1].CreatedAt, rows[^1].Id) : null);
        }

        public async Task<Page<object>> QueueAsync(int limit, string? cursor, CancellationToken ct)
        {
            limit = CursorCodec.Limit(limit);
            var marker = CursorCodec.Decode(cursor, "queue");
            var query = db.Reports.Where(x => x.Status == "Pending").GroupBy(x => x.PostId)
                .Select(group => new { PostId = group.Key, OldestPendingAt = group.Min(x => x.CreatedAt), PendingCount = group.Count() });
            if (marker is not null) { var id = long.Parse(marker.Id); query = query.Where(x => x.OldestPendingAt > marker.At || (x.OldestPendingAt == marker.At && x.PostId > id)); }
            var rows = await query.OrderBy(x => x.OldestPendingAt).ThenBy(x => x.PostId).Take(limit + 1).ToListAsync(ct);
            var more = rows.Count > limit; if (more) rows.RemoveAt(limit);
            return new Page<object>(rows.Select(x => (object)new { PostId = x.PostId.ToString(), x.OldestPendingAt, x.PendingCount }).ToList(),
                more ? CursorCodec.Encode("queue", rows[^1].OldestPendingAt, rows[^1].PostId) : null);
        }

        public async Task<object> DetailAsync(long id, Guid admin, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var post = await posts.LockAsync(id, ct);
            var reports = await db.Reports.AsNoTracking().Where(x => x.PostId == id).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
            if (reports.Count == 0) throw new ApiException(404, "group_not_found", "Không tìm thấy nhóm báo cáo.");
            var snapshotIds = reports.SelectMany(x => x.SnapshotMediaIds).Distinct().ToArray();
            var snapshotMedia = await db.Media.AsNoTracking().Where(x => snapshotIds.Contains(x.Id)).Select(x => new { x.Id, x.Kind }).ToListAsync(ct);
            var result = new
            {
                PostId = id.ToString(),
                GroupVersion = GroupVersion(post, reports),
                CurrentPost = (await posts.ViewsAsync([post], admin, true, ct))[0],
                Reports = reports.Select(x => new { Id = x.Id.ToString(), x.ReporterId, x.Reason, x.Description, x.Status, x.CreatedAt, x.ClosedAt, x.SnapshotText, x.SnapshotMediaIds, SnapshotMedia = snapshotMedia.Where(media => x.SnapshotMediaIds.Contains(media.Id)).ToArray() })
            };
            await transaction.CommitAsync(ct);
            return result;
        }

        public async Task<object> DecideAsync(long id, Guid admin, DecisionRequest request, CancellationToken ct)
        {
            if (request.Decision is not ("keep" or "hide")) throw new ApiException(400, "invalid_decision", "Quyết định không hợp lệ.");
            var reason = ValidateReason(request.Reason);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var post = await posts.LockAsync(id, ct);
            var pending = await db.Reports.Where(x => x.PostId == id && x.Status == "Pending").OrderBy(x => x.Id).ToListAsync(ct);
            if (pending.Count == 0 || post.Status != "Visible" || request.GroupVersion != GroupVersion(post, pending))
                throw new ApiException(409, "group_changed", "Nhóm báo cáo đã thay đổi hoặc đã được xử lý. Vui lòng tải lại.");
            var before = post.Status;
            post.Status = request.Decision == "hide" ? "Hidden" : "Visible";
            if (request.Decision == "hide") post.ModerationReason = reason;
            // A Keep decision must also advance rowversion so a stale queue token cannot be reused.
            db.Entry(post).Property(x => x.Status).IsModified = true;
            var action = new ModerationAction { PostId = id, ActorId = admin, Decision = request.Decision == "hide" ? "Hide" : "Keep", Reason = reason, PreviousStatus = before, NextStatus = post.Status };
            db.ModerationActions.Add(action);
            await db.SaveChangesAsync(ct);
            foreach (var report in pending)
            {
                report.Status = request.Decision == "hide" ? "ClosedHidden" : "ClosedKept";
                report.ClosedAt = DateTime.UtcNow; report.ModerationActionId = action.Id;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new { Id = action.Id.ToString(), action.Decision, action.CreatedAt, Version = Convert.ToBase64String(post.Version) };
        }

        public async Task<object> RestoreAsync(long id, Guid admin, string? version, string reason, CancellationToken ct)
        {
            reason = ValidateReason(reason);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var post = await posts.LockAsync(id, ct);
            if (post.Status == "Deleted") throw new ApiException(404, "post_not_found", "Không tìm thấy nội dung.");
            if (post.Status != "Hidden") throw new ApiException(409, "not_hidden", "Nội dung không ở trạng thái đã ẩn.");
            PostService.MatchVersion(post, version);
            post.Status = "Visible"; post.ModerationReason = null;
            var action = new ModerationAction { PostId = id, ActorId = admin, Decision = "Restore", Reason = reason, PreviousStatus = "Hidden", NextStatus = "Visible" };
            db.ModerationActions.Add(action);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new { Id = action.Id.ToString(), action.Decision, action.CreatedAt, Version = Convert.ToBase64String(post.Version) };
        }

        public async Task<Page<object>> HistoryAsync(long id, int limit, string? cursor, CancellationToken ct)
        {
            limit = CursorCodec.Limit(limit);
            var marker = CursorCodec.Decode(cursor, "history");
            if (!await db.Posts.AnyAsync(x => x.Id == id, ct)) throw new ApiException(404, "post_not_found", "Không tìm thấy nội dung.");
            var query = db.ModerationActions.AsNoTracking().Where(x => x.PostId == id);
            if (marker is not null) { var actionId = long.Parse(marker.Id); query = query.Where(x => x.CreatedAt < marker.At || (x.CreatedAt == marker.At && x.Id < actionId)); }
            var rows = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(limit + 1).ToListAsync(ct);
            var more = rows.Count > limit; if (more) rows.RemoveAt(limit);
            return new Page<object>(rows.Select(x => (object)new { Id = x.Id.ToString(), x.ActorId, x.Decision, x.Reason, x.PreviousStatus, x.NextStatus, x.CreatedAt }).ToList(),
                more ? CursorCodec.Encode("history", rows[^1].CreatedAt, rows[^1].Id) : null);
        }

        private static string ValidateReason(string? reason) => reason?.Trim() is string text && text.Length is >= 1 and <= 1000
            ? text : throw new ApiException(400, "invalid_reason", "Lý do phải có từ 1 đến 1000 ký tự.");
        private static string GroupVersion(Post post, IEnumerable<Report> reports) => Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(
            Convert.ToBase64String(post.Version) + ":" + string.Join(",", reports.Where(x => x.Status == "Pending").Select(x => x.Id).Order()))));
    }
}
