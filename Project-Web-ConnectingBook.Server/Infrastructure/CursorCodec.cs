using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Project_Web_ConnectingBook.Server.Infrastructure
{
    public record CursorValue(string Kind, DateTime At, string Id);
    public record Page<T>(IReadOnlyList<T> Items, string? NextCursor);
    public static class CursorCodec
    {
        public static int Limit(int value) => value < 1
            ? throw new ApiException(400, "invalid_limit", "Giới hạn phải lớn hơn 0.") : Math.Min(value, 50);
        public static string Encode(string kind, DateTime at, long id) =>
            Base64UrlEncoder.Encode(JsonSerializer.Serialize(new CursorValue(kind, at, id.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        public static CursorValue? Decode(string? cursor, string kind)
        {
            if (cursor is null) return null;
            try
            {
                if (cursor.Length is < 1 or > 1024 || cursor.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')) throw new FormatException();
                var value = JsonSerializer.Deserialize<CursorValue>(Base64UrlEncoder.Decode(cursor));
                if (value is null || value.Kind != kind || value.At.Kind != DateTimeKind.Utc ||
                    !long.TryParse(value.Id, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var id) || id <= 0)
                    throw new FormatException();
                return value;
            }
            catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
            {
                throw new ApiException(400, "invalid_cursor", "Cursor không hợp lệ.");
            }
        }
    }
}
