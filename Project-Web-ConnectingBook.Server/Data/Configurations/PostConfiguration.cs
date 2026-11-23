using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project_Web_ConnectingBook.Server.Models;

namespace Project_Web_ConnectingBook.Server.Data.Configurations
{
    public class PostConfiguration : IEntityTypeConfiguration<Post>
    {
        public void Configure(EntityTypeBuilder<Post> builder)
        {
            builder.ToTable("Posts", table =>
            {
                table.HasCheckConstraint("CK_Posts_Type", "[Type] IN (N'Post', N'Reel')");
                table.HasCheckConstraint("CK_Posts_Status", "[Status] IN (N'Processing', N'Failed', N'Visible', N'Hidden', N'Deleted')");
                table.HasCheckConstraint("CK_Posts_Published", "[Status] NOT IN (N'Visible', N'Hidden') OR [PublishedAt] IS NOT NULL");
                table.HasCheckConstraint("CK_Posts_Deleted", "[Status] <> N'Deleted' OR [DeletedAt] IS NOT NULL");
                table.HasCheckConstraint("CK_Posts_Attempts", "[ProcessingAttempts] BETWEEN 0 AND 2");
                table.HasCheckConstraint("CK_Posts_ReelCaption", "[Type] <> N'Reel' OR LEN([Text]) <= 500");
            });
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Type).HasMaxLength(16).IsRequired();
            builder.Property(x => x.Text).HasMaxLength(5000).IsRequired();
            builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
            builder.Property(x => x.ModerationReason).HasMaxLength(1000);
            builder.Property(x => x.FailureCode).HasMaxLength(100);
            builder.Property(x => x.Version).IsRowVersion();
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.PublishedAt, x.Id }).IsDescending().HasFilter("[Status] = N'Visible'");
            builder.HasIndex(x => new { x.AuthorId, x.PublishedAt, x.Id }).IsDescending(false, true, true);
            builder.HasIndex(x => new { x.AuthorId, x.CreatedAt, x.Id }).IsDescending(false, true, true);
        }
    }
}
