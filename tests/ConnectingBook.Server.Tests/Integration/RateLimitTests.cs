using System.Net;
using System.Net.Http.Json;

namespace ConnectingBook.Server.Tests.Integration
{
    public class RateLimitTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
    {
        [SqlServerFact]
        public async Task Login_partition_is_per_email_and_returns_retry_after()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString, new() { ["RateLimits:Login"] = "1" });
            using var client = factory.CreateHttpsClient();
            var email = $"{Guid.NewGuid():N}@example.invalid";
            using var first = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
            using var second = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" });
            Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
            Assert.NotNull(second.Headers.RetryAfter);
            using var different = await client.PostAsJsonAsync("/api/auth/login", new { email = $"other-{email}", password = "wrong-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, different.StatusCode);
        }
    }
}
