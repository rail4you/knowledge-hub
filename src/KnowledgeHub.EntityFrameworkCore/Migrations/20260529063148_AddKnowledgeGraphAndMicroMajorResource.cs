using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeGraphAndMicroMajorResource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppKnowledgeNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Metadata = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Embedding = table.Column<float[]>(type: "real[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppKnowledgeNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppKnowledgeRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppKnowledgeRelations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppMicroMajorResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MicroMajorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppMicroMajorResources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeNodes_CourseId",
                table: "AppKnowledgeNodes",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeNodes_KnowledgeResourceId",
                table: "AppKnowledgeNodes",
                column: "KnowledgeResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeNodes_TenantId",
                table: "AppKnowledgeNodes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeRelations_SourceNodeId",
                table: "AppKnowledgeRelations",
                column: "SourceNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeRelations_TargetNodeId",
                table: "AppKnowledgeRelations",
                column: "TargetNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeRelations_TenantId",
                table: "AppKnowledgeRelations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeRelations_Type",
                table: "AppKnowledgeRelations",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorResources_MicroMajorId",
                table: "AppMicroMajorResources",
                column: "MicroMajorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorResources_MicroMajorId_ResourceId",
                table: "AppMicroMajorResources",
                columns: new[] { "MicroMajorId", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorResources_ResourceId",
                table: "AppMicroMajorResources",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorResources_TenantId",
                table: "AppMicroMajorResources",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppKnowledgeNodes");

            migrationBuilder.DropTable(
                name: "AppKnowledgeRelations");

            migrationBuilder.DropTable(
                name: "AppMicroMajorResources");
        }
    }
}
