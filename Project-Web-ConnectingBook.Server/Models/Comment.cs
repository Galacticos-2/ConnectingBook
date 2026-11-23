namespace Project_Web_ConnectingBook.Server.Models
{
    public class Comment
    {
        public long Id { get; set; }
        public long PostId { get; set; }
        public Guid AuthorId { get; set; }
        public string Text { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? EditedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
