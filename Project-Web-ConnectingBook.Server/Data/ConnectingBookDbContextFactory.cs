using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Project_Web_ConnectingBook.Server.Data
{
    public class ConnectingBookDbContextFactory : IDesignTimeDbContextFactory<ConnectingBookDbContext>
    {
        public ConnectingBookDbContext CreateDbContext(string[] args)
        {
            var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                ?? throw new InvalidOperationException("Thiếu biến môi trường ConnectionStrings__Default.");
            return new ConnectingBookDbContext(new DbContextOptionsBuilder<ConnectingBookDbContext>()
                .UseSqlServer(connection, sql => sql.UseCompatibilityLevel(130)).Options);
        }
    }
}
