using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddDoubleHighModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppDoubleHighEvidences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    IndicatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EvidenceType = table.Column<int>(type: "integer", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttachmentUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExternalLink = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDoubleHighEvidences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppDoubleHighIndicators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IndicatorCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Unit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DataSourceType = table.Column<int>(type: "integer", nullable: false),
                    TargetValue = table.Column<decimal>(type: "numeric", nullable: true),
                    Weight = table.Column<decimal>(type: "numeric", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDoubleHighIndicators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppDoubleHighIndicatorValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    IndicatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDoubleHighIndicatorValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppDoubleHighProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BatchCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastCollectedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDoubleHighProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppDoubleHighReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SummaryJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    GeneratedById = table.Column<Guid>(type: "uuid", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDoubleHighReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighEvidences_IndicatorId",
                table: "AppDoubleHighEvidences",
                column: "IndicatorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighEvidences_ProjectId",
                table: "AppDoubleHighEvidences",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighEvidences_ResourceId",
                table: "AppDoubleHighEvidences",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighEvidences_TenantId",
                table: "AppDoubleHighEvidences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighIndicators_ParentId",
                table: "AppDoubleHighIndicators",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighIndicators_ProjectId",
                table: "AppDoubleHighIndicators",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighIndicators_ProjectId_IndicatorCode",
                table: "AppDoubleHighIndicators",
                columns: new[] { "ProjectId", "IndicatorCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighIndicators_TenantId",
                table: "AppDoubleHighIndicators",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighIndicatorValues_CollectedAt",
                table: "AppDoubleHighIndicatorValues",
                column: "CollectedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighIndicatorValues_IndicatorId",
                table: "AppDoubleHighIndicatorValues",
                column: "IndicatorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighIndicatorValues_ProjectId",
                table: "AppDoubleHighIndicatorValues",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighIndicatorValues_TenantId",
                table: "AppDoubleHighIndicatorValues",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighProjects_BatchCode",
                table: "AppDoubleHighProjects",
                column: "BatchCode");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighProjects_Status",
                table: "AppDoubleHighProjects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighProjects_TenantId",
                table: "AppDoubleHighProjects",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighReports_GeneratedAt",
                table: "AppDoubleHighReports",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighReports_GeneratedById",
                table: "AppDoubleHighReports",
                column: "GeneratedById");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighReports_ProjectId",
                table: "AppDoubleHighReports",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighReports_TenantId",
                table: "AppDoubleHighReports",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppDoubleHighEvidences");

            migrationBuilder.DropTable(
                name: "AppDoubleHighIndicators");

            migrationBuilder.DropTable(
                name: "AppDoubleHighIndicatorValues");

            migrationBuilder.DropTable(
                name: "AppDoubleHighProjects");

            migrationBuilder.DropTable(
                name: "AppDoubleHighReports");
        }
    }
}
