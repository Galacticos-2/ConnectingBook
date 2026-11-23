using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Project_Web_ConnectingBook.Server.Infrastructure
{
    public static class RateLimitPolicies
    {
        public static IServiceCollection AddConnectingBookRateLimits(this IServiceCollection services, IConfiguration config)
        {
            services.AddRateLimiter(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    CreatePartition(context, "General", 300, TimeSpan.FromMinutes(1), config));
                Add("login", "Login", 5, TimeSpan.FromMinutes(1));
                Add("register", "Register", 5, TimeSpan.FromHours(1));
                Add("images", "Images", 20, TimeSpan.FromHours(1));
                Add("reels", "Reels", 5, TimeSpan.FromHours(1));
                Add("reports", "Reports", 20, TimeSpan.FromHours(1));
                Add("interactions", "Interactions", 60, TimeSpan.FromMinutes(1));
                options.OnRejected = async (context, ct) =>
                {
                    var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
                        ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)) : 60;
                    context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
                    context.HttpContext.Response.StatusCode = 429;
                    await context.HttpContext.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Status = 429,
                        Title = "Vượt giới hạn yêu cầu. Vui lòng thử lại sau.",
                        Extensions = { ["code"] = "rate_limited" }
                    }, options: null, contentType: "application/problem+json", cancellationToken: ct);
                };
                void Add(string policy, string group, int limit, TimeSpan window) => options.AddPolicy(policy,
                    context => CreatePartition(context, group, limit, window, config));
            });
            return services;
        }

        private static RateLimitPartition<string> CreatePartition(HttpContext context, string group, int limit, TimeSpan window, IConfiguration config)
        {
            var identity = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (group == "Login") identity = (context.Connection.RemoteIpAddress?.ToString() ?? "unknown") + ":" + context.Items["LoginEmail"];
            return RateLimitPartition.GetFixedWindowLimiter(group + ":" + identity, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, config.GetValue<int?>("RateLimits:" + group) ?? limit),
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            });
        }

        public static async Task ReadLoginPartitionAsync(HttpContext context, RequestDelegate next)
        {
            if (context.Request.Path == "/api/auth/login" && HttpMethods.IsPost(context.Request.Method))
            {
                if (context.Request.ContentLength is > 2048)
                    throw new ApiException(400, "invalid_request", "Yêu cầu đăng nhập không hợp lệ.");
                context.Request.EnableBuffering();
                try
                {
                    var buffer = new byte[2049];
                    var count = 0;
                    while (count < buffer.Length)
                    {
                        var read = await context.Request.Body.ReadAsync(buffer.AsMemory(count), context.RequestAborted);
                        if (read == 0) break;
                        count += read;
                    }
                    if (count > 2048) throw new ApiException(400, "invalid_request", "Yêu cầu đăng nhập không hợp lệ.");
                    using var body = System.Text.Json.JsonDocument.Parse(buffer.AsMemory(0, count));
                    if (body.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                        body.RootElement.TryGetProperty("email", out var email) && email.ValueKind == System.Text.Json.JsonValueKind.String)
                        context.Items["LoginEmail"] = email.GetString()?.Trim().ToLowerInvariant();
                }
                catch (System.Text.Json.JsonException) { /* DTO binding emits the validation response. */ }
                finally { context.Request.Body.Position = 0; }
            }
            await next(context);
        }
    }
}
