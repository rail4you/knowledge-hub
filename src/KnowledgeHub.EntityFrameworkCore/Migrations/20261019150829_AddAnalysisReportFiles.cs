using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisReportFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAnalysisReportFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoredPath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    DownloadCount = table.Column<int>(type: "integer", nullable: false),
                    LastDownloadedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MetricsSnapshot = table.Column<string>(type: "text", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAnalysisReportFiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAnalysisReportFiles_BatchId_Type",
                table: "AppAnalysisReportFiles",
                columns: new[] { "BatchId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAnalysisReportFiles_CourseId",
                table: "AppAnalysisReportFiles",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAnalysisReportFiles_TenantId",
                table: "AppAnalysisReportFiles",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAnalysisReportFiles");
        }
    }
}
