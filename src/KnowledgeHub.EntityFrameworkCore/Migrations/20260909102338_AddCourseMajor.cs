using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseMajor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppCourseMajors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppCourseMajors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseMajors_CourseId",
                table: "AppCourseMajors",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseMajors_CourseId_MajorId",
                table: "AppCourseMajors",
                columns: new[] { "CourseId", "MajorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseMajors_MajorId",
                table: "AppCourseMajors",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseMajors_TenantId",
                table: "AppCourseMajors",
                column: "TenantId");

            // 回填：已有单专业归属的课程生成一条主专业关联；
            // MajorId 为空的历史课程无关联记录，上线后即视为“公共课”。
            migrationBuilder.Sql(@"
                INSERT INTO ""AppCourseMajors"" (""Id"", ""TenantId"", ""CourseId"", ""MajorId"", ""IsPrimary"", ""CreationTime"", ""IsDeleted"")
                SELECT gen_random_uuid(), ""TenantId"", ""Id"", ""MajorId"", TRUE, NOW(), FALSE
                FROM ""AppCourses""
                WHERE ""MajorId"" IS NOT NULL AND ""IsDeleted"" = FALSE
                ON CONFLICT DO NOTHING;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppCourseMajors");
        }
    }
}
