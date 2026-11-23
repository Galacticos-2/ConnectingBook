using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Data;

namespace ConnectingBook.Server.Tests.Integration
{
    public class SqlServerFixture : IAsyncLifetime
    {
        private readonly string databaseName = "ConnectingBook_Test_" + Guid.NewGuid().ToString("N");
        public string ConnectionString { get; private set; } = "";

        public ConnectingBookDbContext CreateContext() => new(
            new DbContextOptionsBuilder<ConnectingBookDbContext>()
                .UseSqlServer(ConnectionString, sql => sql.UseCompatibilityLevel(130)).Options);

        public async Task InitializeAsync()
        {
            var source = Environment.GetEnvironmentVariable("ConnectionStrings__Test")
                ?? throw new InvalidOperationException("Set ConnectionStrings__Test to run SQL Server integration tests.");
            var settings = new SqlConnectionStringBuilder(source) { InitialCatalog = databaseName };
            ConnectionString = settings.ConnectionString;
            await using var db = CreateContext();
            await db.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString)) return;
            // This instance owns only its generated test database. Never reset an application database.
            var configuredName = new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;
            if (configuredName != databaseName || !configuredName.StartsWith("ConnectingBook_Test_", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to remove a database not owned by this test fixture.");
            await using var db = CreateContext();
            await db.Database.EnsureDeletedAsync();
        }
    }

    public class SqlServerTheoryAttribute : TheoryAttribute
    {
        public SqlServerTheoryAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__Test")))
                Skip = "SQL Server integration requires ConnectionStrings__Test; no substitute database is used.";
        }
    }

    public class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__Test")))
                Skip = "SQL Server integration requires ConnectionStrings__Test; no substitute database is used.";
        }
    }
}
