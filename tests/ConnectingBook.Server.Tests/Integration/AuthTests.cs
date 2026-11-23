using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ConnectingBook.Server.Tests.Integration
{
    public class AuthTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
    {
        [SqlServerFact]
        public async Task Concurrent_refresh_has_one_rotation_and_replay_revokes_the_winning_family()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var client = factory.CreateHttpsClient();
            var email = Email(); var password = Password();
            await Register(client, email, password);
            using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
            login.EnsureSuccessStatusCode();
            var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie")).Split(';')[0];
            using var barrier = new Barrier(2);
            async Task<(HttpStatusCode Status, string? Cookie)> Rotate()
            {
                using var participant = factory.CreateHttpsClient(cookies: false);
                participant.DefaultRequestHeaders.Add("Cookie", cookie);
                await Task.Run(() => { if (!barrier.SignalAndWait(TimeSpan.FromSeconds(15))) throw new TimeoutException(); });
                using var response = await participant.PostAsync("/api/auth/refresh", null);
                return (response.StatusCode, response.Headers.TryGetValues("Set-Cookie", out var values) ? values.First().Split(';')[0] : null);
            }
            var results = await Task.WhenAll(Rotate(), Rotate());
            var winner = Assert.Single(results, x => x.Status == HttpStatusCode.OK);
            Assert.Single(results, x => x.Status == HttpStatusCode.Unauthorized);
            Assert.NotNull(winner.Cookie);
            using var next = factory.CreateHttpsClient(cookies: false);
            next.DefaultRequestHeaders.Add("Cookie", winner.Cookie);
            using var revoked = await next.PostAsync("/api/auth/refresh", null);
            Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        }

        private static string Email() => $"{Guid.NewGuid():N}@example.invalid";
        private static string Password() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));

        private static async Task Register(HttpClient client, string email, string password)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password, displayName = "Thành viên" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Member", json.RootElement.GetProperty("role").GetString());
            Assert.False(json.RootElement.TryGetProperty("passwordHash", out _));
        }

        [SqlServerFact]
        public async Task Register_rejects_case_insensitive_duplicate_and_invalid_input()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var client = factory.CreateHttpsClient();
            var email = Email(); var password = Password();
            await Register(client, email, password);
            using var duplicate = await client.PostAsJsonAsync("/api/auth/register", new { email = email.ToUpperInvariant(), password, displayName = "Khác" });
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            using var invalid = await client.PostAsJsonAsync("/api/auth/register", new { email = "invalid", password = "short", displayName = "X" });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }

        [SqlServerFact]
        public async Task Refresh_does_not_require_valid_access_and_replay_revokes_whole_family()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var client = factory.CreateHttpsClient();
            var email = Email(); var password = Password();
            await Register(client, email, password);
            using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
            Assert.Contains("httponly", cookie.ToLowerInvariant());
            Assert.Contains("secure", cookie.ToLowerInvariant());
            Assert.Contains("samesite=strict", cookie.ToLowerInvariant());
            client.DefaultRequestHeaders.Authorization = new("Bearer", "expired-or-invalid-access");
            using var refreshed = await client.PostAsync("/api/auth/refresh", null);
            Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
            using var replayClient = factory.CreateHttpsClient(cookies: false);
            replayClient.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
            using var replay = await replayClient.PostAsync("/api/auth/refresh", null);
            Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
            using var next = await client.PostAsync("/api/auth/refresh", null);
            Assert.Equal(HttpStatusCode.Unauthorized, next.StatusCode);
        }

        [SqlServerFact]
        public async Task Logout_revokes_refresh_cookie_and_cross_origin_is_denied()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var client = factory.CreateHttpsClient();
            var email = Email(); var password = Password();
            await Register(client, email, password);
            using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            client.DefaultRequestHeaders.Add("Origin", "https://untrusted.invalid");
            using var denied = await client.PostAsync("/api/auth/refresh", null);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            client.DefaultRequestHeaders.Remove("Origin");
            using var logout = await client.PostAsync("/api/auth/logout", null);
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
            using var refresh = await client.PostAsync("/api/auth/refresh", null);
            Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        }

        [SqlServerFact]
        public async Task Five_wrong_passwords_lock_account_and_unknown_email_has_same_error()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var client = factory.CreateHttpsClient();
            var email = Email(); var password = Password();
            await Register(client, email, password);
            using var known = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" });
            using var unknown = await client.PostAsJsonAsync("/api/auth/login", new { email = Email(), password = "wrong-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, known.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
            using var knownJson = JsonDocument.Parse(await known.Content.ReadAsStringAsync());
            using var unknownJson = JsonDocument.Parse(await unknown.Content.ReadAsStringAsync());
            Assert.Equal(knownJson.RootElement.GetProperty("detail").GetString(), unknownJson.RootElement.GetProperty("detail").GetString());
            for (var i = 0; i < 3; i++)
            {
                using var wrong = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" });
                Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            }
            using var fifth = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" });
            Assert.Equal(HttpStatusCode.TooManyRequests, fifth.StatusCode);
            Assert.NotNull(fifth.Headers.RetryAfter);
            using var locked = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
            Assert.Equal(HttpStatusCode.TooManyRequests, locked.StatusCode);
        }
    }
}
