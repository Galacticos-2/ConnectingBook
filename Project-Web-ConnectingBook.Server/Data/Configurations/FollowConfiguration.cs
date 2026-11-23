using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project_Web_ConnectingBook.Server.Models;

namespace Project_Web_ConnectingBook.Server.Data.Configurations
{
    public class FollowConfiguration : IEntityTypeConfiguration<Follow>
    {
        public void Configure(EntityTypeBuilder<Follow> builder)
        {
            builder.ToTable("Follows", table => table.HasCheckConstraint("CK_Follows_NoSelf", "[FollowerId] <> [FollowedId]"));
            builder.HasKey(x => new { x.FollowerId, x.FollowedId });
            builder.HasIndex(x => x.FollowedId);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.FollowerId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.FollowedId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
