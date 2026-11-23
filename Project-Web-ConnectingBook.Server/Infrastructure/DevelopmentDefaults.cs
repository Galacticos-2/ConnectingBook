using Microsoft.Data.SqlClient;

namespace Project_Web_ConnectingBook.Server.Infrastructure
{
    public static class DevelopmentDefaults
    {
        public static string DatabaseConnection(IConfiguration configuration, IHostEnvironment environment)
        {
            var configured = configuration["ConnectionStrings:Default"];
            if (!string.IsNullOrWhiteSpace(configured)) return configured;
            if (!environment.IsDevelopment()) throw new InvalidOperationException("Thiếu ConnectionStrings__Default.");
            // These are the explicitly approved local database identifiers, not credentials.
            return new SqlConnectionStringBuilder
            {
                DataSource = @".\SQLEXPRESS",
                InitialCatalog = "ConnectingBookSystemDb",
                IntegratedSecurity = true,
                Encrypt = true,
                TrustServerCertificate = true
            }.ConnectionString;
        }
    }
}
