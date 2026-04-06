using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddResourcePageIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KhResourcePageIndices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageIndexJson = table.Column<string>(type: "jsonb", nullable: false),
                    SourceFormat = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Model = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NodeCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KhResourcePageIndices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KhResourcePageIndices_AppResourceVersions_ResourceVersionId",
                        column: x => x.ResourceVersionId,
                        principalTable: "AppResourceVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KhResourcePageIndices_AppResources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "AppResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KhResourcePageIndices_ResourceId",
                table: "KhResourcePageIndices",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_KhResourcePageIndices_ResourceVersionId",
                table: "KhResourcePageIndices",
                column: "ResourceVersionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KhResourcePageIndices");
        }
    }
}
