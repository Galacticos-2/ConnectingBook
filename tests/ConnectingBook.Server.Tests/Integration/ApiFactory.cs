using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ConnectingBook.Server.Tests.Integration
{
    public class ApiFactory(string connection, Dictionary<string, string?>? overrides = null, Action<IServiceCollection>? configureServices = null, string environment = "Testing")
        : WebApplicationFactory<Project_Web_ConnectingBook.Server.Program>
    {
        private readonly string storageRoot = Path.Combine(Path.GetTempPath(), "ConnectingBook_Test_" + Guid.NewGuid().ToString("N"));
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                configureServices?.Invoke(services);
            });
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var values = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = connection,
                    ["Jwt:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
                    ["RateLimits:Login"] = "100",
                    ["RateLimits:Register"] = "100",
                    ["Storage:Provider"] = "Local",
                    ["Storage:LocalPath"] = storageRoot,
                    ["Client:Origin"] = "https://localhost"
                };
                if (overrides is not null)
                    foreach (var item in overrides) values[item.Key] = item.Value;
                config.AddInMemoryCollection(values);
            });
        }

        public HttpClient CreateHttpsClient(bool cookies = true) => CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = cookies,
            AllowAutoRedirect = false
        });

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            var target = Path.GetFullPath(storageRoot);
            var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (Path.GetDirectoryName(target) != expectedParent || !Path.GetFileName(target).StartsWith("ConnectingBook_Test_", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to clean an unowned storage directory.");
            if (Directory.Exists(target))
            {
                foreach (var file in Directory.EnumerateFiles(target)) File.Delete(file);
                Directory.Delete(target);
            }
        }
    }
}
