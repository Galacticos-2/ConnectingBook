using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Models;
using SkiaSharp;

namespace ConnectingBook.Server.Tests.Integration
{
    public class ImageEvidenceTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
    {
        [SqlServerFact]
        public async Task Uploaded_image_is_private_until_attached_and_deleted_snapshot_is_admin_only()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var author = factory.CreateHttpsClient(); using var reporter = factory.CreateHttpsClient(); using var admin = factory.CreateHttpsClient();
            await ApiTestUser.RegisterAndLogin(author); await ApiTestUser.RegisterAndLogin(reporter);
            var adminId = await ApiTestUser.RegisterAndLogin(admin);
            await using var db = fixture.CreateContext();
            (await db.Users.SingleAsync(x => x.Id == adminId)).Role = "Admin"; await db.SaveChangesAsync();
            using var refresh = await admin.PostAsync("/api/auth/refresh", null);
            using (var json = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync())) admin.DefaultRequestHeaders.Authorization = new("Bearer", json.RootElement.GetProperty("accessToken").GetString());
            using var bitmap = new SKBitmap(4, 4); bitmap.Erase(SKColors.Green);
            using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            using var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(encoded.ToArray()), "file", "photo.png");
            using var upload = await author.PostAsync("/api/media/images", form);
            Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
            using var uploaded = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
            var imageId = uploaded.RootElement.GetProperty("imageId").GetString();
            using var privateImage = await reporter.GetAsync($"/api/media/{imageId}"); Assert.Equal(HttpStatusCode.NotFound, privateImage.StatusCode);
            using var foreignPost = await reporter.PostAsJsonAsync("/api/posts", new { text = "Không sở hữu ảnh", imageIds = new[] { imageId } }); Assert.Equal(HttpStatusCode.BadRequest, foreignPost.StatusCode);
            using var create = await author.PostAsJsonAsync("/api/posts", new { text = "Bằng chứng", imageIds = new[] { imageId } }); Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
            var postId = created.RootElement.GetProperty("id").GetString(); var version = created.RootElement.GetProperty("version").GetString();
            using var report = await reporter.PostAsJsonAsync($"/api/posts/{postId}/reports", new { reason = "Spam" }); Assert.Equal(HttpStatusCode.Created, report.StatusCode);
            using var reported = JsonDocument.Parse(await report.Content.ReadAsStringAsync()); var reportId = reported.RootElement.GetProperty("id").GetString();
            using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{postId}"); deleteRequest.Headers.TryAddWithoutValidation("If-Match", version);
            using var delete = await author.SendAsync(deleteRequest); Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
            using var normal = await admin.GetAsync($"/api/media/{imageId}"); Assert.Equal(HttpStatusCode.NotFound, normal.StatusCode);
            using var evidence = await admin.GetAsync($"/api/admin/reports/{reportId}/media/{imageId}"); Assert.Equal(HttpStatusCode.OK, evidence.StatusCode);
            using var denied = await reporter.GetAsync($"/api/admin/reports/{reportId}/media/{imageId}"); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal("ClosedAuthorDeleted", (await db.Reports.AsNoTracking().SingleAsync(x => x.Id == long.Parse(reportId!))).Status);
        }
    }
}
