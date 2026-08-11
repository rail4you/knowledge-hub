using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseResource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppCourseResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppCourseResources", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseResources_CourseId",
                table: "AppCourseResources",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseResources_CourseId_ResourceId",
                table: "AppCourseResources",
                columns: new[] { "CourseId", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseResources_ResourceId",
                table: "AppCourseResources",
                column: "ResourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppCourseResources");
        }
    }
}
