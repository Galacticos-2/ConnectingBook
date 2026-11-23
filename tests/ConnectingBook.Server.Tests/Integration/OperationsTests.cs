using System.Net;
using System.Text.Json;

namespace ConnectingBook.Server.Tests.Integration
{
    public class OperationsTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
    {
        [SqlServerFact]
        public async Task Development_Swagger_opens_directly_but_application_APIs_still_require_authentication()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString, environment: "Development");
            using var client = factory.CreateHttpsClient();
            using var ui = await client.GetAsync("/swagger/index.html");
            Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
            Assert.Contains("swagger-ui-bundle.js", await ui.Content.ReadAsStringAsync());
            using var asset = await client.GetAsync("/swagger/swagger-ui-bundle.js");
            Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
            using var document = await client.GetAsync("/openapi/v1.json");
            Assert.Equal(HttpStatusCode.OK, document.StatusCode);
            using var json = JsonDocument.Parse(await document.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.GetProperty("paths").TryGetProperty("/api/auth/register", out _));
            using var feed = await client.GetAsync("/api/feed");
            Assert.Equal(HttpStatusCode.Unauthorized, feed.StatusCode);
            using var admin = await client.GetAsync("/api/admin/report-groups");
            Assert.Equal(HttpStatusCode.Unauthorized, admin.StatusCode);
        }

        [SqlServerFact]
        public async Task Health_is_public_but_API_document_and_UI_require_authentication()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var client = factory.CreateHttpsClient();
            using var live = await client.GetAsync("/health/live"); Assert.Equal(HttpStatusCode.OK, live.StatusCode);
            using var ready = await client.GetAsync("/health/ready"); Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
            using var anonymousDoc = await client.GetAsync("/openapi/v1.json"); Assert.Equal(HttpStatusCode.Unauthorized, anonymousDoc.StatusCode);
            using var anonymousUi = await client.GetAsync("/swagger/index.html"); Assert.Equal(HttpStatusCode.Unauthorized, anonymousUi.StatusCode);
            await ApiTestUser.RegisterAndLogin(client);
            using var document = await client.GetAsync("/openapi/v1.json"); Assert.Equal(HttpStatusCode.OK, document.StatusCode);
            using var json = JsonDocument.Parse(await document.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.GetProperty("paths").TryGetProperty("/api/feed", out _));
            using var ui = await client.GetAsync("/swagger/index.html"); Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        }
    }
}
