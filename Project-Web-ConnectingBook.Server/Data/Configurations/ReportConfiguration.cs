using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project_Web_ConnectingBook.Server.Models;

namespace Project_Web_ConnectingBook.Server.Data.Configurations
{
    public class ReportConfiguration : IEntityTypeConfiguration<Report>
    {
        public void Configure(EntityTypeBuilder<Report> builder)
        {
            builder.ToTable("Reports", table =>
            {
                table.HasCheckConstraint("CK_Reports_Reason", "[Reason] IN (N'Spam', N'Harassment', N'Inappropriate', N'Other')");
                table.HasCheckConstraint("CK_Reports_Description", "[Reason] <> N'Other' OR ([Description] IS NOT NULL AND LEN(LTRIM(RTRIM([Description]))) > 0)");
                table.HasCheckConstraint("CK_Reports_Status", "[Status] IN (N'Pending', N'ClosedKept', N'ClosedHidden', N'ClosedAuthorDeleted')");
                table.HasCheckConstraint("CK_Reports_Closure", "[Status] = N'Pending' OR ([ClosedAt] IS NOT NULL AND [ModerationActionId] IS NOT NULL)");
                table.HasCheckConstraint("CK_Reports_SnapshotJson", "ISJSON([SnapshotMediaIds]) = 1");
            });
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Reason).HasMaxLength(32).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
            builder.Property(x => x.SnapshotText).HasMaxLength(5000).IsRequired();
            builder.Property(x => x.SnapshotMediaIds)
                .HasConversion(
                    value => System.Text.Json.JsonSerializer.Serialize(value, (System.Text.Json.JsonSerializerOptions?)null),
                    value => System.Text.Json.JsonSerializer.Deserialize<Guid[]>(value, (System.Text.Json.JsonSerializerOptions?)null)!)
                .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<Guid[]>(
                    (left, right) => left!.SequenceEqual(right!),
                    value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
                    value => value.ToArray()));
            builder.HasIndex(x => new { x.PostId, x.ReporterId }).IsUnique().HasFilter("[Status] = N'Pending'");
            builder.HasIndex(x => new { x.PostId, x.CreatedAt }).HasFilter("[Status] = N'Pending'");
            builder.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<ModerationAction>().WithMany().HasForeignKey(x => x.ModerationActionId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
