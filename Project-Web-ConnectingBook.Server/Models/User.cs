namespace Project_Web_ConnectingBook.Server.Models
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Bio { get; set; } = "";
        public Guid? AvatarMediaId { get; set; }
        public string Role { get; set; } = "Member";
        public string Status { get; set; } = "Active";
        public int FailedLoginCount { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
