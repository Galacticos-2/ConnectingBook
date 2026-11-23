namespace Project_Web_ConnectingBook.Server.Models
{
    public class ModerationAction
    {
        public long Id { get; set; }
        public long PostId { get; set; }
        public Guid? ActorId { get; set; }
        public string Decision { get; set; } = "";
        public string Reason { get; set; } = "";
        public string PreviousStatus { get; set; } = "";
        public string NextStatus { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
