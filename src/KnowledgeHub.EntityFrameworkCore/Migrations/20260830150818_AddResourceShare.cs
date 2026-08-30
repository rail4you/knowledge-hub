using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppResourceShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppResourceShares", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceShares_ResourceId",
                table: "AppResourceShares",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceShares_ResourceId_TargetTenantId",
                table: "AppResourceShares",
                columns: new[] { "ResourceId", "TargetTenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceShares_TargetTenantId",
                table: "AppResourceShares",
                column: "TargetTenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppResourceShares");
        }
    }
}
