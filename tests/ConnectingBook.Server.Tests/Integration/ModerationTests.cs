using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Models;

namespace ConnectingBook.Server.Tests.Integration
{
    public class ModerationTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
    {
        [SqlServerFact]
        public async Task Reporting_hide_restore_preserves_history_and_ownership()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var author = factory.CreateHttpsClient();
            using var member = factory.CreateHttpsClient();
            using var admin = factory.CreateHttpsClient();
            var owner = await ApiTestUser.RegisterAndLogin(author);
            await ApiTestUser.RegisterAndLogin(member);
            var adminId = await ApiTestUser.RegisterAndLogin(admin);
            await using var db = fixture.CreateContext();
            var adminRow = await db.Users.SingleAsync(x => x.Id == adminId);
            adminRow.Role = "Admin"; await db.SaveChangesAsync();
            using var refresh = await admin.PostAsync("/api/auth/refresh", null);
            using (var refreshed = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync()))
                admin.DefaultRequestHeaders.Authorization = new("Bearer", refreshed.RootElement.GetProperty("accessToken").GetString());
            var post = new Post { AuthorId = owner, Text = "Bản gốc", PublishedAt = DateTime.UtcNow.AddDays(-1) };
            db.Posts.Add(post); await db.SaveChangesAsync();
            using var self = await author.PostAsJsonAsync($"/api/posts/{post.Id}/reports", new { reason = "Spam" });
            Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
            using var report = await member.PostAsJsonAsync($"/api/posts/{post.Id}/reports", new { reason = "Spam" });
            Assert.Equal(HttpStatusCode.Created, report.StatusCode);
            using var duplicate = await member.PostAsJsonAsync($"/api/posts/{post.Id}/reports", new { reason = "Spam" });
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            using var forbidden = await member.GetAsync("/api/admin/report-groups");
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            using var details = await admin.GetAsync($"/api/admin/report-groups/{post.Id}");
            Assert.Equal(HttpStatusCode.OK, details.StatusCode);
            using var detailJson = JsonDocument.Parse(await details.Content.ReadAsStringAsync());
            var groupVersion = detailJson.RootElement.GetProperty("groupVersion").GetString();
            using var decision = await admin.PostAsJsonAsync($"/api/admin/report-groups/{post.Id}/decision", new { decision = "hide", reason = "Nội dung không phù hợp", groupVersion });
            Assert.Equal(HttpStatusCode.OK, decision.StatusCode);
            using var invisible = await member.GetAsync($"/api/posts/{post.Id}");
            Assert.Equal(HttpStatusCode.NotFound, invisible.StatusCode);
            db.ChangeTracker.Clear();
            var hidden = await db.Posts.SingleAsync(x => x.Id == post.Id);
            using var restoreRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/posts/{post.Id}/restore") { Content = JsonContent.Create(new { reason = "Kiểm tra lại" }) };
            restoreRequest.Headers.TryAddWithoutValidation("If-Match", Convert.ToBase64String(hidden.Version));
            using var restore = await admin.SendAsync(restoreRequest);
            Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
            db.ChangeTracker.Clear();
            var restored = await db.Posts.SingleAsync(x => x.Id == post.Id);
            Assert.Equal(post.PublishedAt, restored.PublishedAt);
            Assert.Equal("Visible", restored.Status);
            Assert.Equal(2, await db.ModerationActions.CountAsync(x => x.PostId == post.Id));
            Assert.Equal("ClosedHidden", (await db.Reports.SingleAsync(x => x.PostId == post.Id)).Status);
        }
    }
}
