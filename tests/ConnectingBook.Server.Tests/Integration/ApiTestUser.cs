using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace ConnectingBook.Server.Tests.Integration
{
    public static class ApiTestUser
    {
        public static async Task<Guid> RegisterAndLogin(HttpClient client)
        {
            var email = $"{Guid.NewGuid():N}@example.invalid";
            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            using var registration = await client.PostAsJsonAsync("/api/auth/register", new { email, password, displayName = "Thành viên" });
            Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
            using var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            using var body = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.RootElement.GetProperty("accessToken").GetString());
            return body.RootElement.GetProperty("user").GetProperty("id").GetGuid();
        }
    }
}
