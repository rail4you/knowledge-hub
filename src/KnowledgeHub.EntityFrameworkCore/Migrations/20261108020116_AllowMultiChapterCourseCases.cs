using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultiChapterCourseCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppCourseCases_AiTaskId",
                table: "AppCourseCases");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseCases_AiTaskId",
                table: "AppCourseCases",
                column: "AiTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseCases_AiTaskId_ChapterId",
                table: "AppCourseCases",
                columns: new[] { "AiTaskId", "ChapterId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppCourseCases_AiTaskId",
                table: "AppCourseCases");

            migrationBuilder.DropIndex(
                name: "IX_AppCourseCases_AiTaskId_ChapterId",
                table: "AppCourseCases");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseCases_AiTaskId",
                table: "AppCourseCases",
                column: "AiTaskId",
                unique: true);
        }
    }
}
