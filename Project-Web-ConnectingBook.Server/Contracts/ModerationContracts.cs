using System.ComponentModel.DataAnnotations;

namespace Project_Web_ConnectingBook.Server.Contracts
{
    public record ReportRequest([Required] string Reason, [StringLength(500)] string? Description);
    public record DecisionRequest([Required] string Decision, [Required, StringLength(1000, MinimumLength = 1)] string Reason, [Required] string GroupVersion);
    public record RestoreRequest([Required, StringLength(1000, MinimumLength = 1)] string Reason);
}
