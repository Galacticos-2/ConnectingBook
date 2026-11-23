using System.ComponentModel.DataAnnotations;

namespace Project_Web_ConnectingBook.Server.Contracts
{
    public record RegisterRequest(
        [Required, EmailAddress, StringLength(254)] string Email,
        [Required, StringLength(128, MinimumLength = 10)] string Password,
        [Required, StringLength(50, MinimumLength = 2)] string DisplayName);
    public record LoginRequest(
        [Required, EmailAddress, StringLength(254)] string Email,
        [Required, StringLength(128)] string Password);
    public record AuthUser(Guid Id, string DisplayName, string Role, Guid? AvatarMediaId);
    public record AuthResponse(string AccessToken, DateTime ExpiresAt, AuthUser User);
}
