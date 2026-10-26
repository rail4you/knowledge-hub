using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCoursePracticumLibraryLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppResourceLibraryCourses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppResourceLibraryCourses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppResourceLibraryPracticums",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PracticumProjectId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppResourceLibraryPracticums", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryCourses_CourseId",
                table: "AppResourceLibraryCourses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryCourses_LibraryId",
                table: "AppResourceLibraryCourses",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryCourses_LibraryId_CourseId",
                table: "AppResourceLibraryCourses",
                columns: new[] { "LibraryId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryCourses_TenantId",
                table: "AppResourceLibraryCourses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryPracticums_LibraryId",
                table: "AppResourceLibraryPracticums",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryPracticums_LibraryId_PracticumProjectId",
                table: "AppResourceLibraryPracticums",
                columns: new[] { "LibraryId", "PracticumProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryPracticums_PracticumProjectId",
                table: "AppResourceLibraryPracticums",
                column: "PracticumProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryPracticums_TenantId",
                table: "AppResourceLibraryPracticums",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppResourceLibraryCourses");

            migrationBuilder.DropTable(
                name: "AppResourceLibraryPracticums");

            // 数据回填：既有课程 / 实训通过 LibraryId 单值关联，迁移后同步到多对多关联表
            migrationBuilder.Sql(@"
                INSERT INTO ""AppResourceLibraryCourses"" (""Id"", ""LibraryId"", ""CourseId"", ""TenantId"", ""CreationTime"")
                SELECT gen_random_uuid(), c.""LibraryId"", c.""Id"", c.""TenantId"", now()
                FROM ""AppCourses"" c
                WHERE c.""LibraryId"" IS NOT NULL
                  AND c.""IsDeleted"" = false
                  AND NOT EXISTS (
                      SELECT 1 FROM ""AppResourceLibraryCourses"" l
                      WHERE l.""CourseId"" = c.""Id"" AND l.""LibraryId"" = c.""LibraryId"");
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ""AppResourceLibraryPracticums"" (""Id"", ""LibraryId"", ""PracticumProjectId"", ""TenantId"", ""CreationTime"")
                SELECT gen_random_uuid(), p.""LibraryId"", p.""Id"", p.""TenantId"", now()
                FROM ""AppPracticumProjects"" p
                WHERE p.""LibraryId"" IS NOT NULL
                  AND p.""IsDeleted"" = false
                  AND NOT EXISTS (
                      SELECT 1 FROM ""AppResourceLibraryPracticums"" l
                      WHERE l.""PracticumProjectId"" = p.""Id"" AND l.""LibraryId"" = p.""LibraryId"");
            ");
        }
    }
}
