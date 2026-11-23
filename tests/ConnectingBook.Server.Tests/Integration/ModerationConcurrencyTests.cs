using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Models;

namespace ConnectingBook.Server.Tests.Integration
{
    public class ModerationConcurrencyTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
    {
        [SqlServerTheory]
        [InlineData("edit")]
        [InlineData("delete")]
        [InlineData("report")]
        public async Task Concurrent_change_and_hide_never_commit_a_stale_group(string change)
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var admin = factory.CreateHttpsClient();
            using var author = factory.CreateHttpsClient();
            using var reporter = factory.CreateHttpsClient();
            var adminId = await ApiTestUser.RegisterAndLogin(admin);
            var owner = await ApiTestUser.RegisterAndLogin(author);
            await ApiTestUser.RegisterAndLogin(reporter);
            await using var db = fixture.CreateContext();
            (await db.Users.SingleAsync(x => x.Id == adminId)).Role = "Admin";
            var post = new Post { AuthorId = owner, Text = "Original snapshot", PublishedAt = DateTime.UtcNow };
            db.Posts.Add(post); await db.SaveChangesAsync();
            db.Reports.Add(new Report { PostId = post.Id, ReporterId = adminId, Reason = "Spam", SnapshotText = post.Text });
            await db.SaveChangesAsync();
            var version = Convert.ToBase64String(post.Version);
            using var refresh = await admin.PostAsync("/api/auth/refresh", null);
            using (var token = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync())) admin.DefaultRequestHeaders.Authorization = new("Bearer", token.RootElement.GetProperty("accessToken").GetString());
            using var detail = await admin.GetAsync($"/api/admin/report-groups/{post.Id}");
            detail.EnsureSuccessStatusCode();
            using var group = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
            var groupVersion = group.RootElement.GetProperty("groupVersion").GetString();
            using var barrier = new Barrier(2);
            async Task Wait() => await Task.Run(() => { if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15))) throw new TimeoutException(); });
            async Task<HttpStatusCode> Hide()
            {
                await Wait();
                using var result = await admin.PostAsJsonAsync($"/api/admin/report-groups/{post.Id}/decision", new { decision = "hide", reason = "Race test", groupVersion });
                return result.StatusCode;
            }
            async Task<HttpStatusCode> Change()
            {
                await Wait();
                using var request = change == "report"
                    ? new HttpRequestMessage(HttpMethod.Post, $"/api/posts/{post.Id}/reports") { Content = JsonContent.Create(new { reason = "Spam" }) }
                    : new HttpRequestMessage(change == "edit" ? HttpMethod.Patch : HttpMethod.Delete, $"/api/posts/{post.Id}") { Content = change == "edit" ? JsonContent.Create(new { text = "Edited" }) : null };
                request.Headers.TryAddWithoutValidation("If-Match", version);
                using var result = await (change == "report" ? reporter : author).SendAsync(request);
                return result.StatusCode;
            }
            var results = await Task.WhenAll(Hide(), Change());
            if (results[0] == HttpStatusCode.OK)
            {
                Assert.Equal(change == "report" ? HttpStatusCode.NotFound : HttpStatusCode.Conflict, results[1]);
                Assert.False(await db.Reports.AnyAsync(x => x.PostId == post.Id && x.Status == "Pending"));
            }
            else
            {
                Assert.Equal(HttpStatusCode.Conflict, results[0]);
                Assert.Equal(change == "report" ? HttpStatusCode.Created : change == "delete" ? HttpStatusCode.NoContent : HttpStatusCode.OK, results[1]);
            }
            Assert.All(await db.Reports.Where(x => x.PostId == post.Id).ToListAsync(), report => Assert.Equal("Original snapshot", report.SnapshotText));
        }

        [SqlServerFact]
        public async Task Failure_closing_reports_rolls_back_post_and_history()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var admin = factory.CreateHttpsClient();
            using var author = factory.CreateHttpsClient();
            using var reporter = factory.CreateHttpsClient();
            var adminId = await ApiTestUser.RegisterAndLogin(admin);
            var authorId = await ApiTestUser.RegisterAndLogin(author);
            await ApiTestUser.RegisterAndLogin(reporter);
            await using var db = fixture.CreateContext();
            (await db.Users.SingleAsync(x => x.Id == adminId)).Role = "Admin";
            var post = new Post { AuthorId = authorId, Text = "Rollback evidence", PublishedAt = DateTime.UtcNow };
            db.Posts.Add(post); await db.SaveChangesAsync();
            using var refresh = await admin.PostAsync("/api/auth/refresh", null);
            using (var token = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync())) admin.DefaultRequestHeaders.Authorization = new("Bearer", token.RootElement.GetProperty("accessToken").GetString());
            using var reported = await reporter.PostAsJsonAsync($"/api/posts/{post.Id}/reports", new { reason = "Spam" });
            Assert.Equal(HttpStatusCode.Created, reported.StatusCode);
            using var detail = await admin.GetAsync($"/api/admin/report-groups/{post.Id}");
            detail.EnsureSuccessStatusCode();
            using var group = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
            var groupVersion = group.RootElement.GetProperty("groupVersion").GetString();
            // This fixture owns its entire temporary database. Inject a failure in the second save.
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER [Test_ReportFailure] ON [Reports] AFTER UPDATE AS BEGIN THROW 51000, 'Injected test failure', 1; END");
            try
            {
                using var failed = await admin.PostAsJsonAsync($"/api/admin/report-groups/{post.Id}/decision", new { decision = "hide", reason = "Rollback test", groupVersion });
                Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
                db.ChangeTracker.Clear();
                Assert.Equal("Visible", (await db.Posts.SingleAsync(x => x.Id == post.Id)).Status);
                Assert.Equal("Pending", (await db.Reports.SingleAsync(x => x.PostId == post.Id)).Status);
                Assert.False(await db.ModerationActions.AnyAsync(x => x.PostId == post.Id));
            }
            finally { await db.Database.ExecuteSqlRawAsync("DROP TRIGGER [Test_ReportFailure]"); }
        }

        [SqlServerFact]
        public async Task Two_decisions_on_same_group_have_one_winner_and_one_conflict()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var client = factory.CreateHttpsClient();
            var adminId = await ApiTestUser.RegisterAndLogin(client);
            await using var db = fixture.CreateContext();
            (await db.Users.SingleAsync(x => x.Id == adminId)).Role = "Admin";
            var owner = new User { Email = $"{Guid.NewGuid():N}@example.invalid", DisplayName = "Tác giả", PasswordHash = "test-only" };
            db.Users.Add(owner);
            var post = new Post { AuthorId = owner.Id, Text = "Nội dung", PublishedAt = DateTime.UtcNow };
            db.Posts.Add(post); await db.SaveChangesAsync();
            db.Reports.Add(new Report { PostId = post.Id, ReporterId = adminId, Reason = "Spam", SnapshotText = post.Text });
            await db.SaveChangesAsync();
            using var refresh = await client.PostAsync("/api/auth/refresh", null);
            using (var json = JsonDocument.Parse(await refresh.Content.ReadAsStringAsync())) client.DefaultRequestHeaders.Authorization = new("Bearer", json.RootElement.GetProperty("accessToken").GetString());
            using var detail = await client.GetAsync($"/api/admin/report-groups/{post.Id}");
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            using var group = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
            var groupVersion = group.RootElement.GetProperty("groupVersion").GetString();
            using var barrier = new Barrier(2);
            async Task<HttpStatusCode> Decide(string decision)
            {
                await Task.Run(() => { if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15))) throw new TimeoutException(); });
                using var response = await client.PostAsJsonAsync($"/api/admin/report-groups/{post.Id}/decision", new { decision, reason = "Đã kiểm tra", groupVersion });
                return response.StatusCode;
            }
            var results = await Task.WhenAll(Decide("keep"), Decide("hide"));
            Assert.Single(results, x => x == HttpStatusCode.OK);
            Assert.Single(results, x => x == HttpStatusCode.Conflict);
            Assert.Equal(1, await db.ModerationActions.CountAsync(x => x.PostId == post.Id));
            Assert.False(await db.Reports.AnyAsync(x => x.PostId == post.Id && x.Status == "Pending"));
        }
    }
}
