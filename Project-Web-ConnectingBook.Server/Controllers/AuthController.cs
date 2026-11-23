using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Project_Web_ConnectingBook.Server.Contracts;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Services;

namespace Project_Web_ConnectingBook.Server.Controllers
{
    [ApiController, Route("api/auth"), AllowAnonymous]
    public class AuthController(AuthService auth, IConfiguration config) : ControllerBase
    {
        private const string CookieName = "connectingbook.refresh";

        [HttpPost("register"), EnableRateLimiting("register")]
        public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct) =>
            StatusCode(201, await auth.RegisterAsync(request, ct));

        [HttpPost("login"), EnableRateLimiting("login")]
        public async Task<AuthResponse> Login(LoginRequest request, CancellationToken ct)
        {
            ValidateOrigin();
            var session = await auth.LoginAsync(request, ct);
            SetCookie(session.RefreshToken);
            return session.Response;
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
        {
            ValidateOrigin();
            var cookie = ReadCookie();
            if (cookie is null) return InvalidSession();
            var session = await auth.UseRefreshAsync(cookie, false, ct);
            SetCookie(session!.RefreshToken);
            return session.Response;
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            ValidateOrigin();
            var cookie = ReadCookie();
            if (cookie is null)
            {
                Response.Cookies.Delete(CookieName, CookieOptions());
                return InvalidSession();
            }
            try { await auth.UseRefreshAsync(cookie, true, ct); }
            finally { Response.Cookies.Delete(CookieName, CookieOptions()); }
            return NoContent();
        }

        private string? ReadCookie() => Request.Cookies[CookieName] is string value && value.Length is > 0 and <= 128
            ? value : null;

        private ObjectResult InvalidSession()
        {
            Response.Headers.CacheControl = "no-store";
            return new ObjectResult(new ProblemDetails
            {
                Status = 401,
                Title = "Không thể thực hiện yêu cầu.",
                Detail = "Phiên đăng nhập không còn hợp lệ.",
                Instance = Request.Path,
                Extensions = { ["code"] = "invalid_session", ["correlationId"] = HttpContext.TraceIdentifier }
            })
            { StatusCode = 401, ContentTypes = { "application/problem+json" } };
        }

        private void ValidateOrigin()
        {
            if (Request.Headers.Origin.Count == 0) return; // Native API clients do not attach browser cookies automatically.
            var origin = Request.Headers.Origin.ToString();
            var allowed = config["Client:Origin"] ?? $"{Request.Scheme}://{Request.Host}";
            if (!string.Equals(origin, allowed, StringComparison.OrdinalIgnoreCase))
                throw new ApiException(403, "invalid_origin", "Nguồn yêu cầu không được cho phép.");
        }

        private static CookieOptions CookieOptions() => new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth"
        };
        private void SetCookie(string value)
        {
            var options = CookieOptions();
            options.MaxAge = TimeSpan.FromDays(7);
            Response.Cookies.Append(CookieName, value, options);
            Response.Headers.CacheControl = "no-store";
        }
    }
}
