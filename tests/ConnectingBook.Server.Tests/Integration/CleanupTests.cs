using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Project_Web_ConnectingBook.Server.Models;
using Project_Web_ConnectingBook.Server.Storage;
using Project_Web_ConnectingBook.Server.Workers;

namespace ConnectingBook.Server.Tests.Integration
{
    public class CleanupTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
    {
        [SqlServerFact]
        public async Task Removes_only_expired_unreferenced_files_and_retains_report_evidence()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var client = factory.CreateHttpsClient();
            var owner = await ApiTestUser.RegisterAndLogin(client);
            await using var db = fixture.CreateContext();
            var stale = DateTime.UtcNow.AddDays(-2);
            Media Create(DateTime time) => new() { OwnerId = owner, StorageKey = Guid.NewGuid().ToString("N"), Kind = "Image", ContentType = "image/png", SizeBytes = 3, Width = 1, Height = 1, CreatedAt = time };
            var orphan = Create(stale); var retained = Create(stale); var fresh = Create(DateTime.UtcNow);
            var post = new Post { AuthorId = owner, Text = "Đã có báo cáo", PublishedAt = DateTime.UtcNow };
            db.Media.AddRange(orphan, retained, fresh); db.Posts.Add(post); await db.SaveChangesAsync();
            db.Reports.Add(new Report { PostId = post.Id, ReporterId = owner, Reason = "Spam", SnapshotText = post.Text, SnapshotMediaIds = [retained.Id] });
            await db.SaveChangesAsync();
            var storage = factory.Services.GetRequiredService<IStorage>();
            foreach (var item in new[] { orphan, retained, fresh }) { using var input = new MemoryStream([1, 2, 3]); await storage.PutAsync(item.StorageKey, input, 3, item.ContentType, CancellationToken.None); }
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<MediaCleanupService>().SweepAsync(DateTime.UtcNow, CancellationToken.None);
            Assert.False(await db.Media.AsNoTracking().AnyAsync(x => x.Id == orphan.Id));
            Assert.True(await db.Media.AsNoTracking().AnyAsync(x => x.Id == retained.Id));
            Assert.True(await db.Media.AsNoTracking().AnyAsync(x => x.Id == fresh.Id));
            Assert.Null(await storage.StatAsync(orphan.StorageKey, CancellationToken.None));
            Assert.NotNull(await storage.StatAsync(retained.StorageKey, CancellationToken.None));
        }
    }
}
