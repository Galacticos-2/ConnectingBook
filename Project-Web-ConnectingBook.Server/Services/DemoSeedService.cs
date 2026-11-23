using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Data;
using Project_Web_ConnectingBook.Server.Models;
using Project_Web_ConnectingBook.Server.Storage;

namespace Project_Web_ConnectingBook.Server.Services
{
    public class DemoSeedService(ConnectingBookDbContext db, PasswordHasher<User> hasher, VideoTools video, IStorage storage)
    {
        public async Task RunAsync(bool performance, CancellationToken ct)
        {
            var database = new SqlConnectionStringBuilder(db.Database.GetConnectionString()).InitialCatalog;
            if (!database.StartsWith("ConnectingBook_Test_", StringComparison.Ordinal) && !database.StartsWith("ConnectingBook_Demo_", StringComparison.Ordinal))
                throw new InvalidOperationException("Demo seed is restricted to dedicated test/demo databases, never ConnectingBookSystemDb.");
            await db.Database.MigrateAsync(ct);
            await using var seedTransaction = await db.Database.BeginTransactionAsync(ct);
            if (await db.Users.AnyAsync(ct)) return;
            var password = Environment.GetEnvironmentVariable("DEMO_PASSWORD");
            if (password is null || password.Length is < 10 or > 128) throw new InvalidOperationException("Provide DEMO_PASSWORD through the environment (10–128 characters).");
            var email = Environment.GetEnvironmentVariable("DEMO_EMAIL") ?? "minh@connectbook.invalid";
            var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL") ?? "admin@connectbook.invalid";
            var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
            if (!performance && (adminPassword is null || adminPassword.Length is < 10 or > 128))
                throw new InvalidOperationException("Provide ADMIN_PASSWORD through the environment for demo administration.");
            var userCount = performance ? 1000 : 3;
            var users = new List<User>();
            for (var index = 0; index < userCount; index++)
            {
                var user = new User
                {
                    Email = (index == 0 ? email : !performance && index == 2 ? adminEmail : $"member-{index}@connectbook.invalid").Trim().ToLowerInvariant(),
                    DisplayName = index == 0 ? "Minh" : index == 1 ? "An" : !performance ? "Quản trị viên" : $"Thành viên {index}",
                    Bio = "Tài khoản dữ liệu demo cục bộ.",
                    Role = !performance && index == 2 ? "Admin" : "Member"
                };
                user.PasswordHash = hasher.HashPassword(user, !performance && index == 2 ? adminPassword! : index == 0 || !performance ? password : Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
                users.Add(user);
            }
            db.Users.AddRange(users); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            for (var index = 1; index < Math.Min(userCount, 101); index++) db.Follows.Add(new Follow { FollowerId = users[0].Id, FollowedId = users[index].Id });
            await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            var count = performance ? 100000 : 24;
            var now = DateTime.UtcNow;
            string[] captions = [
                "Hôm nay mình dành một chút thời gian để đọc, ghi lại vài ý tưởng và bắt đầu một việc mới. Còn bạn, điều gì khiến ngày hôm nay đáng nhớ?",
                "Một cuộc trò chuyện ngắn đôi khi mở ra cả một góc nhìn mới. Rất vui được chia sẻ câu chuyện ở đây.",
                "Cuối tuần chậm lại một chút: một trang sách, một tách trà và thời gian cho những người mình quan tâm.",
                "Chia sẻ một điều mình vừa học được: không cần đợi mọi thứ hoàn hảo mới bắt đầu. Mỗi bước nhỏ đều có giá trị."
            ];
            for (var offset = 0; offset < count; offset += 2000)
            {
                var rows = Enumerable.Range(offset, Math.Min(2000, count - offset)).Select(index => new Post
                {
                    AuthorId = users[index % users.Count].Id,
                    Text = performance ? $"Nội dung đo hiệu năng {index}" : captions[index % captions.Length],
                    CreatedAt = now.AddSeconds(-index / 2),
                    PublishedAt = now.AddSeconds(-index / 2)
                });
                db.Posts.AddRange(rows); await db.SaveChangesAsync(ct); db.ChangeTracker.Clear();
            }
            if (!performance)
            {
                var post = await db.Posts.OrderBy(x => x.Id).FirstAsync(ct);
                db.Reports.Add(new Report { PostId = post.Id, ReporterId = users[1].Id, Reason = "Other", Description = "Báo cáo mẫu để thực hành luồng kiểm duyệt.", SnapshotText = post.Text });
                await db.SaveChangesAsync(ct);
            }
            var keys = performance ? Array.Empty<string>() : await AddDemoReelAsync(users[0].Id, ct);
            try { await seedTransaction.CommitAsync(ct); }
            catch
            {
                foreach (var key in keys) await storage.DeleteAsync(key, CancellationToken.None);
                throw;
            }
        }

        private async Task<string[]> AddDemoReelAsync(Guid owner, CancellationToken ct)
        {
            Directory.CreateDirectory(video.WorkDirectory);
            var job = Guid.NewGuid().ToString("N");
            var path = Path.Combine(video.WorkDirectory, job + ".mp4");
            var thumbnail = Path.Combine(video.WorkDirectory, job + ".jpg");
            var stored = new List<string>();
            try
            {
                await video.RunAsync(false, ["-nostdin", "-v", "error", "-y", "-f", "lavfi", "-i", "testsrc=size=320x180:rate=24:duration=2", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-threads", "1", "-map_metadata", "-1", "-movflags", "+faststart", path], ct);
                await video.RunAsync(false, ["-nostdin", "-v", "error", "-y", "-i", path, "-frames:v", "1", "-threads", "1", thumbnail], ct);
                var info = await video.ProbeAsync(path, ct);
                var post = new Post { AuthorId = owner, Type = "Reel", Text = "Video demo được tạo cục bộ.", PublishedAt = DateTime.UtcNow };
                db.Posts.Add(post); await db.SaveChangesAsync(ct);
                var media = new Media { OwnerId = owner, PostId = post.Id, Kind = "Video", StorageKey = Guid.NewGuid().ToString("N"), ContentType = "video/mp4", SizeBytes = new FileInfo(path).Length, Width = info.Width, Height = info.Height, DurationSeconds = info.Duration };
                var thumb = new Media { OwnerId = owner, PostId = post.Id, ParentMediaId = media.Id, Kind = "Thumbnail", StorageKey = Guid.NewGuid().ToString("N"), ContentType = "image/jpeg", SizeBytes = new FileInfo(thumbnail).Length, Width = info.Width, Height = info.Height };
                foreach (var item in new[] { (media, path), (thumb, thumbnail) })
                {
                    await using var input = File.OpenRead(item.Item2);
                    await storage.PutAsync(item.Item1.StorageKey, input, input.Length, item.Item1.ContentType, ct);
                    stored.Add(item.Item1.StorageKey);
                }
                db.Media.AddRange(media, thumb); await db.SaveChangesAsync(ct);
                return stored.ToArray();
            }
            catch
            {
                foreach (var key in stored) await storage.DeleteAsync(key, CancellationToken.None);
                throw;
            }
            finally { File.Delete(path); File.Delete(thumbnail); }
        }
    }
}
