using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Project_Web_ConnectingBook.Server.Services;
using Project_Web_ConnectingBook.Server.Workers;
using Project_Web_ConnectingBook.Server.Models;
using Microsoft.AspNetCore.Http;
using Project_Web_ConnectingBook.Server.Infrastructure;

namespace ConnectingBook.Server.Tests.Integration
{
    public class ReelsTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
    {
        [SqlServerFact]
        public async Task Oversized_video_is_rejected_without_reading_or_creating_a_post()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var client = factory.CreateHttpsClient();
            var owner = await ApiTestUser.RegisterAndLogin(client);
            using var scope = factory.Services.CreateScope();
            using var empty = new MemoryStream();
            var file = new FormFile(empty, 0, 50 * 1024 * 1024 + 1, "file", "oversized.mp4");
            var service = scope.ServiceProvider.GetRequiredService<ReelService>();
            var exception = await Assert.ThrowsAsync<ApiException>(() => service.UploadAsync(owner, file, "", CancellationToken.None));
            Assert.Equal(413, exception.Status);
            Assert.Equal("video_too_large", exception.Code);
            await using var db = fixture.CreateContext();
            Assert.False(await db.Posts.AnyAsync(x => x.AuthorId == owner));
        }

        [SqlServerFact]
        public async Task Missing_source_retries_twice_and_deleted_processing_post_is_never_published()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var client = factory.CreateHttpsClient();
            var owner = await ApiTestUser.RegisterAndLogin(client);
            await using var db = fixture.CreateContext();
            var failed = new Post { AuthorId = owner, Type = "Reel", Status = "Processing", Text = "Retry" };
            var deleted = new Post { AuthorId = owner, Type = "Reel", Status = "Deleted", Text = "Deleted", DeletedAt = DateTime.UtcNow };
            db.Posts.AddRange(failed, deleted);
            await db.SaveChangesAsync();
            db.Media.Add(new Media { OwnerId = owner, PostId = failed.Id, Kind = "Video", StorageKey = Guid.NewGuid().ToString("N"), ContentType = "video/mp4", SizeBytes = 12, Width = 32, Height = 32, DurationSeconds = 1 });
            await db.SaveChangesAsync();
            var worker = factory.Services.GetRequiredService<ReelWorker>();
            await worker.ProcessAsync(failed.Id, CancellationToken.None);
            await db.Entry(failed).ReloadAsync();
            Assert.Equal("Processing", failed.Status);
            Assert.Equal(1, failed.ProcessingAttempts);
            await worker.ProcessAsync(failed.Id, CancellationToken.None);
            await worker.ProcessAsync(failed.Id, CancellationToken.None);
            await db.Entry(failed).ReloadAsync();
            Assert.Equal("Failed", failed.Status);
            Assert.Equal(2, failed.ProcessingAttempts);
            Assert.Null(failed.PublishedAt);
            Assert.Equal("video_processing_failed", failed.FailureCode);
            await worker.ProcessAsync(deleted.Id, CancellationToken.None);
            await db.Entry(deleted).ReloadAsync();
            Assert.Equal("Deleted", deleted.Status);
            Assert.Null(deleted.PublishedAt);
            Assert.Equal(0, deleted.ProcessingAttempts);
        }

        [SqlServerFact]
        public async Task Real_MP4_longer_than_sixty_seconds_is_rejected()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var client = factory.CreateHttpsClient();
            await ApiTestUser.RegisterAndLogin(client);
            var video = factory.Services.GetRequiredService<VideoTools>();
            Directory.CreateDirectory(video.WorkDirectory);
            var path = Path.Combine(video.WorkDirectory, Guid.NewGuid().ToString("N") + ".mp4");
            try
            {
                await video.RunAsync(false, ["-nostdin", "-v", "error", "-y", "-f", "lavfi", "-i", "color=c=green:s=32x32:r=1:d=61", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-threads", "1", path], CancellationToken.None);
                using var content = new MultipartFormDataContent();
                content.Add(new ByteArrayContent(await File.ReadAllBytesAsync(path)), "file", "long.mp4");
                using var response = await client.PostAsync("/api/reels", content);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal("invalid_video_duration", problem.RootElement.GetProperty("code").GetString());
            }
            finally { File.Delete(path); }
        }

        [SqlServerFact]
        public async Task Real_MP4_is_processed_published_and_served_with_authenticated_ranges()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var client = factory.CreateHttpsClient();
            await ApiTestUser.RegisterAndLogin(client);
            var video = factory.Services.GetRequiredService<VideoTools>();
            Directory.CreateDirectory(video.WorkDirectory);
            var path = Path.Combine(video.WorkDirectory, Guid.NewGuid().ToString("N") + ".mp4");
            try
            {
                await video.RunAsync(false, ["-nostdin", "-v", "error", "-y", "-f", "lavfi", "-i", "color=c=green:s=32x32:d=1", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-threads", "1", path], CancellationToken.None);
                using var content = new MultipartFormDataContent();
                content.Add(new ByteArrayContent(await File.ReadAllBytesAsync(path)), "file", "video.mp4");
                content.Add(new StringContent("Một khoảnh khắc"), "caption");
                using var upload = await client.PostAsync("/api/reels", content);
                Assert.Equal(HttpStatusCode.Accepted, upload.StatusCode);
                using var body = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
                var id = long.Parse(body.RootElement.GetProperty("id").GetString()!);
                Assert.Equal("Processing", body.RootElement.GetProperty("status").GetString());
                var worker = factory.Services.GetRequiredService<ReelWorker>();
                await Task.WhenAll(worker.ProcessAsync(id, CancellationToken.None), worker.ProcessAsync(id, CancellationToken.None));
                await using var db = fixture.CreateContext();
                var post = await db.Posts.SingleAsync(x => x.Id == id);
                Assert.Equal("Visible", post.Status);
                Assert.NotNull(post.PublishedAt);
                Assert.Equal(1, post.ProcessingAttempts);
                var media = await db.Media.SingleAsync(x => x.PostId == id && x.Kind == "Video");
                using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/media/{media.Id}");
                request.Headers.Range = new RangeHeaderValue(0, 15);
                using var range = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.PartialContent, range.StatusCode);
                Assert.Equal(16, (await range.Content.ReadAsByteArrayAsync()).Length);
                Assert.NotNull(range.Content.Headers.ContentRange);
                using var anonymous = factory.CreateHttpsClient();
                using var denied = await anonymous.GetAsync($"/api/media/{media.Id}");
                Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            }
            finally { File.Delete(path); }
        }

        [SqlServerFact]
        public async Task Fake_MP4_is_rejected_before_post_is_created()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var client = factory.CreateHttpsClient();
            await ApiTestUser.RegisterAndLogin(client);
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent([77, 90, 0, 0]), "file", "fake.mp4");
            using var response = await client.PostAsync("/api/reels", content);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        }
    }
}
