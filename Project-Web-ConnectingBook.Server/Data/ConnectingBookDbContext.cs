using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Project_Web_ConnectingBook.Server.Models;

namespace Project_Web_ConnectingBook.Server.Data
{
    public class ConnectingBookDbContext(DbContextOptions<ConnectingBookDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<Follow> Follows => Set<Follow>();
        public DbSet<Post> Posts => Set<Post>();
        public DbSet<Media> Media => Set<Media>();
        public DbSet<Reaction> Reactions => Set<Reaction>();
        public DbSet<Comment> Comments => Set<Comment>();
        public DbSet<Report> Reports => Set<Report>();
        public DbSet<ModerationAction> ModerationActions => Set<ModerationAction>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<DateTime>()
                .HaveConversion<UtcDateTimeConverter>().HaveColumnType("datetime2(7)");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConnectingBookDbContext).Assembly);
        }
    }

    public class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => ValidateUtc(value),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
        private static DateTime ValidateUtc(DateTime value)
        {
            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("Thời gian phải ở múi giờ UTC.");
            }
            return value;
        }
    }
}
