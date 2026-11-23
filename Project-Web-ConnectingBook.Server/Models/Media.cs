namespace Project_Web_ConnectingBook.Server.Models
{
    public class Media
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OwnerId { get; set; }
        public long? PostId { get; set; }
        public string Kind { get; set; } = "Image";
        public string StorageKey { get; set; } = "";
        public string ContentType { get; set; } = "";
        public long SizeBytes { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double? DurationSeconds { get; set; }
        public int? Position { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid? ParentMediaId { get; set; }
    }
}
