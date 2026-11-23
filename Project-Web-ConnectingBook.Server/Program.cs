
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Project_Web_ConnectingBook.Server.Data;
using Project_Web_ConnectingBook.Server.Infrastructure;
using Project_Web_ConnectingBook.Server.Models;
using Project_Web_ConnectingBook.Server.Services;
using Serilog;
using Project_Web_ConnectingBook.Server.Storage;
using Project_Web_ConnectingBook.Server.Workers;

namespace Project_Web_ConnectingBook.Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            if (args.Contains("--account-sql"))
            {
                AccountSqlGenerator.Run();
                return;
            }
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((context, configuration) => configuration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
                .WriteTo.Console());

            builder.Services.AddDbContext<ConnectingBookDbContext>((services, options) =>
                options.UseSqlServer(DevelopmentDefaults.DatabaseConnection(services.GetRequiredService<IConfiguration>(),
                    services.GetRequiredService<IHostEnvironment>()), sql => sql.UseCompatibilityLevel(130)));
            builder.Services.AddExceptionHandler<ApiExceptionHandler>();
            builder.Services.AddProblemDetails();
            builder.Services.AddSingleton<JwtSettings>();
            builder.Services.AddScoped<AuthService>();
            builder.Services.AddScoped<ProfileService>();
            builder.Services.AddScoped<FollowService>();
            builder.Services.AddScoped<PostService>();
            builder.Services.AddScoped<ImageService>();
            builder.Services.AddScoped<FeedService>();
            builder.Services.AddScoped<InteractionService>();
            builder.Services.AddScoped<ModerationService>();
            builder.Services.AddScoped<ReelService>();
            builder.Services.AddSingleton<VideoTools>();
            builder.Services.AddSingleton<ReelQueue>();
            builder.Services.AddSingleton<ReelWorker>();
            builder.Services.AddScoped<MediaCleanupService>();
            builder.Services.AddScoped<DemoSeedService>();
            if (builder.Configuration.GetValue("Workers:Enabled", true))
            {
                builder.Services.AddHostedService(services => services.GetRequiredService<ReelWorker>());
                builder.Services.AddHostedService<MediaCleanupWorker>();
            }
            builder.Services.AddSingleton<PasswordHasher<User>>();
            builder.Services.AddSingleton<IStorage>(services =>
                services.GetRequiredService<IConfiguration>()["Storage:Provider"] == "Minio"
                    ? new MinioStorage(services.GetRequiredService<IConfiguration>())
                    : new LocalStorage(services.GetRequiredService<IConfiguration>(), services.GetRequiredService<IWebHostEnvironment>()));
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
            builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<JwtSettings>((options, settings) =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = settings.Issuer,
                        ValidateAudience = true,
                        ValidAudience = settings.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = settings.Key,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnChallenge = async context =>
                        {
                            context.HandleResponse();
                            context.Response.StatusCode = 401;
                            await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = 401, Title = "Vui lòng đăng nhập.", Extensions = { ["code"] = "authentication_required" } }, options: null, contentType: "application/problem+json", cancellationToken: context.Request.HttpContext.RequestAborted);
                        },
                        OnForbidden = async context =>
                        {
                            context.Response.StatusCode = 403;
                            await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = 403, Title = "Bạn không có quyền thực hiện thao tác này.", Extensions = { ["code"] = "forbidden" } }, options: null, contentType: "application/problem+json", cancellationToken: context.Request.HttpContext.RequestAborted);
                        }
                    };
                });
            builder.Services.AddAuthorization();
            builder.Services.AddConnectingBookRateLimits(builder.Configuration);
            builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
            {
                var origin = builder.Configuration["Client:Origin"];
                if (!string.IsNullOrWhiteSpace(origin)) policy.WithOrigins(origin).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            }));

            // Add services to the container.

            builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
                options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new ValidationProblemDetails(
                    context.ModelState.Where(entry => entry.Value?.Errors.Count > 0).ToDictionary(entry => entry.Key, _ => new[] { "Giá trị không hợp lệ. Vui lòng kiểm tra định dạng và độ dài." }))
                {
                    Status = 400,
                    Title = "Dữ liệu không hợp lệ.",
                    Extensions = { ["code"] = "validation_failed" }
                })
                { ContentTypes = { "application/problem+json" } });
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddConnectingBookOpenApi();

            var app = builder.Build();

            if (args.Contains("--seed-demo") || args.Contains("--seed-performance"))
            {
                using var seedScope = app.Services.CreateScope();
                await seedScope.ServiceProvider.GetRequiredService<DemoSeedService>().RunAsync(args.Contains("--seed-performance"), CancellationToken.None);
                Console.WriteLine("Dedicated demo/test data is ready. No credentials are printed.");
                return;
            }

            if (args.Contains("--migrate"))
            {
                var connection = builder.Configuration["ConnectionStrings:Default"];
                if (string.IsNullOrWhiteSpace(connection))
                {
                    throw new InvalidOperationException("Thiếu biến môi trường ConnectionStrings__Default.");
                }
                var databaseName = new SqlConnectionStringBuilder(connection).InitialCatalog;
                if (databaseName != "ConnectingBookSystemDb" &&
                    !databaseName.StartsWith("ConnectingBook_Test_", StringComparison.Ordinal) &&
                    !databaseName.StartsWith("ConnectingBook_Demo_", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Database chưa được cho phép áp dụng migration.");
                }
                using var scope = app.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ConnectingBookDbContext>().Database.MigrateAsync();
                return;
            }

            _ = app.Services.GetRequiredService<JwtSettings>();
            await AdminBootstrap.RunAsync(app.Services);
            app.UseMiddleware<RequestHeadersMiddleware>();
            app.UseExceptionHandler();

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.MapStaticAssets();

            app.UseHttpsRedirection();

            app.UseCors();
            app.UseAuthentication();
            app.Use(RateLimitPolicies.ReadLoginPartitionAsync);
            app.UseRateLimiter();
            app.UseAuthorization();

            app.Use(async (context, next) =>
            {
                if (!app.Environment.IsDevelopment() && context.Request.Path.StartsWithSegments("/swagger") && context.User.Identity?.IsAuthenticated != true)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = 401, Title = "Vui lòng đăng nhập.", Extensions = { ["code"] = "authentication_required" } }, options: null, contentType: "application/problem+json");
                    return;
                }
                await next();
            });
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", "Connectbook v1");
                options.DocumentTitle = "Connectbook API";
            });
            var openApi = app.MapOpenApi();
            if (app.Environment.IsDevelopment()) openApi.AllowAnonymous();
            else openApi.RequireAuthorization();


            app.MapControllers();
            app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).AllowAnonymous();
            app.MapGet("/health/ready", async (ConnectingBookDbContext db, IStorage storage, CancellationToken ct) =>
            {
                try
                {
                    var ready = await db.Database.CanConnectAsync(ct) && await storage.IsReadyAsync(ct);
                    return Results.Json(new { status = ready ? "ready" : "unavailable" }, statusCode: ready ? 200 : 503);
                }
                catch { return Results.Json(new { status = "unavailable" }, statusCode: 503); }
            }).AllowAnonymous();

            app.MapFallbackToFile("/index.html");

            app.Run();
        }
    }
}
