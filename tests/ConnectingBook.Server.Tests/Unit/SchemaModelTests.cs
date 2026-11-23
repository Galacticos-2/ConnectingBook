using Microsoft.EntityFrameworkCore;
using Project_Web_ConnectingBook.Server.Data;
using Project_Web_ConnectingBook.Server.Models;

namespace ConnectingBook.Server.Tests.Unit
{
    public class SchemaModelTests
    {
        [Fact]
        public void Model_has_all_nine_entities_and_uses_database_rowversion()
        {
            using var db = new ConnectingBookDbContext(new DbContextOptionsBuilder<ConnectingBookDbContext>()
                .UseSqlServer().Options);
            Assert.Equal(9, db.Model.GetEntityTypes().Count());
            var version = db.Model.FindEntityType(typeof(Post))!.FindProperty(nameof(Post.Version))!;
            Assert.True(version.IsConcurrencyToken);
            Assert.Equal("rowversion", version.GetColumnType());
        }

        [Fact]
        public void Schema_preserves_history_and_has_filtered_unique_pending_reports()
        {
            using var db = new ConnectingBookDbContext(new DbContextOptionsBuilder<ConnectingBookDbContext>()
                .UseSqlServer().Options);
            Assert.All(db.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()),
                fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
            var report = db.Model.FindEntityType(typeof(Report))!;
            Assert.Contains(report.GetIndexes(), index => index.IsUnique && index.GetFilter() == "[Status] = N'Pending'");
            var sql = db.Database.GenerateCreateScript();
            Assert.Contains("rowversion", sql);
            Assert.Contains("datetime2(7)", sql);
            Assert.DoesNotContain("xmin", sql);
        }
    }
}
