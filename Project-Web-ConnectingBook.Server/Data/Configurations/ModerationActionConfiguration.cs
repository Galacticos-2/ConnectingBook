using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project_Web_ConnectingBook.Server.Models;

namespace Project_Web_ConnectingBook.Server.Data.Configurations
{
    public class ModerationActionConfiguration : IEntityTypeConfiguration<ModerationAction>
    {
        public void Configure(EntityTypeBuilder<ModerationAction> builder)
        {
            builder.ToTable("ModerationActions", table =>
            {
                table.HasCheckConstraint("CK_Actions_Decision", "[Decision] IN (N'Keep', N'Hide', N'Restore', N'AuthorDeleted')");
                table.HasCheckConstraint("CK_Actions_Reason", "LEN(LTRIM(RTRIM([Reason]))) BETWEEN 1 AND 1000");
                table.HasCheckConstraint("CK_Actions_Actor", "[ActorId] IS NOT NULL OR [Decision] = N'AuthorDeleted'");
                table.HasCheckConstraint("CK_Actions_Status", "[PreviousStatus] IN (N'Processing', N'Failed', N'Visible', N'Hidden', N'Deleted') AND [NextStatus] IN (N'Processing', N'Failed', N'Visible', N'Hidden', N'Deleted')");
            });
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Decision).HasMaxLength(32).IsRequired();
            builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            builder.Property(x => x.PreviousStatus).HasMaxLength(16).IsRequired();
            builder.Property(x => x.NextStatus).HasMaxLength(16).IsRequired();
            builder.HasIndex(x => new { x.PostId, x.CreatedAt, x.Id });
            builder.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
