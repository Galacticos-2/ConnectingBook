using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Project_Web_ConnectingBook.Server.Contracts;
using Project_Web_ConnectingBook.Server.Data;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Models;

namespace Project_Web_ConnectingBook.Server.Services
{
    public record IssuedSession(AuthResponse Response, string RefreshToken);

    public class AuthService(ConnectingBookDbContext db, JwtSettings jwt, PasswordHasher<User> hasher)
    {
        public async Task<AuthUser> RegisterAsync(RegisterRequest request, CancellationToken ct)
        {
            var name = request.DisplayName.Trim();
            if (name.Length < 2) throw new ApiException(400, "invalid_name", "Tên hiển thị phải có ít nhất 2 ký tự.");
            var user = new User { Email = request.Email.Trim().ToLowerInvariant(), DisplayName = name };
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            return ToUser(user);
        }

        public async Task<IssuedSession> LoginAsync(LoginRequest request, CancellationToken ct)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var user = await db.Users.FromSqlInterpolated($"SELECT * FROM [Users] WITH (UPDLOCK, ROWLOCK) WHERE [Email] = {email}").SingleOrDefaultAsync(ct);
            if (user is null)
            {
                // Hash on the unknown-account path as well; no email existence detail is returned.
                hasher.HashPassword(new User(), request.Password);
                throw InvalidLogin();
            }
            if (user.LockoutEnd > DateTime.UtcNow) throw Locked(user.LockoutEnd.Value);
            if (user.LockoutEnd is not null)
            {
                user.FailedLoginCount = 0;
                user.LockoutEnd = null;
            }
            var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
            if (user.Status != "Active" || verification == PasswordVerificationResult.Failed)
            {
                user.FailedLoginCount++;
                if (user.FailedLoginCount >= 5) user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                if (user.LockoutEnd is DateTime lockedUntil) throw Locked(lockedUntil);
                throw InvalidLogin();
            }
            user.FailedLoginCount = 0;
            user.LockoutEnd = null;
            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
                user.PasswordHash = hasher.HashPassword(user, request.Password);
            var session = Issue(user, Guid.NewGuid());
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return session;
        }

        public async Task<IssuedSession?> UseRefreshAsync(string rawToken, bool logout, CancellationToken ct)
        {
            var hash = Hash(rawToken);
            var owner = await db.RefreshTokens.AsNoTracking().Where(x => x.TokenHash == hash)
                .Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(ct);
            if (owner is null) throw InvalidSession();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var user = await db.Users.FromSqlInterpolated($"SELECT * FROM [Users] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {owner.Value}").SingleAsync(ct);
            var token = await db.RefreshTokens.SingleAsync(x => x.TokenHash == hash, ct);
            if (token.RevokedAt is not null || token.ExpiresAt <= DateTime.UtcNow || user.Status != "Active")
            {
                await db.RefreshTokens.Where(x => x.FamilyId == token.FamilyId && x.RevokedAt == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
                await transaction.CommitAsync(ct);
                throw InvalidSession();
            }
            token.RevokedAt = DateTime.UtcNow;
            IssuedSession? issued = null;
            if (logout)
            {
                await db.RefreshTokens.Where(x => x.FamilyId == token.FamilyId && x.RevokedAt == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.RevokedAt, DateTime.UtcNow), ct);
            }
            else
            {
                issued = Issue(user, token.FamilyId);
                var nextHash = Hash(issued.RefreshToken);
                token.ReplacedByTokenId = db.RefreshTokens.Local.Single(x => x.TokenHash == nextHash).Id;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return issued;
        }

        private IssuedSession Issue(User user, Guid family)
        {
            var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));
            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TokenHash = Hash(raw),
                FamilyId = family,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });
            var expiresAt = DateTime.UtcNow.AddMinutes(15);
            var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience,
                [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Role, user.Role)],
                notBefore: DateTime.UtcNow, expires: expiresAt,
                signingCredentials: new SigningCredentials(jwt.Key, SecurityAlgorithms.HmacSha256));
            return new IssuedSession(new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAt, ToUser(user)), raw);
        }

        private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        private static AuthUser ToUser(User user) => new(user.Id, user.DisplayName, user.Role, user.AvatarMediaId);
        private static ApiException InvalidLogin() => new(401, "invalid_credentials", "Email hoặc mật khẩu không đúng.");
        private static ApiException InvalidSession() => new(401, "invalid_session", "Phiên đăng nhập không còn hợp lệ.");
        private static ApiException Locked(DateTime until) => new(429, "account_locked", "Vui lòng thử lại sau.", Math.Max(1, (int)Math.Ceiling((until - DateTime.UtcNow).TotalSeconds)));
    }
}
