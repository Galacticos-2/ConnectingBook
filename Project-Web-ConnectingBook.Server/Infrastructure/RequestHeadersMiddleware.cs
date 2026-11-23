namespace Project_Web_ConnectingBook.Server.Infrastructure
{
    public class RequestHeadersMiddleware(RequestDelegate next)
    {
        public Task InvokeAsync(HttpContext context)
        {
            context.TraceIdentifier = Guid.NewGuid().ToString("N");
            context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            return next(context);
        }
    }
}
