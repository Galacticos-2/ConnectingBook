using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Data;
using Project_Web_ConnectingBook.Server.Models;
using Project_Web_ConnectingBook.Server.Services;
using Project_Web_ConnectingBook.Server.Storage;

namespace Project_Web_ConnectingBook.Server.Workers
{
    public class ReelWorker(IServiceScopeFactory scopes, ReelQueue queue, VideoTools video, ILogger<ReelWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    long id;
                    if (!queue.Channel.Reader.TryRead(out id))
                    {
                        using var scan = scopes.CreateScope();
                        var db = scan.ServiceProvider.GetRequiredService<ConnectingBookDbContext>();
                        var stale = DateTime.UtcNow.AddMinutes(-10);
                        var candidate = await db.Posts.Where(x => x.Type == "Reel" && x.Status == "Processing" && (x.ProcessingStartedAt == null || x.ProcessingStartedAt < stale)).OrderBy(x => x.CreatedAt).Select(x => (long?)x.Id).FirstOrDefaultAsync(stoppingToken);
                        if (candidate is null) { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); continue; }
                        id = candidate.Value;
                    }
                    await ProcessAsync(id, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    logger.LogWarning("Video worker unavailable: {ExceptionType}", exception.GetType().Name);
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                }
            }
        }

        public async Task ProcessAsync(long id, CancellationToken ct)
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ConnectingBookDbContext>();
            var posts = scope.ServiceProvider.GetRequiredService<PostService>();
            var storage = scope.ServiceProvider.GetRequiredService<IStorage>();
            Media source;
            await using (var claim = await db.Database.BeginTransactionAsync(ct))
            {
                var post = await posts.LockAsync(id, ct);
                if (!ReelPolicy.TryClaim(post, DateTime.UtcNow)) { await db.SaveChangesAsync(ct); await claim.CommitAsync(ct); return; }
                source = await db.Media.SingleAsync(x => x.PostId == id && x.Kind == "Video", ct);
                await db.SaveChangesAsync(ct); await claim.CommitAsync(ct);
            }
            Directory.CreateDirectory(video.WorkDirectory);
            var job = Guid.NewGuid().ToString("N");
            var input = Path.Combine(video.WorkDirectory, job + "-input.mp4");
            var output = Path.Combine(video.WorkDirectory, job + "-output.mp4");
            var thumbnail = Path.Combine(video.WorkDirectory, job + "-thumb.jpg");
            var createdKeys = new List<string>();
            var originalKey = source.StorageKey;
            try
            {
                await using (var file = File.Create(input)) await storage.CopyToAsync(originalKey, file, 0, source.SizeBytes, ct);
                await video.RunAsync(false, ["-nostdin", "-v", "error", "-y", "-protocol_whitelist", "file", "-i", input, "-map", "0:v:0", "-map", "0:a?", "-vf", "scale='min(1080,iw)':'min(1080,ih)':force_original_aspect_ratio=decrease:force_divisible_by=2", "-c:v", "libx264", "-preset", "fast", "-crf", "23", "-threads", "1", "-pix_fmt", "yuv420p", "-c:a", "aac", "-map_metadata", "-1", "-movflags", "+faststart", output], ct);
                var info = await video.ProbeAsync(output, ct);
                await video.RunAsync(false, ["-nostdin", "-v", "error", "-y", "-protocol_whitelist", "file", "-i", output, "-frames:v", "1", "-vf", "scale=320:-2", "-threads", "1", thumbnail], ct);
                var outputKey = Guid.NewGuid().ToString("N"); var thumbnailKey = Guid.NewGuid().ToString("N");
                await using (var file = File.OpenRead(output)) await storage.PutAsync(outputKey, file, file.Length, "video/mp4", ct);
                createdKeys.Add(outputKey);
                await using (var file = File.OpenRead(thumbnail)) await storage.PutAsync(thumbnailKey, file, file.Length, "image/jpeg", ct);
                createdKeys.Add(thumbnailKey);
                db.ChangeTracker.Clear();
                await using var publish = await db.Database.BeginTransactionAsync(ct);
                var post = await posts.LockAsync(id, ct);
                if (post.Status != "Processing") { await publish.RollbackAsync(ct); return; }
                var media = await db.Media.SingleAsync(x => x.Id == source.Id, ct);
                media.StorageKey = outputKey; media.SizeBytes = new FileInfo(output).Length; media.Width = info.Width; media.Height = info.Height; media.DurationSeconds = info.Duration;
                db.Media.Add(new Media { OwnerId = post.AuthorId, PostId = id, ParentMediaId = media.Id, Kind = "Thumbnail", StorageKey = thumbnailKey, ContentType = "image/jpeg", Width = 320, Height = Math.Max(2, (int)(320.0 * info.Height / info.Width) / 2 * 2), SizeBytes = new FileInfo(thumbnail).Length });
                post.Status = "Visible"; post.PublishedAt = DateTime.UtcNow; post.FailureCode = null;
                await db.SaveChangesAsync(ct); await publish.CommitAsync(ct);
                createdKeys.Clear();
                await storage.DeleteAsync(originalKey, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch
            {
                db.ChangeTracker.Clear();
                await using var failed = await db.Database.BeginTransactionAsync(ct);
                var post = await posts.LockAsync(id, ct);
                if (post.Status == "Processing")
                {
                    post.ProcessingStartedAt = null;
                    if (post.ProcessingAttempts >= 2) { post.Status = "Failed"; post.FailureCode = "video_processing_failed"; post.UpdatedAt = DateTime.UtcNow; }
                    else queue.Channel.Writer.TryWrite(id);
                    await db.SaveChangesAsync(ct);
                }
                await failed.CommitAsync(ct);
            }
            finally
            {
                File.Delete(input); File.Delete(output); File.Delete(thumbnail);
                foreach (var key in createdKeys) await storage.DeleteAsync(key, CancellationToken.None);
            }
        }
    }
}
