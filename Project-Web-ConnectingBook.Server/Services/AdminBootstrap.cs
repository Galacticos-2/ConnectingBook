using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Data;
using Project_Web_ConnectingBook.Server.Models;

namespace Project_Web_ConnectingBook.Server.Services
{
    public static class AdminBootstrap
    {
        public static async Task RunAsync(IServiceProvider services, CancellationToken ct = default)
        {
            var email = Environment.GetEnvironmentVariable("ADMIN_EMAIL");
            var password = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrEmpty(password)) return;
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password) || password.Length is < 10 or > 128 ||
                !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) || email.Length > 254)
                throw new InvalidOperationException("Admin bootstrap requires valid credentials provided through environment variables.");
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ConnectingBookDbContext>();
            var normalized = email.Trim().ToLowerInvariant();
            var existing = await db.Users.SingleOrDefaultAsync(x => x.Email == normalized, ct);
            if (existing is not null)
            {
                if (existing.Role != "Admin") throw new InvalidOperationException("Bootstrap cannot elevate an existing member.");
                return;
            }
            var user = new User { Email = normalized, DisplayName = "Quản trị viên", Role = "Admin" };
            user.PasswordHash = scope.ServiceProvider.GetRequiredService<PasswordHasher<User>>().HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }
    }
}
