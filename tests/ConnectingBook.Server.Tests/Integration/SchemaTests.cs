using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Models;

namespace ConnectingBook.Server.Tests.Integration
{
    public class SchemaTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
    {
        private static User NewUser() => new()
        {
            Email = $"{Guid.NewGuid():N}@example.invalid",
            DisplayName = "Thành viên",
            PasswordHash = "test-only-not-a-real-password-hash"
        };

        [SqlServerFact]
        public async Task Migration_is_repeatable_and_preserves_UTC_ticks()
        {
            await using var db = fixture.CreateContext();
            await db.Database.MigrateAsync();
            var user = NewUser();
            user.CreatedAt = new DateTime(2026, 11, 22, 1, 2, 3, DateTimeKind.Utc).AddTicks(1234567);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var loaded = await db.Users.SingleAsync(x => x.Id == user.Id);
            Assert.Equal(user.CreatedAt.Ticks, loaded.CreatedAt.Ticks);
            Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.Kind);
            Assert.Single(await db.Database.GetAppliedMigrationsAsync());
        }

        [SqlServerFact]
        public async Task Email_is_unique_case_insensitively_and_self_follow_is_rejected()
        {
            await using var db = fixture.CreateContext();
            var user = NewUser();
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.Users.Add(new User { Email = user.Email.ToUpperInvariant(), DisplayName = "Khác", PasswordHash = "test-only" });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            db.Follows.Add(new Follow { FollowerId = user.Id, FollowedId = user.Id });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        [SqlServerFact]
        public async Task Pending_reports_are_unique_and_closed_report_does_not_prevent_new_report()
        {
            await using var db = fixture.CreateContext();
            var author = NewUser();
            var reporter = NewUser();
            db.Users.AddRange(author, reporter);
            var post = new Post { AuthorId = author.Id, Text = "Nội dung", PublishedAt = DateTime.UtcNow };
            db.Posts.Add(post);
            await db.SaveChangesAsync();
            var report = new Report { PostId = post.Id, ReporterId = reporter.Id, Reason = "Spam", SnapshotText = post.Text };
            db.Reports.Add(report);
            await db.SaveChangesAsync();
            db.Reports.Add(new Report { PostId = post.Id, ReporterId = reporter.Id, Reason = "Spam", SnapshotText = post.Text });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            var action = new ModerationAction { PostId = post.Id, ActorId = author.Id, Decision = "Keep", Reason = "Giữ nguyên", PreviousStatus = "Visible", NextStatus = "Visible" };
            db.ModerationActions.Add(action);
            await db.SaveChangesAsync();
            var pending = await db.Reports.SingleAsync(x => x.Id == report.Id);
            pending.Status = "ClosedKept";
            pending.ClosedAt = DateTime.UtcNow;
            pending.ModerationActionId = action.Id;
            await db.SaveChangesAsync();
            db.Reports.Add(new Report { PostId = post.Id, ReporterId = reporter.Id, Reason = "Spam", SnapshotText = post.Text });
            await db.SaveChangesAsync();
        }

        [SqlServerFact]
        public async Task Concurrent_updates_have_exactly_one_winner()
        {
            await using var setup = fixture.CreateContext();
            var author = NewUser();
            setup.Users.Add(author);
            var post = new Post { AuthorId = author.Id, Text = "Bản gốc", PublishedAt = DateTime.UtcNow };
            setup.Posts.Add(post);
            await setup.SaveChangesAsync();
            using var barrier = new Barrier(2);
            async Task<bool> Update(string text)
            {
                await using var db = fixture.CreateContext();
                var loaded = await db.Posts.SingleAsync(x => x.Id == post.Id);
                loaded.Text = text;
                await Task.Run(() =>
                {
                    if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15)))
                        throw new TimeoutException("Concurrency barrier timed out.");
                });
                try { await db.SaveChangesAsync(); return true; }
                catch (DbUpdateConcurrencyException) { return false; }
            }
            var outcomes = await Task.WhenAll(Update("Một"), Update("Hai"));
            Assert.Single(outcomes, result => result);
            Assert.Single(outcomes, result => !result);
        }
    }
}
