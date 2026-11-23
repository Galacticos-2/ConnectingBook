using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Project_Web_ConnectingBook.Server.Models;

namespace Project_Web_ConnectingBook.Server.Infrastructure
{
    public static class AccountSqlGenerator
    {
        public static void Run()
        {
            if (Console.IsInputRedirected) throw new InvalidOperationException("Chạy lệnh trong terminal tương tác để nhập mật khẩu ẩn.");
            Console.Write("Email: "); var email = Console.ReadLine() ?? "";
            Console.Write("Tên hiển thị: "); var displayName = Console.ReadLine() ?? "";
            Console.Write("Vai trò (admin/user): "); var role = Console.ReadLine() ?? "";
            Console.Write("Mật khẩu (10–128 ký tự): "); var password = ReadPassword();
            Console.Write("Nhập lại mật khẩu: "); var confirmation = ReadPassword();
            if (password != confirmation) throw new InvalidOperationException("Hai lần nhập mật khẩu không khớp.");
            var script = CreateScript(email, displayName, password, role);
            Console.WriteLine();
            Console.WriteLine("--- Copy phần SQL dưới đây vào SSMS. Chưa có thao tác nào trên database. ---");
            Console.WriteLine(script);
        }

        public static string CreateScript(string email, string displayName, string password, string role)
        {
            email = email.Trim().ToLowerInvariant(); displayName = displayName.Trim();
            if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email)) throw new ArgumentException("Email không hợp lệ.");
            if (displayName.Length is < 2 or > 50) throw new ArgumentException("Tên hiển thị phải có từ 2 đến 50 ký tự.");
            if (password.Length is < 10 or > 128) throw new ArgumentException("Mật khẩu phải có từ 10 đến 128 ký tự.");
            var actualRole = role.Trim().ToLowerInvariant() switch
            {
                "admin" => "Admin",
                "user" or "member" => "Member",
                _ => throw new ArgumentException("Vai trò phải là admin hoặc user.")
            };
            var user = new User { Email = email, DisplayName = displayName, Role = actualRole };
            var hash = new PasswordHasher<User>().HashPassword(user, password);
            static string SqlText(string value) => value.Replace("'", "''");
            return $"""
                USE [ConnectingBookSystemDb];
                SET XACT_ABORT ON;
                BEGIN TRANSACTION;

                DECLARE @Email nvarchar(254) = N'{SqlText(email)}';
                IF EXISTS (SELECT 1 FROM [dbo].[Users] WITH (UPDLOCK, HOLDLOCK) WHERE [Email] = @Email)
                BEGIN
                    ROLLBACK TRANSACTION;
                    THROW 50001, N'Email đã tồn tại. Không thay đổi tài khoản hiện có.', 1;
                END;

                INSERT INTO [dbo].[Users]
                    ([Id], [Email], [PasswordHash], [DisplayName], [Bio], [AvatarMediaId],
                     [Role], [Status], [FailedLoginCount], [LockoutEnd], [CreatedAt])
                VALUES
                    (NEWID(), @Email, N'{hash}', N'{SqlText(displayName)}', N'', NULL,
                     N'{actualRole}', N'Active', 0, NULL, SYSUTCDATETIME());

                COMMIT TRANSACTION;
                SELECT [Id], [Email], [DisplayName], [Role], [Status]
                FROM [dbo].[Users] WHERE [Email] = @Email;
                """;
        }

        private static string ReadPassword()
        {
            var value = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (value.Length > 0) { value.Length--; Console.Write("\b \b"); }
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    if (value.Length >= 128) throw new InvalidOperationException("Mật khẩu tối đa 128 ký tự; không tự cắt ngắn mật khẩu.");
                    value.Append(key.KeyChar); Console.Write('*');
                }
            }
        }
    }
}
