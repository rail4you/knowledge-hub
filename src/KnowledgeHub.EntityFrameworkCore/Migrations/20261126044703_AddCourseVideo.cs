using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseVideo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppCourseVideos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    KnowledgeResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppCourseVideos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppVideoKnowledgeMarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseVideoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointSec = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    KnowledgeResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppVideoKnowledgeMarks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseVideos_ChapterId",
                table: "AppCourseVideos",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseVideos_CourseId",
                table: "AppCourseVideos",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseVideos_CourseId_SortOrder",
                table: "AppCourseVideos",
                columns: new[] { "CourseId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseVideos_KnowledgeResourceId",
                table: "AppCourseVideos",
                column: "KnowledgeResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseVideos_TenantId",
                table: "AppCourseVideos",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppVideoKnowledgeMarks_CourseVideoId_PointSec",
                table: "AppVideoKnowledgeMarks",
                columns: new[] { "CourseVideoId", "PointSec" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppVideoKnowledgeMarks_KnowledgeResourceId",
                table: "AppVideoKnowledgeMarks",
                column: "KnowledgeResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppVideoKnowledgeMarks_TenantId",
                table: "AppVideoKnowledgeMarks",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppCourseVideos");

            migrationBuilder.DropTable(
                name: "AppVideoKnowledgeMarks");
        }
    }
}
