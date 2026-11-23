using Project_Web_ConnectingBook.Server.Infrastructure;

namespace ConnectingBook.Server.Tests.Unit
{
    public class CursorTests
    {
        [Fact]
        public void Cursor_preserves_full_SQL_Server_time_precision_and_long_id()
        {
            var time = new DateTime(2026, 11, 22, 1, 2, 3, DateTimeKind.Utc).AddTicks(1234567);
            var encoded = CursorCodec.Encode("feed", time, long.MaxValue);
            var decoded = CursorCodec.Decode(encoded, "feed");
            Assert.Equal(time, decoded!.At);
            Assert.Equal(long.MaxValue.ToString(), decoded.Id);
            Assert.Throws<ApiException>(() => CursorCodec.Decode(encoded, "comments"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("%%%")]
        [InlineData("e30")]
        [InlineData("bnVsbA")]
        public void Rejects_invalid_cursor(string value) => Assert.Throws<ApiException>(() => CursorCodec.Decode(value, "feed"));

        [Fact]
        public void Limits_are_bounded() { Assert.Equal(50, CursorCodec.Limit(100)); Assert.Throws<ApiException>(() => CursorCodec.Limit(0)); }
    }
}
