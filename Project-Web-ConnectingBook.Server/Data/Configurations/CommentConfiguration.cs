using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Project_Web_ConnectingBook.Server.Models;

namespace Project_Web_ConnectingBook.Server.Data.Configurations
{
    public class CommentConfiguration : IEntityTypeConfiguration<Comment>
    {
        public void Configure(EntityTypeBuilder<Comment> builder)
        {
            builder.ToTable("Comments", table => table.HasCheckConstraint("CK_Comments_Text", "LEN(LTRIM(RTRIM([Text]))) BETWEEN 1 AND 1000"));
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Text).HasMaxLength(1000).IsRequired();
            builder.HasIndex(x => new { x.PostId, x.CreatedAt, x.Id });
            builder.HasOne<Post>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
