using Microsoft.OpenApi.Models;

namespace Project_Web_ConnectingBook.Server.Infrastructure
{
    public static class ApiDocumentation
    {
        public static IServiceCollection AddConnectingBookOpenApi(this IServiceCollection services)
        {
            services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer((document, _, _) =>
                {
                    document.Info.Title = "Connectbook API";
                    document.Info.Version = "v1";
                    document.Info.Description = "API mạng xã hội Connectbook. Thời gian UTC; ID bigint truyền dưới dạng chuỗi. Refresh/logout dùng cookie HttpOnly.";
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "JWT truy cập, có hiệu lực 15 phút."
                    };
                    document.SecurityRequirements.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
                    });
                    return Task.CompletedTask;
                });
                options.AddOperationTransformer((operation, context, _) =>
                {
                    var responses = new Dictionary<string, string>
                    {
                        ["400"] = "Dữ liệu/cursor không hợp lệ; ProblemDetails có code và errors theo trường.",
                        ["401"] = "Chưa đăng nhập hoặc phiên hết hạn.",
                        ["403"] = "Không đủ quyền hoặc không sở hữu nội dung.",
                        ["404"] = "Nội dung không tồn tại hoặc không được phép xem.",
                        ["409"] = "Dữ liệu đã thay đổi, báo cáo trùng hoặc nhóm đã xử lý.",
                        ["429"] = "Vượt giới hạn hoặc khóa tạm thời; có Retry-After."
                    };
                    foreach (var item in responses) operation.Responses.TryAdd(item.Key, new OpenApiResponse { Description = item.Value });
                    if (context.Description.RelativePath?.Contains("media/images", StringComparison.Ordinal) == true ||
                        (context.Description.RelativePath == "api/reels" && context.Description.HttpMethod == "POST"))
                    {
                        operation.Responses.TryAdd("413", new OpenApiResponse { Description = "Tệp vượt dung lượng cho phép." });
                        operation.Responses.TryAdd("415", new OpenApiResponse { Description = "Định dạng tệp thực tế không hợp lệ." });
                    }
                    return Task.CompletedTask;
                });
            });
            return services;
        }
    }
}
