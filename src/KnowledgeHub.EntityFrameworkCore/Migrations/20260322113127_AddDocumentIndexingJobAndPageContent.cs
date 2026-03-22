using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentIndexingJobAndPageContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KhDocumentIndexingJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TotalPages = table.Column<int>(type: "integer", nullable: true),
                    ProcessedPages = table.Column<int>(type: "integer", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KhDocumentIndexingJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KhPageContents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: false),
                    PageWidth = table.Column<float>(type: "real", nullable: false),
                    PageHeight = table.Column<float>(type: "real", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    TextItemsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KhPageContents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KhPageContents_AppResources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "AppResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KhDocumentIndexingJobs_CreationTime",
                table: "KhDocumentIndexingJobs",
                column: "CreationTime");

            migrationBuilder.CreateIndex(
                name: "IX_KhDocumentIndexingJobs_NextRetryAt",
                table: "KhDocumentIndexingJobs",
                column: "NextRetryAt");

            migrationBuilder.CreateIndex(
                name: "IX_KhDocumentIndexingJobs_ResourceId",
                table: "KhDocumentIndexingJobs",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_KhDocumentIndexingJobs_Status",
                table: "KhDocumentIndexingJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_KhPageContents_ResourceId_PageNumber",
                table: "KhPageContents",
                columns: new[] { "ResourceId", "PageNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KhDocumentIndexingJobs");

            migrationBuilder.DropTable(
                name: "KhPageContents");
        }
    }
}
