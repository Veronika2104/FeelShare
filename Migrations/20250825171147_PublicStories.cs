using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeelShare.Migrations
{
    /// <inheritdoc />
    public partial class PublicStories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PublicStory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmotionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicStory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PublicStory_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PublicStory_Emotion_EmotionId",
                        column: x => x.EmotionId,
                        principalTable: "Emotion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StoryLike",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoryId = table.Column<int>(type: "int", nullable: false),
                    LikeKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryLike", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryLike_PublicStory_StoryId",
                        column: x => x.StoryId,
                        principalTable: "PublicStory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PublicStory_EmotionId",
                table: "PublicStory",
                column: "EmotionId");

            migrationBuilder.CreateIndex(
                name: "IX_PublicStory_UserId",
                table: "PublicStory",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryLike_StoryId_LikeKey",
                table: "StoryLike",
                columns: new[] { "StoryId", "LikeKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoryLike");

            migrationBuilder.DropTable(
                name: "PublicStory");
        }
    }
}
