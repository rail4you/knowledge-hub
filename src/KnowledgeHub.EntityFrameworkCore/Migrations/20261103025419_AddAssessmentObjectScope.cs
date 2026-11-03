using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentObjectScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ObjectId",
                table: "AppDoubleHighProjects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObjectName",
                table: "AppDoubleHighProjects",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ObjectType",
                table: "AppDoubleHighProjects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "BasisDocumentId",
                table: "AppDoubleHighIndicators",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ScopeId",
                table: "AppDoubleHighIndicators",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScopeName",
                table: "AppDoubleHighIndicators",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScopeType",
                table: "AppDoubleHighIndicators",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppDoubleHighProjectDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    FileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StoredPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AppDoubleHighProjectDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighIndicators_BasisDocumentId",
                table: "AppDoubleHighIndicators",
                column: "BasisDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighProjectDocuments_ProjectId",
                table: "AppDoubleHighProjectDocuments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoubleHighProjectDocuments_TenantId",
                table: "AppDoubleHighProjectDocuments",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppDoubleHighProjectDocuments");

            migrationBuilder.DropIndex(
                name: "IX_AppDoubleHighIndicators_BasisDocumentId",
                table: "AppDoubleHighIndicators");

            migrationBuilder.DropColumn(
                name: "ObjectId",
                table: "AppDoubleHighProjects");

            migrationBuilder.DropColumn(
                name: "ObjectName",
                table: "AppDoubleHighProjects");

            migrationBuilder.DropColumn(
                name: "ObjectType",
                table: "AppDoubleHighProjects");

            migrationBuilder.DropColumn(
                name: "BasisDocumentId",
                table: "AppDoubleHighIndicators");

            migrationBuilder.DropColumn(
                name: "ScopeId",
                table: "AppDoubleHighIndicators");

            migrationBuilder.DropColumn(
                name: "ScopeName",
                table: "AppDoubleHighIndicators");

            migrationBuilder.DropColumn(
                name: "ScopeType",
                table: "AppDoubleHighIndicators");
        }
    }
}
