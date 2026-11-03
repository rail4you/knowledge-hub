using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppCourseCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChapterTitle = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Industry = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Timeframe = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ContentJson = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<byte>(type: "smallint", nullable: false),
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
                    table.PrimaryKey("PK_AppCourseCases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseCases_AiTaskId",
                table: "AppCourseCases",
                column: "AiTaskId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseCases_ChapterId",
                table: "AppCourseCases",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseCases_CourseId",
                table: "AppCourseCases",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseCases_Source",
                table: "AppCourseCases",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseCases_TenantId",
                table: "AppCourseCases",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppCourseCases");
        }
    }
}
