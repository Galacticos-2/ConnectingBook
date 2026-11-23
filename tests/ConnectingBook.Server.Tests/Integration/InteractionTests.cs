using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Project_Web_ConnectingBook.Server.Models;

namespace ConnectingBook.Server.Tests.Integration
{
    public class InteractionTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
    {
        [SqlServerFact]
        public async Task Comment_cursor_handles_timestamp_ties_and_only_author_can_mutate()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var owner = factory.CreateHttpsClient();
            using var stranger = factory.CreateHttpsClient();
            var author = await ApiTestUser.RegisterAndLogin(owner);
            await ApiTestUser.RegisterAndLogin(stranger);
            await using var db = fixture.CreateContext();
            var post = new Post { AuthorId = author, Text = "Comments", PublishedAt = DateTime.UtcNow };
            db.Posts.Add(post); await db.SaveChangesAsync();
            var timestamp = DateTime.UtcNow;
            var comments = Enumerable.Range(0, 3).Select(index => new Comment { PostId = post.Id, AuthorId = author, Text = $"Comment {index}", CreatedAt = timestamp }).ToArray();
            db.Comments.AddRange(comments); await db.SaveChangesAsync();
            using var first = await owner.GetAsync($"/api/posts/{post.Id}/comments?limit=2");
            first.EnsureSuccessStatusCode();
            using var page = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
            var ids = page.RootElement.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToArray();
            Assert.Equal(comments.Take(2).Select(x => x.Id.ToString()), ids);
            var cursor = page.RootElement.GetProperty("nextCursor").GetString();
            using var second = await owner.GetAsync($"/api/posts/{post.Id}/comments?limit=2&cursor={Uri.EscapeDataString(cursor!)}");
            second.EnsureSuccessStatusCode();
            using var next = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
            Assert.Equal(comments[2].Id.ToString(), Assert.Single(next.RootElement.GetProperty("items").EnumerateArray()).GetProperty("id").GetString());
            using var deniedEdit = await stranger.PatchAsJsonAsync($"/api/comments/{comments[0].Id}", new { text = "Unauthorized" });
            Assert.Equal(HttpStatusCode.Forbidden, deniedEdit.StatusCode);
            using var deniedDelete = await stranger.DeleteAsync($"/api/comments/{comments[0].Id}");
            Assert.Equal(HttpStatusCode.Forbidden, deniedDelete.StatusCode);
            using var edited = await owner.PatchAsJsonAsync($"/api/comments/{comments[0].Id}", new { text = "  Updated  " });
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
            await db.Entry(comments[0]).ReloadAsync();
            Assert.Equal("Updated", comments[0].Text);
            using var deleted = await owner.DeleteAsync($"/api/comments/{comments[0].Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        }

        [SqlServerFact]
        public async Task Likes_are_idempotent_and_hidden_post_comments_return_not_found()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var client = factory.CreateHttpsClient();
            var me = await ApiTestUser.RegisterAndLogin(client);
            await using var db = fixture.CreateContext();
            var post = new Post { AuthorId = me, Text = "Thử tương tác", PublishedAt = DateTime.UtcNow };
            db.Posts.Add(post); await db.SaveChangesAsync();
            for (var i = 0; i < 2; i++) { using var like = await client.PutAsync($"/api/posts/{post.Id}/like", null); Assert.Equal(HttpStatusCode.NoContent, like.StatusCode); }
            Assert.Single(db.Reactions.Where(x => x.PostId == post.Id));
            using var comment = await client.PostAsJsonAsync($"/api/posts/{post.Id}/comments", new { text = "  Bình luận  " });
            Assert.Equal(HttpStatusCode.Created, comment.StatusCode);
            using var body = JsonDocument.Parse(await comment.Content.ReadAsStringAsync());
            var commentId = body.RootElement.GetProperty("id").GetString();
            post.Status = "Hidden"; post.ModerationReason = "Kiểm tra"; await db.SaveChangesAsync();
            using var list = await client.GetAsync($"/api/posts/{post.Id}/comments");
            Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
            using var edit = await client.PatchAsJsonAsync($"/api/comments/{commentId}", new { text = "Sửa" });
            Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        }
    }
}
