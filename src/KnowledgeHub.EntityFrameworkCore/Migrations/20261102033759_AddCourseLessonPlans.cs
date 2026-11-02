using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseLessonPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppCourseLessonPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChapterTitle = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Source = table.Column<byte>(type: "smallint", nullable: false),
                    FileUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FileExtension = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: true),
                    ContentJson = table.Column<string>(type: "text", nullable: true),
                    AiTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatorName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
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
                    table.PrimaryKey("PK_AppCourseLessonPlans", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseLessonPlans_AiTaskId",
                table: "AppCourseLessonPlans",
                column: "AiTaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseLessonPlans_ChapterId",
                table: "AppCourseLessonPlans",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseLessonPlans_CourseId",
                table: "AppCourseLessonPlans",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseLessonPlans_Source",
                table: "AppCourseLessonPlans",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseLessonPlans_TenantId",
                table: "AppCourseLessonPlans",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppCourseLessonPlans");
        }
    }
}
