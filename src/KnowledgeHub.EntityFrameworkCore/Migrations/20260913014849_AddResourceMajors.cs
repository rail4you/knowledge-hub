using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceMajors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppResourceMajors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MajorId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppResourceMajors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceMajors_MajorId",
                table: "AppResourceMajors",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceMajors_ResourceId",
                table: "AppResourceMajors",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceMajors_ResourceId_MajorId",
                table: "AppResourceMajors",
                columns: new[] { "ResourceId", "MajorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceMajors_TenantId",
                table: "AppResourceMajors",
                column: "TenantId");

            // 回填：把现有 Resources.MajorId 同步到 AppResourceMajors 作为主专业。
            // 不复制 TenantId/CreatorId 等审计字段（历史资源没有这些记录），
            // 仅保留 ResourceId/MajorId/IsPrimary 让前端能看到完整归属。
            migrationBuilder.Sql(@"
                INSERT INTO ""AppResourceMajors"" (
                    ""Id"", ""ResourceId"", ""MajorId"", ""IsPrimary"",
                    ""TenantId"", ""CreationTime"", ""IsDeleted""
                )
                SELECT
                    gen_random_uuid(),
                    ""Id"",
                    ""MajorId"",
                    TRUE,
                    ""TenantId"",
                    NOW(),
                    FALSE
                FROM ""AppResources""
                WHERE ""MajorId"" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1 FROM ""AppResourceMajors"" m
                      WHERE m.""ResourceId"" = ""AppResources"".""Id""
                        AND m.""MajorId"" = ""AppResources"".""MajorId""
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppResourceMajors");
        }
    }
}
