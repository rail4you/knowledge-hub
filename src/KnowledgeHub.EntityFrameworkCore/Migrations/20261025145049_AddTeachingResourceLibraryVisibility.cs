using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddTeachingResourceLibraryVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "Visibility",
                table: "AppResourceLibraries",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            // 历史数据可见性回填：
            // 引入 Visibility 前，所有库的教师可见范围都是「本租户全体教师可见」。
            // 因此带关联学校的库映射为「院校共建(2)」，其余映射为「公开(1)」，
            // 避免升级后存量库突然对教师不可见。
            migrationBuilder.Sql(@"
UPDATE ""AppResourceLibraries"" l
SET ""Visibility"" = 2
WHERE EXISTS (
    SELECT 1
    FROM ""AppResourceLibraryMembers"" m
    WHERE m.""LibraryId"" = l.""Id""
      AND m.""MemberTenantId"" <> l.""OwnerTenantId""
);

UPDATE ""AppResourceLibraries"" SET ""Visibility"" = 1 WHERE ""Visibility"" = 0;
");

            migrationBuilder.AddColumn<Guid>(
                name: "LibraryId",
                table: "AppPracticumProjects",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppResourceLibraryTeachers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppResourceLibraryTeachers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraries_Visibility",
                table: "AppResourceLibraries",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumProjects_LibraryId",
                table: "AppPracticumProjects",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryTeachers_LibraryId",
                table: "AppResourceLibraryTeachers",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryTeachers_LibraryId_UserId",
                table: "AppResourceLibraryTeachers",
                columns: new[] { "LibraryId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryTeachers_TenantId",
                table: "AppResourceLibraryTeachers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryTeachers_UserId",
                table: "AppResourceLibraryTeachers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppResourceLibraryTeachers");

            migrationBuilder.DropIndex(
                name: "IX_AppResourceLibraries_Visibility",
                table: "AppResourceLibraries");

            migrationBuilder.DropIndex(
                name: "IX_AppPracticumProjects_LibraryId",
                table: "AppPracticumProjects");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "AppResourceLibraries");

            migrationBuilder.DropColumn(
                name: "LibraryId",
                table: "AppPracticumProjects");
        }
    }
}
