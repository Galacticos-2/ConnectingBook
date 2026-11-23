using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SkiaSharp;

namespace ConnectingBook.Server.Tests.Integration
{
    public class PostsMediaTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
    {
        [SqlServerFact]
        public async Task Jpeg_orientation_is_applied_and_metadata_is_removed()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var owner = factory.CreateHttpsClient();
            await ApiTestUser.RegisterAndLogin(owner);
            using var bitmap = new SKBitmap(80, 40);
            bitmap.Erase(SKColors.Blue);
            using var image = SKImage.FromBitmap(bitmap);
            using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            var bytes = jpeg.ToArray();
            // JPEG APP1: Exif TIFF little-endian, orientation=6 (rotate 90 degrees).
            byte[] exif = [255, 225, 0, 34, 69, 120, 105, 102, 0, 0, 73, 73, 42, 0, 8, 0, 0, 0, 1, 0, 18, 1, 3, 0, 1, 0, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0];
            var oriented = bytes[..2].Concat(exif).Concat(bytes[2..]).ToArray();
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(oriented), "file", "rotated.jpg");
            using var uploaded = await owner.PostAsync("/api/media/images", form);
            Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
            using var metadata = JsonDocument.Parse(await uploaded.Content.ReadAsStringAsync());
            Assert.Equal(40, metadata.RootElement.GetProperty("width").GetInt32());
            Assert.Equal(80, metadata.RootElement.GetProperty("height").GetInt32());
            var id = metadata.RootElement.GetProperty("imageId").GetString();
            using var response = await owner.GetAsync($"/api/media/{id}");
            response.EnsureSuccessStatusCode();
            var normalized = await response.Content.ReadAsByteArrayAsync();
            using var data = SKData.CreateCopy(normalized);
            using var codec = SKCodec.Create(data);
            Assert.Equal(SKEncodedImageFormat.Png, codec.EncodedFormat);
            Assert.Equal(SKEncodedOrigin.TopLeft, codec.EncodedOrigin);
            Assert.False(System.Text.Encoding.Latin1.GetString(normalized).Contains("Exif", StringComparison.Ordinal));
        }

        [SqlServerFact]
        public async Task Image_limits_corruption_avatar_crop_and_private_conditional_responses_are_enforced()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var owner = factory.CreateHttpsClient();
            using var stranger = factory.CreateHttpsClient();
            await ApiTestUser.RegisterAndLogin(owner);
            await ApiTestUser.RegisterAndLogin(stranger);
            async Task<HttpResponseMessage> Upload(byte[] bytes, string purpose = "post")
            {
                using var form = new MultipartFormDataContent();
                form.Add(new ByteArrayContent(bytes), "file", "sample.png");
                form.Add(new StringContent(purpose), "purpose");
                return await owner.PostAsync("/api/media/images", form);
            }
            using var oversized = await Upload(new byte[5 * 1024 * 1024 + 1]);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
            using var avatarOversized = await Upload(new byte[2 * 1024 * 1024 + 1], "avatar");
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, avatarOversized.StatusCode);
            using var corrupt = await Upload([137, 80, 78, 71, 13, 10, 26, 10, 0]);
            Assert.Equal(HttpStatusCode.BadRequest, corrupt.StatusCode);
            using var wide = new SKBitmap(4097, 1);
            using var wideImage = SKImage.FromBitmap(wide);
            using var wideEncoded = wideImage.Encode(SKEncodedImageFormat.Png, 100);
            using var dimensions = await Upload(wideEncoded.ToArray());
            Assert.Equal(HttpStatusCode.BadRequest, dimensions.StatusCode);
            using var bitmap = new SKBitmap(80, 40);
            bitmap.Erase(SKColors.Green);
            using var image = SKImage.FromBitmap(bitmap);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            using var avatar = await Upload(encoded.ToArray(), "avatar");
            Assert.Equal(HttpStatusCode.Created, avatar.StatusCode);
            using var metadata = JsonDocument.Parse(await avatar.Content.ReadAsStringAsync());
            Assert.Equal(40, metadata.RootElement.GetProperty("width").GetInt32());
            Assert.Equal(40, metadata.RootElement.GetProperty("height").GetInt32());
            var id = metadata.RootElement.GetProperty("imageId").GetString();
            using var media = await owner.GetAsync($"/api/media/{id}");
            Assert.Equal(HttpStatusCode.OK, media.StatusCode);
            var etag = Assert.Single(media.Headers.GetValues("ETag"));
            using var condition = new HttpRequestMessage(HttpMethod.Get, $"/api/media/{id}");
            condition.Headers.TryAddWithoutValidation("If-None-Match", etag);
            using var unchanged = await owner.SendAsync(condition);
            Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);
            using var foreign = new HttpRequestMessage(HttpMethod.Get, $"/api/media/{id}");
            foreign.Headers.TryAddWithoutValidation("If-None-Match", etag);
            using var denied = await stranger.SendAsync(foreign);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            using var foreignAvatar = await stranger.PatchAsJsonAsync("/api/me/profile", new { avatarMediaId = id });
            Assert.Equal(HttpStatusCode.BadRequest, foreignAvatar.StatusCode);
            using var profile = await owner.PatchAsJsonAsync("/api/me/profile", new { displayName = "Updated name", bio = "Updated bio", avatarMediaId = id, role = "Admin" });
            Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
            using var publicAvatar = await stranger.GetAsync($"/api/media/{id}");
            Assert.Equal(HttpStatusCode.OK, publicAvatar.StatusCode);
            using var roleDenied = await owner.GetAsync("/api/admin/report-groups");
            Assert.Equal(HttpStatusCode.Forbidden, roleDenied.StatusCode);
        }

        [SqlServerFact]
        public async Task Posts_require_auth_and_only_owner_can_edit_or_delete()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var anonymous = factory.CreateHttpsClient();
            using var noAuth = await anonymous.PostAsJsonAsync("/api/posts", new { text = "Bài mới", imageIds = Array.Empty<Guid>() });
            Assert.Equal(HttpStatusCode.Unauthorized, noAuth.StatusCode);
            using var author = factory.CreateHttpsClient();
            using var stranger = factory.CreateHttpsClient();
            await ApiTestUser.RegisterAndLogin(author);
            await ApiTestUser.RegisterAndLogin(stranger);
            using var created = await author.PostAsJsonAsync("/api/posts", new { text = "Bài của tôi", imageIds = Array.Empty<Guid>() });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            var id = json.RootElement.GetProperty("id").GetString();
            var version = json.RootElement.GetProperty("version").GetString();
            using var edit = new HttpRequestMessage(HttpMethod.Patch, $"/api/posts/{id}") { Content = JsonContent.Create(new { text = "Sửa trái quyền" }) };
            edit.Headers.TryAddWithoutValidation("If-Match", version);
            using var denied = await stranger.SendAsync(edit);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/posts/{id}");
            delete.Headers.TryAddWithoutValidation("If-Match", version);
            using var deleteDenied = await stranger.SendAsync(delete);
            Assert.Equal(HttpStatusCode.Forbidden, deleteDenied.StatusCode);
            using var loaded = await author.GetAsync($"/api/posts/{id}");
            Assert.Equal(HttpStatusCode.OK, loaded.StatusCode);
        }

        [SqlServerFact]
        public async Task Follow_is_idempotent_and_rejects_self_and_fake_image()
        {
            await using var factory = new ApiFactory(fixture.ConnectionString);
            using var client = factory.CreateHttpsClient();
            using var other = factory.CreateHttpsClient();
            var me = await ApiTestUser.RegisterAndLogin(client);
            var target = await ApiTestUser.RegisterAndLogin(other);
            using var self = await client.PutAsync($"/api/users/{me}/follow", null);
            Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
            for (var i = 0; i < 2; i++)
            {
                using var follow = await client.PutAsync($"/api/users/{target}/follow", null);
                Assert.Equal(HttpStatusCode.NoContent, follow.StatusCode);
            }
            using var body = new MultipartFormDataContent();
            body.Add(new ByteArrayContent([77, 90, 0, 0]), "file", "fake.jpg");
            using var upload = await client.PostAsync("/api/media/images", body);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, upload.StatusCode);
        }
    }
}
