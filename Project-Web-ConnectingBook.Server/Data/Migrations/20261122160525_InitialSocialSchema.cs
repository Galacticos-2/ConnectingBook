using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project_Web_ConnectingBook.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSocialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<long>(type: "bigint", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.Id);
                    table.CheckConstraint("CK_Comments_Text", "LEN(LTRIM(RTRIM([Text]))) BETWEEN 1 AND 1000");
                });

            migrationBuilder.CreateTable(
                name: "Follows",
                columns: table => new
                {
                    FollowerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FollowedId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Follows", x => new { x.FollowerId, x.FollowedId });
                    table.CheckConstraint("CK_Follows_NoSelf", "[FollowerId] <> [FollowedId]");
                });

            migrationBuilder.CreateTable(
                name: "Media",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PostId = table.Column<long>(type: "bigint", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    StorageKey = table.Column<string>(type: "varchar(1024)", unicode: false, maxLength: 1024, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    DurationSeconds = table.Column<double>(type: "float", nullable: true),
                    Position = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ParentMediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Media", x => x.Id);
                    table.CheckConstraint("CK_Media_Dimensions", "[Width] > 0 AND [Height] > 0 AND [SizeBytes] > 0");
                    table.CheckConstraint("CK_Media_Duration", "[Kind] <> N'Video' OR ([DurationSeconds] IS NOT NULL AND [DurationSeconds] > 0 AND [DurationSeconds] <= 60)");
                    table.CheckConstraint("CK_Media_ImageDimensions", "[Kind] NOT IN (N'Image', N'Avatar') OR ([Width] <= 4096 AND [Height] <= 4096)");
                    table.CheckConstraint("CK_Media_Kind", "[Kind] IN (N'Image', N'Video', N'Thumbnail', N'Avatar')");
                    table.CheckConstraint("CK_Media_Position", "[Position] IS NULL OR [Position] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_Media_Media_ParentMediaId",
                        column: x => x.ParentMediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false, collation: "Latin1_General_100_CI_AS"),
                    PasswordHash = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Bio = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    AvatarMediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Role = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    LockoutEnd = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.CheckConstraint("CK_Users_DisplayName", "LEN([DisplayName]) BETWEEN 2 AND 50");
                    table.CheckConstraint("CK_Users_FailedLoginCount", "[FailedLoginCount] >= 0");
                    table.CheckConstraint("CK_Users_Role", "[Role] IN (N'Member', N'Admin')");
                    table.CheckConstraint("CK_Users_Status", "[Status] IN (N'Active', N'Disabled')");
                    table.ForeignKey(
                        name: "FK_Users_Media_AvatarMediaId",
                        column: x => x.AvatarMediaId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Posts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ModerationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ProcessingAttempts = table.Column<int>(type: "int", nullable: false),
                    ProcessingStartedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Posts", x => x.Id);
                    table.CheckConstraint("CK_Posts_Attempts", "[ProcessingAttempts] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_Posts_Deleted", "[Status] <> N'Deleted' OR [DeletedAt] IS NOT NULL");
                    table.CheckConstraint("CK_Posts_Published", "[Status] NOT IN (N'Visible', N'Hidden') OR [PublishedAt] IS NOT NULL");
                    table.CheckConstraint("CK_Posts_ReelCaption", "[Type] <> N'Reel' OR LEN([Text]) <= 500");
                    table.CheckConstraint("CK_Posts_Status", "[Status] IN (N'Processing', N'Failed', N'Visible', N'Hidden', N'Deleted')");
                    table.CheckConstraint("CK_Posts_Type", "[Type] IN (N'Post', N'Reel')");
                    table.ForeignKey(
                        name: "FK_Posts_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_RefreshTokens_ReplacedByTokenId",
                        column: x => x.ReplacedByTokenId,
                        principalTable: "RefreshTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModerationActions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<long>(type: "bigint", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Decision = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PreviousStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    NextStatus = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationActions", x => x.Id);
                    table.CheckConstraint("CK_Actions_Actor", "[ActorId] IS NOT NULL OR [Decision] = N'AuthorDeleted'");
                    table.CheckConstraint("CK_Actions_Decision", "[Decision] IN (N'Keep', N'Hide', N'Restore', N'AuthorDeleted')");
                    table.CheckConstraint("CK_Actions_Reason", "LEN(LTRIM(RTRIM([Reason]))) BETWEEN 1 AND 1000");
                    table.CheckConstraint("CK_Actions_Status", "[PreviousStatus] IN (N'Processing', N'Failed', N'Visible', N'Hidden', N'Deleted') AND [NextStatus] IN (N'Processing', N'Failed', N'Visible', N'Hidden', N'Deleted')");
                    table.ForeignKey(
                        name: "FK_ModerationActions_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModerationActions_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reactions",
                columns: table => new
                {
                    PostId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reactions", x => new { x.PostId, x.UserId });
                    table.ForeignKey(
                        name: "FK_Reactions_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reactions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PostId = table.Column<long>(type: "bigint", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SnapshotText = table.Column<string>(type: "nvarchar(max)", maxLength: 5000, nullable: false),
                    SnapshotMediaIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2(7)", nullable: true),
                    ModerationActionId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                    table.CheckConstraint("CK_Reports_Closure", "[Status] = N'Pending' OR ([ClosedAt] IS NOT NULL AND [ModerationActionId] IS NOT NULL)");
                    table.CheckConstraint("CK_Reports_Description", "[Reason] <> N'Other' OR ([Description] IS NOT NULL AND LEN(LTRIM(RTRIM([Description]))) > 0)");
                    table.CheckConstraint("CK_Reports_Reason", "[Reason] IN (N'Spam', N'Harassment', N'Inappropriate', N'Other')");
                    table.CheckConstraint("CK_Reports_SnapshotJson", "ISJSON([SnapshotMediaIds]) = 1");
                    table.CheckConstraint("CK_Reports_Status", "[Status] IN (N'Pending', N'ClosedKept', N'ClosedHidden', N'ClosedAuthorDeleted')");
                    table.ForeignKey(
                        name: "FK_Reports_ModerationActions_ModerationActionId",
                        column: x => x.ModerationActionId,
                        principalTable: "ModerationActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reports_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reports_Users_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_AuthorId",
                table: "Comments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_PostId_CreatedAt_Id",
                table: "Comments",
                columns: new[] { "PostId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Follows_FollowedId",
                table: "Follows",
                column: "FollowedId");

            migrationBuilder.CreateIndex(
                name: "IX_Media_OwnerId",
                table: "Media",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Media_ParentMediaId",
                table: "Media",
                column: "ParentMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Media_PostId_Position",
                table: "Media",
                columns: new[] { "PostId", "Position" },
                unique: true,
                filter: "[PostId] IS NOT NULL AND [Kind] = N'Image'");

            migrationBuilder.CreateIndex(
                name: "IX_Media_StorageKey",
                table: "Media",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModerationActions_ActorId",
                table: "ModerationActions",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationActions_PostId_CreatedAt_Id",
                table: "ModerationActions",
                columns: new[] { "PostId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_AuthorId_CreatedAt_Id",
                table: "Posts",
                columns: new[] { "AuthorId", "CreatedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_AuthorId_PublishedAt_Id",
                table: "Posts",
                columns: new[] { "AuthorId", "PublishedAt", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_PublishedAt_Id",
                table: "Posts",
                columns: new[] { "PublishedAt", "Id" },
                descending: new bool[0],
                filter: "[Status] = N'Visible'");

            migrationBuilder.CreateIndex(
                name: "IX_Reactions_UserId",
                table: "Reactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FamilyId",
                table: "RefreshTokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ReplacedByTokenId",
                table: "RefreshTokens",
                column: "ReplacedByTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ModerationActionId",
                table: "Reports",
                column: "ModerationActionId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_PostId_CreatedAt",
                table: "Reports",
                columns: new[] { "PostId", "CreatedAt" },
                filter: "[Status] = N'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_PostId_ReporterId",
                table: "Reports",
                columns: new[] { "PostId", "ReporterId" },
                unique: true,
                filter: "[Status] = N'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReporterId",
                table: "Reports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_AvatarMediaId",
                table: "Users",
                column: "AvatarMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Posts_PostId",
                table: "Comments",
                column: "PostId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Users_AuthorId",
                table: "Comments",
                column: "AuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Follows_Users_FollowedId",
                table: "Follows",
                column: "FollowedId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Follows_Users_FollowerId",
                table: "Follows",
                column: "FollowerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Media_Posts_PostId",
                table: "Media",
                column: "PostId",
                principalTable: "Posts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Media_Users_OwnerId",
                table: "Media",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Media_Posts_PostId",
                table: "Media");

            migrationBuilder.DropForeignKey(
                name: "FK_Media_Users_OwnerId",
                table: "Media");

            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropTable(
                name: "Follows");

            migrationBuilder.DropTable(
                name: "Reactions");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropTable(
                name: "ModerationActions");

            migrationBuilder.DropTable(
                name: "Posts");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Media");
        }
    }
}
