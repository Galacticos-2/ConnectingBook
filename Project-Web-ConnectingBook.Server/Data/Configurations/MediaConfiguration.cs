using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project_Web_ConnectingBook.Server.Models;

namespace Project_Web_ConnectingBook.Server.Data.Configurations
{
    public class MediaConfiguration : IEntityTypeConfiguration<Media>
    {
        public void Configure(EntityTypeBuilder<Media> builder)
        {
            builder.ToTable("Media", table =>
            {
                table.HasCheckConstraint("CK_Media_Kind", "[Kind] IN (N'Image', N'Video', N'Thumbnail', N'Avatar')");
                table.HasCheckConstraint("CK_Media_Dimensions", "[Width] > 0 AND [Height] > 0 AND [SizeBytes] > 0");
                table.HasCheckConstraint("CK_Media_ImageDimensions", "[Kind] NOT IN (N'Image', N'Avatar') OR ([Width] <= 4096 AND [Height] <= 4096)");
                table.HasCheckConstraint("CK_Media_Duration", "[Kind] <> N'Video' OR ([DurationSeconds] IS NOT NULL AND [DurationSeconds] > 0 AND [DurationSeconds] <= 60)");
                table.HasCheckConstraint("CK_Media_Position", "[Position] IS NULL OR [Position] BETWEEN 0 AND 3");
            });
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Kind).HasMaxLength(16).IsRequired();
            builder.Property(x => x.StorageKey).HasMaxLength(1024).IsUnicode(false).IsRequired();
            builder.HasIndex(x => x.StorageKey).IsUnique();
            builder.Property(x => x.ContentType).HasMaxLength(100).IsUnicode(false).IsRequired();
            builder.HasIndex(x => new { x.PostId, x.Position }).IsUnique().HasFilter("[PostId] IS NOT NULL AND [Kind] = N'Image'");
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<Media>().WithMany().HasForeignKey(x => x.ParentMediaId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
