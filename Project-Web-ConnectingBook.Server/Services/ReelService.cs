using System.ComponentModel.DataAnnotations;
using Project_Web_ConnectingBook.Server.Data;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Models;
using Project_Web_ConnectingBook.Server.Storage;
using Project_Web_ConnectingBook.Server.Workers;

namespace Project_Web_ConnectingBook.Server.Services
{
    public class ReelService(ConnectingBookDbContext db, IStorage storage, VideoTools video, ReelQueue queue, PostService posts)
    {
        public async Task<object> UploadAsync(Guid owner, IFormFile file, string caption, CancellationToken ct)
        {
            if (caption.Length > 500) throw new ApiException(400, "invalid_caption", "Mô tả reel tối đa 500 ký tự.");
            if (file.Length > 50 * 1024 * 1024) throw new ApiException(413, "video_too_large", "Video tối đa 50 MB.");
            Directory.CreateDirectory(video.WorkDirectory);
            var path = Path.Combine(video.WorkDirectory, Guid.NewGuid().ToString("N") + ".mp4");
            string? savedKey = null;
            try
            {
                await using (var input = file.OpenReadStream())
                await using (var output = File.Create(path))
                {
                    var buffer = new byte[81920]; int count;
                    while ((count = await input.ReadAsync(buffer, ct)) > 0)
                    {
                        if (output.Length + count > 50 * 1024 * 1024) throw new ApiException(413, "video_too_large", "Video tối đa 50 MB.");
                        await output.WriteAsync(buffer.AsMemory(0, count), ct);
                    }
                }
                var info = await video.ProbeAsync(path, ct);
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var post = new Post { AuthorId = owner, Type = "Reel", Text = caption.Trim(), Status = "Processing" };
                db.Posts.Add(post); await db.SaveChangesAsync(ct);
                var media = new Media { OwnerId = owner, PostId = post.Id, Kind = "Video", StorageKey = Guid.NewGuid().ToString("N"), ContentType = "video/mp4", SizeBytes = new FileInfo(path).Length, Width = info.Width, Height = info.Height, DurationSeconds = info.Duration };
                await using (var input = File.OpenRead(path)) await storage.PutAsync(media.StorageKey, input, media.SizeBytes, media.ContentType, ct);
                savedKey = media.StorageKey;
                db.Media.Add(media); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
                savedKey = null;
                queue.Channel.Writer.TryWrite(post.Id); // If full, the durable database scanner picks it up.
                return (await posts.ViewsAsync([post], owner, true, ct))[0];
            }
            finally
            {
                File.Delete(path);
                if (savedKey is not null) await storage.DeleteAsync(savedKey, CancellationToken.None);
            }
        }
    }
}
