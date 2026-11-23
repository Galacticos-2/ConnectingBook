using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Models;

namespace ConnectingBook.Server.Tests.Unit
{
    public class AccountSqlGeneratorTests
    {
        [Theory]
        [InlineData("admin", "Admin")]
        [InlineData("user", "Member")]
        public void Generated_SQL_uses_compatible_hash_not_plain_password(string role, string expectedRole)
        {
            var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var script = AccountSqlGenerator.CreateScript("chosen@example.invalid", "Tên được chọn", password, role);
            Assert.DoesNotContain(password, script);
            Assert.Contains($"N'{expectedRole}'", script);
            Assert.Contains("USE [ConnectingBookSystemDb]", script);
            var hash = Regex.Match(script, "N'(?<hash>AQ[^']+)'").Groups["hash"].Value;
            Assert.NotEmpty(hash);
            Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<User>().VerifyHashedPassword(new User(), hash, password));
        }

        [Fact]
        public void User_text_is_escaped_and_unsupported_role_is_rejected()
        {
            var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var script = AccountSqlGenerator.CreateScript("chosen@example.invalid", "O'Brien", password, "user");
            Assert.Contains("N'O''Brien'", script);
            Assert.Throws<ArgumentException>(() => AccountSqlGenerator.CreateScript("chosen@example.invalid", "Thành viên", password, "superadmin"));
        }
    }
}
