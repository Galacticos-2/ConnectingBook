using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Project_Web_ConnectingBook.Server.Infrastructure
{
    public class ApiException(int status, string code, string message, int? retryAfter = null) : Exception(message)
    {
        public int Status { get; } = status;
        public string Code { get; } = code;
        public int? RetryAfter { get; } = retryAfter;
    }

    public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
        {
            if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested) return false;
            var api = exception as ApiException;
            var status = api?.Status ?? (exception switch
            {
                DbUpdateConcurrencyException => 409,
                SqlException { Number: 1205 } => 409,
                DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => 409,
                BadHttpRequestException bad => bad.StatusCode,
                _ => 500
            });
            var code = api?.Code ?? (status == 409 ? "write_conflict" : status == 500 ? "server_error" : "invalid_request");
            if (status == 500)
                logger.LogError("Request failed: {ExceptionType}, correlation {CorrelationId}", exception.GetType().Name, context.TraceIdentifier);
            context.Response.StatusCode = status;
            if (api?.RetryAfter is int retry) context.Response.Headers.RetryAfter = retry.ToString();
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = status == 500 ? "Lỗi hệ thống." : "Không thể thực hiện yêu cầu.",
                Detail = api?.Message ?? (status == 409 ? "Dữ liệu đã thay đổi hoặc bị trùng. Vui lòng tải lại." : "Yêu cầu không thể được xử lý."),
                Instance = context.Request.Path,
                Extensions = { ["code"] = code, ["correlationId"] = context.TraceIdentifier }
            }, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
            return true;
        }
    }
}
