using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Data;
using Project_Web_ConnectingBook.Server.Services;
using Project_Web_ConnectingBook.Server.Storage;

namespace Project_Web_ConnectingBook.Server.Workers
{
    public class MediaCleanupService(ConnectingBookDbContext db, IStorage storage, PostService posts)
    {
        public async Task SweepAsync(DateTime now, CancellationToken ct)
        {
            var orphanBefore = now.AddHours(-24);
            var failedBefore = now.AddDays(-7);
            var candidates = await db.Media.AsNoTracking().Where(x => x.ParentMediaId == null && x.PostId == null && x.CreatedAt < orphanBefore && (x.Kind == "Image" || x.Kind == "Avatar"))
                .Select(x => new { x.Id, x.OwnerId }).Take(100).ToListAsync(ct);
            foreach (var candidate in candidates)
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                _ = await db.Users.FromSqlInterpolated($"SELECT * FROM [Users] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {candidate.OwnerId}").SingleAsync(ct);
                var media = await db.Media.FromSqlInterpolated($"SELECT * FROM [Media] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {candidate.Id}").SingleOrDefaultAsync(ct);
                if (media is null || media.PostId != null || media.CreatedAt >= orphanBefore || await db.Users.AnyAsync(x => x.AvatarMediaId == candidate.Id, ct)) continue;
                var children = await db.Media.Where(x => x.ParentMediaId == candidate.Id).ToListAsync(ct);
                var ids = children.Select(x => x.Id).Append(media.Id).ToHashSet();
                var references = await db.Reports.Select(x => x.SnapshotMediaIds).ToListAsync(ct);
                if (references.Any(snapshot => snapshot.Any(ids.Contains))) continue;
                // Only unreferenced, expired objects are removed. Missing files are safe to retry.
                foreach (var item in children.Append(media)) await storage.DeleteAsync(item.StorageKey, ct);
                db.Media.RemoveRange(children); db.Media.Remove(media);
                await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            }
            var failedIds = await db.Posts.AsNoTracking().Where(x => x.Type == "Reel" && x.Status == "Failed" && (x.UpdatedAt ?? x.CreatedAt) < failedBefore).Select(x => x.Id).Take(100).ToListAsync(ct);
            foreach (var id in failedIds)
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var post = await posts.LockAsync(id, ct);
                if (post.Status != "Failed" || (post.UpdatedAt ?? post.CreatedAt) >= failedBefore || await db.Reports.AnyAsync(x => x.PostId == id, ct)) continue;
                var media = await db.Media.Where(x => x.PostId == id).ToListAsync(ct);
                var ids = media.Select(x => x.Id).ToHashSet();
                var references = await db.Reports.Select(x => x.SnapshotMediaIds).ToListAsync(ct);
                if (references.Any(snapshot => snapshot.Any(ids.Contains))) continue;
                foreach (var item in media) await storage.DeleteAsync(item.StorageKey, ct);
                db.Media.RemoveRange(media); db.Posts.Remove(post);
                await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            }
        }
    }

    public class MediaCleanupWorker(IServiceScopeFactory scopes, ILogger<MediaCleanupWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<MediaCleanupService>().SweepAsync(DateTime.UtcNow, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception) { logger.LogWarning("Media cleanup interrupted: {ExceptionType}", exception.GetType().Name); }
            }
        }
    }
}
