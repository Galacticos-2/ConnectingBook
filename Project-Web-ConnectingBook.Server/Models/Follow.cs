namespace Project_Web_ConnectingBook.Server.Models
{
    public class Follow
    {
        public Guid FollowerId { get; set; }
        public Guid FollowedId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
