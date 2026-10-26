using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceLibraryResourceLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppResourceLibraryResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppResourceLibraryResources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryResources_LibraryId",
                table: "AppResourceLibraryResources",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryResources_LibraryId_ResourceId",
                table: "AppResourceLibraryResources",
                columns: new[] { "LibraryId", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryResources_ResourceId",
                table: "AppResourceLibraryResources",
                column: "ResourceId");

            // 数据回填：既有资源通过 Resource.LibraryId 关联到单个库，
            // 迁移后统一登记到多对多关联表，保证「库内内容 / 数量」口径一致。
            migrationBuilder.Sql(@"
                INSERT INTO ""AppResourceLibraryResources"" (""Id"", ""LibraryId"", ""ResourceId"", ""TenantId"", ""CreationTime"")
                SELECT gen_random_uuid(), r.""LibraryId"", r.""Id"", r.""TenantId"", now()
                FROM ""AppResources"" r
                WHERE r.""LibraryId"" IS NOT NULL
                  AND r.""IsDeleted"" = false
                  AND NOT EXISTS (
                      SELECT 1 FROM ""AppResourceLibraryResources"" l
                      WHERE l.""ResourceId"" = r.""Id"" AND l.""LibraryId"" = r.""LibraryId"");
            ");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryResources_TenantId",
                table: "AppResourceLibraryResources",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppResourceLibraryResources");
        }
    }
}
