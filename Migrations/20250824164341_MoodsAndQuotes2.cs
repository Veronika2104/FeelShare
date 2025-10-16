using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FeelShare.Migrations
{
    /// <inheritdoc />
    public partial class MoodsAndQuotes2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Emotion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Emotion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JournalEntry",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EmotionId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalEntry_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JournalEntry_Emotion_EmotionId",
                        column: x => x.EmotionId,
                        principalTable: "Emotion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Quote",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmotionId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Author = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quote_Emotion_EmotionId",
                        column: x => x.EmotionId,
                        principalTable: "Emotion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Emotion",
                columns: new[] { "Id", "Icon", "Name", "Slug" },
                values: new object[,]
                {
                    { 1, "😢", "Мне грустно", "sad" },
                    { 2, "😊", "Радостно", "happy" },
                    { 3, "😟", "Тревожно", "anxious" },
                    { 4, "😠", "Злюсь", "angry" },
                    { 5, "🥺", "Одиноко", "lonely" },
                    { 6, "🙏", "Благодарен", "grateful" },
                    { 7, "🪫", "Нет вдохновения", "stuck" },
                    { 8, "🗣️", "Хочу высказаться", "speak" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Emotion_Slug",
                table: "Emotion",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntry_EmotionId",
                table: "JournalEntry",
                column: "EmotionId");

            migrationBuilder.CreateIndex(
                name: "IX_JournalEntry_UserId",
                table: "JournalEntry",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Quote_EmotionId",
                table: "Quote",
                column: "EmotionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalEntry");

            migrationBuilder.DropTable(
                name: "Quote");

            migrationBuilder.DropTable(
                name: "Emotion");
        }
    }
}
