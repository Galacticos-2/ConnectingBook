using System.Security.Claims;

namespace Project_Web_ConnectingBook.Server.Infrastructure
{
    public static class CurrentUser
    {
        public static Guid UserId(this ClaimsPrincipal principal) =>
            Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id : throw new ApiException(401, "authentication_required", "Vui lòng đăng nhập.");
    }
}
