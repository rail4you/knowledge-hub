using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultiChapterCourseLessonPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppCourseLessonPlans_AiTaskId",
                table: "AppCourseLessonPlans");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseLessonPlans_AiTaskId",
                table: "AppCourseLessonPlans",
                column: "AiTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseLessonPlans_AiTaskId_ChapterId",
                table: "AppCourseLessonPlans",
                columns: new[] { "AiTaskId", "ChapterId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppCourseLessonPlans_AiTaskId",
                table: "AppCourseLessonPlans");

            migrationBuilder.DropIndex(
                name: "IX_AppCourseLessonPlans_AiTaskId_ChapterId",
                table: "AppCourseLessonPlans");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseLessonPlans_AiTaskId",
                table: "AppCourseLessonPlans",
                column: "AiTaskId",
                unique: true);
        }
    }
}
