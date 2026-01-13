using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsintBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddAiJobDebugInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Debug",
                table: "AiJobs",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DebugInfo",
                table: "AiJobs",
                type: "json",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Debug",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "DebugInfo",
                table: "AiJobs");
        }
    }
}
