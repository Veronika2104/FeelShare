using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FeelShare.Migrations
{
    /// <inheritdoc />
    public partial class StoryReactionsAndComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoryComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoryId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryComments_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoryComments_PublicStory_StoryId",
                        column: x => x.StoryId,
                        principalTable: "PublicStory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryReactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StoryId = table.Column<int>(type: "int", nullable: false),
                    Reaction = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ReactKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryReactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryReactions_PublicStory_StoryId",
                        column: x => x.StoryId,
                        principalTable: "PublicStory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoryComments_StoryId_CreatedAtUtc",
                table: "StoryComments",
                columns: new[] { "StoryId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryComments_UserId",
                table: "StoryComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryReactions_StoryId_Reaction_ReactKey",
                table: "StoryReactions",
                columns: new[] { "StoryId", "Reaction", "ReactKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoryComments");

            migrationBuilder.DropTable(
                name: "StoryReactions");
        }
    }
}
