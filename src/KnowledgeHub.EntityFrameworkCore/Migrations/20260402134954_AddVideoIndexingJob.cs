using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoIndexingJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KhVideoIndexingJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TotalEvents = table.Column<int>(type: "integer", nullable: true),
                    ProcessedEvents = table.Column<int>(type: "integer", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KhVideoIndexingJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KhVideoIndexingJobs_CreationTime",
                table: "KhVideoIndexingJobs",
                column: "CreationTime");

            migrationBuilder.CreateIndex(
                name: "IX_KhVideoIndexingJobs_NextRetryAt",
                table: "KhVideoIndexingJobs",
                column: "NextRetryAt");

            migrationBuilder.CreateIndex(
                name: "IX_KhVideoIndexingJobs_ResourceId",
                table: "KhVideoIndexingJobs",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_KhVideoIndexingJobs_Status",
                table: "KhVideoIndexingJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KhVideoIndexingJobs");
        }
    }
}
