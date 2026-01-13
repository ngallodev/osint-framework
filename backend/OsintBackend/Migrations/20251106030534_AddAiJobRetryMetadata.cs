using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsintBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddAiJobRetryMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkerHost",
                table: "AiJobs",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptStartedAt",
                table: "AiJobs",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptCompletedAt",
                table: "AiJobs",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastDurationMilliseconds",
                table: "AiJobs",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "AiJobs",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkerHost",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "LastAttemptStartedAt",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "LastAttemptCompletedAt",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "LastDurationMilliseconds",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "AiJobs");
        }
    }
}
