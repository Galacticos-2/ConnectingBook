namespace Project_Web_ConnectingBook.Server.Models
{
    public class Post
    {
        public long Id { get; set; }
        public Guid AuthorId { get; set; }
        public string Type { get; set; } = "Post";
        public string Text { get; set; } = "";
        public string Status { get; set; } = "Visible";
        public string? ModerationReason { get; set; }
        public string? FailureCode { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public byte[] Version { get; set; } = [];
        public int ProcessingAttempts { get; set; }
        public DateTime? ProcessingStartedAt { get; set; }
    }
}
