namespace Project_Web_ConnectingBook.Server.Models
{
    public class Report
    {
        public long Id { get; set; }
        public long PostId { get; set; }
        public Guid ReporterId { get; set; }
        public string Reason { get; set; } = "";
        public string? Description { get; set; }
        public string SnapshotText { get; set; } = "";
        public Guid[] SnapshotMediaIds { get; set; } = [];
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ClosedAt { get; set; }
        public long? ModerationActionId { get; set; }
    }
}
