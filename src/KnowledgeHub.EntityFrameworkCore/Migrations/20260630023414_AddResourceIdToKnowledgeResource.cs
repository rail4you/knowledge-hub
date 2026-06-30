using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceIdToKnowledgeResource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResourceId",
                table: "AppKnowledgeResources",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppChapterResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppChapterResources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppChapterResources_ChapterId",
                table: "AppChapterResources",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppChapterResources_ChapterId_ResourceId",
                table: "AppChapterResources",
                columns: new[] { "ChapterId", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppChapterResources_ResourceId",
                table: "AppChapterResources",
                column: "ResourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppChapterResources");

            migrationBuilder.DropColumn(
                name: "ResourceId",
                table: "AppKnowledgeResources");
        }
    }
}
