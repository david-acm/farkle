using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Farkle.Infrastructure.Migrations.ReadModel
{
    /// <inheritdoc />
    public partial class FeedbackViewReadModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedbackViews",
                columns: table => new
                {
                    Position = table.Column<long>(type: "bigint", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    GameId = table.Column<int>(type: "integer", nullable: true),
                    PlayerId = table.Column<int>(type: "integer", nullable: true),
                    Stage = table.Column<string>(type: "text", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Sentiment = table.Column<string>(type: "text", nullable: false),
                    Route = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackViews", x => x.Position);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackViews_GameId",
                table: "FeedbackViews",
                column: "GameId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedbackViews");
        }
    }
}
