using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Project_Web_ConnectingBook.Server.Contracts;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Services;

namespace Project_Web_ConnectingBook.Server.Controllers
{
    [ApiController, Authorize]
    public class ReportsController(ModerationService moderation) : ControllerBase
    {
        [HttpPost("api/posts/{id:long}/reports"), EnableRateLimiting("reports")]
        public async Task<IActionResult> Report(long id, ReportRequest request, CancellationToken ct) => StatusCode(201, await moderation.ReportAsync(id, User.UserId(), request, ct));
        [HttpGet("api/me/reports")]
        public Task<Page<object>> MyReports([FromQuery] int limit = 20, [FromQuery] string? cursor = null, CancellationToken ct = default) => moderation.MyReportsAsync(User.UserId(), limit, cursor, ct);
    }
}
