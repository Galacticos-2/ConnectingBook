using System.ComponentModel.DataAnnotations;

namespace Project_Web_ConnectingBook.Server.Contracts
{
    public record CreatePostRequest([StringLength(5000)] string? Text, Guid[]? ImageIds);
    public record UpdatePostRequest([StringLength(5000)] string? Text, Guid[]? ImageIds, [StringLength(500)] string? Caption);
    public record ProfileRequest([StringLength(50, MinimumLength = 2)] string? DisplayName, [StringLength(160)] string? Bio, Guid? AvatarMediaId);
    public record AuthorDto(Guid Id, string DisplayName, Guid? AvatarMediaId);
    public record MediaDto(Guid Id, Guid? ThumbnailId, string Kind, int Width, int Height);
    public record PostDto(string Id, AuthorDto Author, string Type, string Text, IReadOnlyList<MediaDto> Media,
        DateTime? PublishedAt, DateTime CreatedAt, DateTime? UpdatedAt, int LikeCount, int CommentCount,
        bool IsLikedByMe, string Version, string? Status, string? ModerationReason, string? FailureCode);
}
