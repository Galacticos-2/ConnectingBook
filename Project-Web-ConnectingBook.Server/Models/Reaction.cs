namespace Project_Web_ConnectingBook.Server.Models
{
    public class Reaction
    {
        public long PostId { get; set; }
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
