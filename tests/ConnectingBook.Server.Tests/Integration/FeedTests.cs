using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Project_Web_ConnectingBook.Server.Models;

namespace ConnectingBook.Server.Tests.Integration
{
    public class FeedTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
    {
        [SqlServerFact]
        public async Task Subsequent_page_rechecks_follow_and_visibility_and_rejects_invalid_limits()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var reader = factory.CreateHttpsClient();
            using var author = factory.CreateHttpsClient();
            var me = await ApiTestUser.RegisterAndLogin(reader);
            var other = await ApiTestUser.RegisterAndLogin(author);
            using var followed = await reader.PutAsync($"/api/users/{other}/follow", null);
            Assert.Equal(HttpStatusCode.NoContent, followed.StatusCode);
            await using var db = fixture.CreateContext();
            var time = DateTime.UtcNow.AddMinutes(-5);
            var anchor = new Post { AuthorId = me, Text = "Anchor", PublishedAt = time };
            var hidden = new Post { AuthorId = me, Text = "Hidden later", PublishedAt = time.AddSeconds(-1) };
            var unfollowed = new Post { AuthorId = other, Text = "Unfollowed later", PublishedAt = time.AddSeconds(-2) };
            db.Posts.AddRange(anchor, hidden, unfollowed);
            await db.SaveChangesAsync();
            using var first = await reader.GetAsync("/api/feed?limit=1");
            first.EnsureSuccessStatusCode();
            using var page = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
            var cursor = page.RootElement.GetProperty("nextCursor").GetString();
            Assert.NotNull(cursor);
            hidden.Status = "Hidden";
            hidden.ModerationReason = "Test visibility";
            await db.SaveChangesAsync();
            using var unfollow = await reader.DeleteAsync($"/api/users/{other}/follow");
            Assert.Equal(HttpStatusCode.NoContent, unfollow.StatusCode);
            using var second = await reader.GetAsync($"/api/feed?limit=1&cursor={Uri.EscapeDataString(cursor!)}");
            second.EnsureSuccessStatusCode();
            using var next = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
            Assert.Empty(next.RootElement.GetProperty("items").EnumerateArray());
            foreach (var limit in new[] { 0, -1 })
            {
                using var invalid = await reader.GetAsync($"/api/feed?limit={limit}");
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            }
            using var bounded = await reader.GetAsync("/api/feed?limit=51");
            Assert.Equal(HttpStatusCode.OK, bounded.StatusCode);
            using var boundedPage = JsonDocument.Parse(await bounded.Content.ReadAsStringAsync());
            Assert.InRange(boundedPage.RootElement.GetProperty("items").GetArrayLength(), 0, 50);
        }

        [SqlServerFact]
        public async Task Ties_new_insert_and_deleted_anchor_do_not_duplicate_pages()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var client = factory.CreateHttpsClient();
            var me = await ApiTestUser.RegisterAndLogin(client);
            await using var db = fixture.CreateContext();
            var time = DateTime.UtcNow.AddMinutes(-5);
            var original = Enumerable.Range(0, 4).Select(i => new Post { AuthorId = me, Text = $"Bài {i}", PublishedAt = time }).ToArray();
            db.Posts.AddRange(original); await db.SaveChangesAsync();
            using var first = await client.GetAsync("/api/feed?limit=2");
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            using var page1 = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
            var cursor = page1.RootElement.GetProperty("nextCursor").GetString();
            var firstIds = page1.RootElement.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray();
            var anchor = original.Single(x => x.Id.ToString() == firstIds[1]);
            anchor.Status = "Deleted"; anchor.DeletedAt = DateTime.UtcNow;
            db.Posts.Add(new Post { AuthorId = me, Text = "Mới", PublishedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            using var second = await client.GetAsync($"/api/feed?limit=2&cursor={Uri.EscapeDataString(cursor!)}");
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            using var page2 = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
            var secondIds = page2.RootElement.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray();
            Assert.Equal(2, secondIds.Length);
            Assert.Empty(firstIds.Intersect(secondIds));
            Assert.Equal(JsonValueKind.Null, page2.RootElement.GetProperty("nextCursor").ValueKind);
            using var invalid = await client.GetAsync("/api/feed?cursor=bad");
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
    }
}
