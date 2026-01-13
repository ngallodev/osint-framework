using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OsintBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddAiJobStructuredOutputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "WorkerHost",
                table: "AiJobs",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ErrorInfo",
                table: "AiJobs",
                type: "json",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ResultFormat",
                table: "AiJobs",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "markdown_sections_v1")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "StructuredResult",
                table: "AiJobs",
                type: "json",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorInfo",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "ResultFormat",
                table: "AiJobs");

            migrationBuilder.DropColumn(
                name: "StructuredResult",
                table: "AiJobs");

            migrationBuilder.AlterColumn<string>(
                name: "WorkerHost",
                table: "AiJobs",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
