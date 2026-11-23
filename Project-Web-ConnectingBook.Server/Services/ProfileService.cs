using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Contracts;
using Project_Web_ConnectingBook.Server.Data;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Models;

namespace Project_Web_ConnectingBook.Server.Services
{
    public class ProfileService(ConnectingBookDbContext db)
    {
        public async Task<object> GetAsync(Guid target, Guid me, CancellationToken ct)
        {
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == target && x.Status == "Active", ct)
                ?? throw new ApiException(404, "user_not_found", "Không tìm thấy hồ sơ.");
            return new
            {
                user.Id,
                user.DisplayName,
                user.Bio,
                user.AvatarMediaId,
                FollowerCount = await db.Follows.CountAsync(x => x.FollowedId == target, ct),
                FollowingCount = await db.Follows.CountAsync(x => x.FollowerId == target, ct),
                IsFollowedByMe = await db.Follows.AnyAsync(x => x.FollowerId == me && x.FollowedId == target, ct)
            };
        }

        public async Task<object> UpdateAsync(Guid me, ProfileRequest request, CancellationToken ct)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var user = await db.Users.FromSqlInterpolated($"SELECT * FROM [Users] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {me}").SingleAsync(ct);
            if (request.DisplayName is not null)
            {
                if (request.DisplayName.Trim().Length < 2) throw new ApiException(400, "invalid_name", "Tên hiển thị phải có ít nhất 2 ký tự.");
                user.DisplayName = request.DisplayName.Trim();
            }
            if (request.Bio is not null) user.Bio = request.Bio.Trim();
            if (request.AvatarMediaId is Guid mediaId)
            {
                var avatar = await db.Media.FromSqlInterpolated($"SELECT * FROM [Media] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {mediaId}").SingleOrDefaultAsync(ct);
                if (avatar is null || avatar.OwnerId != me || avatar.Kind != "Avatar") throw new ApiException(400, "invalid_avatar", "Ảnh đại diện không hợp lệ.");
                user.AvatarMediaId = mediaId;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return await GetAsync(me, me, ct);
        }
    }

    public class FollowService(ConnectingBookDbContext db)
    {
        public async Task SetAsync(Guid me, Guid target, bool follow, CancellationToken ct)
        {
            if (me == target) throw new ApiException(400, "self_follow", "Không thể tự theo dõi.");
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // Serializes duplicate operations from the same account without catching transaction-aborting unique errors.
            _ = await db.Users.FromSqlInterpolated($"SELECT * FROM [Users] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {me}").SingleAsync(ct);
            if (!await db.Users.AnyAsync(x => x.Id == target && x.Status == "Active", ct)) throw new ApiException(404, "user_not_found", "Không tìm thấy hồ sơ.");
            var existing = await db.Follows.FindAsync([me, target], ct);
            if (follow && existing is null) db.Follows.Add(new Follow { FollowerId = me, FollowedId = target });
            if (!follow && existing is not null) db.Follows.Remove(existing);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }
}
