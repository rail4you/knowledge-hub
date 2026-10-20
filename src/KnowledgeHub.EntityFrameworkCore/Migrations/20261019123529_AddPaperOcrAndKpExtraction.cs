using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddPaperOcrAndKpExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OcrModel",
                table: "AppPaperScripts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OcrProvider",
                table: "AppPaperScripts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawOcrJson",
                table: "AppPaperScripts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileName",
                table: "AppPaperScripts",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RawText",
                table: "AppPaperItemScores",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppPaperUploadImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StoredPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OcrProvider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    OcrModel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OcrRawJson = table.Column<string>(type: "text", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ScriptId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPaperUploadImages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperUploadImages_BatchId_Status",
                table: "AppPaperUploadImages",
                columns: new[] { "BatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperUploadImages_ScriptId",
                table: "AppPaperUploadImages",
                column: "ScriptId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperUploadImages_TenantId",
                table: "AppPaperUploadImages",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppPaperUploadImages");

            migrationBuilder.DropColumn(
                name: "OcrModel",
                table: "AppPaperScripts");

            migrationBuilder.DropColumn(
                name: "OcrProvider",
                table: "AppPaperScripts");

            migrationBuilder.DropColumn(
                name: "RawOcrJson",
                table: "AppPaperScripts");

            migrationBuilder.DropColumn(
                name: "SourceFileName",
                table: "AppPaperScripts");

            migrationBuilder.DropColumn(
                name: "RawText",
                table: "AppPaperItemScores");
        }
    }
}
