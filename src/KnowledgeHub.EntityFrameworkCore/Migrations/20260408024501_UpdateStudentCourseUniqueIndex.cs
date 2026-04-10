using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class UpdateStudentCourseUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppStudentCourses_StudentId_CourseId",
                table: "AppStudentCourses");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentCourses_TenantId_StudentId_CourseId",
                table: "AppStudentCourses",
                columns: new[] { "TenantId", "StudentId", "CourseId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppStudentCourses_TenantId_StudentId_CourseId",
                table: "AppStudentCourses");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentCourses_StudentId_CourseId",
                table: "AppStudentCourses",
                columns: new[] { "StudentId", "CourseId" },
                unique: true);
        }
    }
}
