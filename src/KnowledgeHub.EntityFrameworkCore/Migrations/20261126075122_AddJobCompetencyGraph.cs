using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddJobCompetencyGraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppJobCompetencyGraphs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MajorId = table.Column<Guid>(type: "uuid", nullable: true),
                    JobPostingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppJobCompetencyGraphs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppJobCoreTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    GraphId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
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
                    table.PrimaryKey("PK_AppJobCoreTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppJobTaskCompetencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CoreTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    RequiredLevel = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppJobTaskCompetencies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppJobCompetencyGraphs_JobPostingId",
                table: "AppJobCompetencyGraphs",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobCompetencyGraphs_MajorId",
                table: "AppJobCompetencyGraphs",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobCompetencyGraphs_MajorId_Version",
                table: "AppJobCompetencyGraphs",
                columns: new[] { "MajorId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_AppJobCompetencyGraphs_TenantId",
                table: "AppJobCompetencyGraphs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobCoreTasks_GraphId",
                table: "AppJobCoreTasks",
                column: "GraphId");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobCoreTasks_TenantId",
                table: "AppJobCoreTasks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobTaskCompetencies_CompetencyId",
                table: "AppJobTaskCompetencies",
                column: "CompetencyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobTaskCompetencies_CoreTaskId_CompetencyId",
                table: "AppJobTaskCompetencies",
                columns: new[] { "CoreTaskId", "CompetencyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppJobTaskCompetencies_TenantId",
                table: "AppJobTaskCompetencies",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppJobCompetencyGraphs");

            migrationBuilder.DropTable(
                name: "AppJobCoreTasks");

            migrationBuilder.DropTable(
                name: "AppJobTaskCompetencies");
        }
    }
}
