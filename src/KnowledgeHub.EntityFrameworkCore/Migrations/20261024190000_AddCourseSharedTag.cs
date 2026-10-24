using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <summary>
    /// 给 AppCourses 表加 IsShared 字段：标记课程是否为「共享课」。
    /// 发布为共享课时置为 true，并同步共享给平台「社会学校」租户，
    /// 使该租户下的所有注册用户都能选课学习。
    /// </summary>
    public partial class AddCourseSharedTag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsShared",
                table: "AppCourses",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsShared",
                table: "AppCourses");
        }
    }
}
