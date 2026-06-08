using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <summary>
    /// P1-13：给 AppResources 表加 IsResume 字段。
    /// 上传"文档"时可勾选"作为简历使用"。
    /// 职业规划下拉按 IsResume=true AND Status=LeagueApproved AND CreatorId=当前用户 过滤。
    /// </summary>
    public partial class AddIsResumeToResource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsResume",
                table: "AppResources",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsResume",
                table: "AppResources");
        }
    }
}
