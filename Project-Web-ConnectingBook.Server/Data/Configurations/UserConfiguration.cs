using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project_Web_ConnectingBook.Server.Models;

namespace Project_Web_ConnectingBook.Server.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("Users", table =>
            {
                table.HasCheckConstraint("CK_Users_Role", "[Role] IN (N'Member', N'Admin')");
                table.HasCheckConstraint("CK_Users_Status", "[Status] IN (N'Active', N'Disabled')");
                table.HasCheckConstraint("CK_Users_DisplayName", "LEN([DisplayName]) BETWEEN 2 AND 50");
                table.HasCheckConstraint("CK_Users_FailedLoginCount", "[FailedLoginCount] >= 0");
            });
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Email).HasMaxLength(254).UseCollation("Latin1_General_100_CI_AS").IsRequired();
            builder.HasIndex(x => x.Email).IsUnique();
            builder.Property(x => x.PasswordHash).HasMaxLength(1024).IsRequired();
            builder.Property(x => x.DisplayName).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Bio).HasMaxLength(160).IsRequired();
            builder.Property(x => x.Role).HasMaxLength(16).IsRequired();
            builder.Property(x => x.Status).HasMaxLength(16).IsRequired();
            builder.HasOne<Media>().WithMany().HasForeignKey(x => x.AvatarMediaId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
