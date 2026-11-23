using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Project_Web_ConnectingBook.Server.Models;
using Project_Web_ConnectingBook.Server.Services;
using Project_Web_ConnectingBook.Server.Storage;
using Project_Web_ConnectingBook.Server.Workers;

namespace ConnectingBook.Server.Tests.Integration
{
    public class ReelDeletionRaceTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
    {
        [SqlServerFact]
        public async Task Delete_after_claim_prevents_publication_and_removes_generated_outputs()
        {
            using var started = new SemaphoreSlim(0, 1);
            using var resume = new SemaphoreSlim(0, 1);
            await using var factory = new ApiFactory(fixture.ConnectionString, configureServices: services =>
                services.AddSingleton<IStorage>(provider => new PausedStorage(
                    new LocalStorage(provider.GetRequiredService<IConfiguration>(), provider.GetRequiredService<IWebHostEnvironment>()), started, resume)));
            using var client = factory.CreateHttpsClient();
            var owner = await ApiTestUser.RegisterAndLogin(client);
            var tools = factory.Services.GetRequiredService<VideoTools>();
            Directory.CreateDirectory(tools.WorkDirectory);
            var path = Path.Combine(tools.WorkDirectory, Guid.NewGuid().ToString("N") + ".mp4");
            Task? job = null;
            try
            {
                await tools.RunAsync(false, ["-nostdin", "-v", "error", "-y", "-f", "lavfi", "-i", "color=c=blue:s=32x32:d=1", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-threads", "1", path], CancellationToken.None);
                await using var db = fixture.CreateContext();
                var post = new Post { AuthorId = owner, Type = "Reel", Status = "Processing", Text = "Delete during processing" };
                db.Posts.Add(post); await db.SaveChangesAsync();
                var media = new Media { OwnerId = owner, PostId = post.Id, Kind = "Video", StorageKey = Guid.NewGuid().ToString("N"), ContentType = "video/mp4", SizeBytes = new FileInfo(path).Length, Width = 32, Height = 32, DurationSeconds = 1 };
                db.Media.Add(media); await db.SaveChangesAsync();
                var storage = (PausedStorage)factory.Services.GetRequiredService<IStorage>();
                await using (var input = File.OpenRead(path)) await storage.PutAsync(media.StorageKey, input, input.Length, media.ContentType, CancellationToken.None);
                job = factory.Services.GetRequiredService<ReelWorker>().ProcessAsync(post.Id, CancellationToken.None);
                Assert.True(await started.WaitAsync(TimeSpan.FromSeconds(15)));
                await db.Entry(post).ReloadAsync();
                using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{post.Id}");
                delete.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(post.Version));
                using var deleted = await client.SendAsync(delete);
                Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
                resume.Release();
                await job;
                await db.Entry(post).ReloadAsync();
                Assert.Equal("Deleted", post.Status);
                Assert.Null(post.PublishedAt);
                Assert.Single(await db.Media.Where(x => x.PostId == post.Id).ToListAsync());
                foreach (var key in storage.GeneratedKeys) Assert.Null(await storage.StatAsync(key, CancellationToken.None));
                Assert.Equal(2, storage.GeneratedKeys.Count);
            }
            finally
            {
                if (resume.CurrentCount == 0) resume.Release();
                if (job is not null) await job;
                File.Delete(path);
            }
        }

        private sealed class PausedStorage(IStorage inner, SemaphoreSlim started, SemaphoreSlim resume) : IStorage
        {
            private bool processing;
            public List<string> GeneratedKeys { get; } = [];
            public async Task PutAsync(string key, Stream input, long size, string contentType, CancellationToken ct)
            {
                await inner.PutAsync(key, input, size, contentType, ct);
                if (processing) GeneratedKeys.Add(key);
            }
            public Task<StoredObject?> StatAsync(string key, CancellationToken ct) => inner.StatAsync(key, ct);
            public Task DeleteAsync(string key, CancellationToken ct) => inner.DeleteAsync(key, ct);
            public Task<bool> IsReadyAsync(CancellationToken ct) => inner.IsReadyAsync(ct);
            public async Task CopyToAsync(string key, Stream output, long offset, long length, CancellationToken ct)
            {
                processing = true;
                started.Release();
                await resume.WaitAsync(TimeSpan.FromSeconds(20), ct);
                await inner.CopyToAsync(key, output, offset, length, ct);
            }
        }
    }
}
