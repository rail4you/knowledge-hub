using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddWhiteboardCourse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CourseId",
                table: "AppWhiteboards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CourseName",
                table: "AppWhiteboards",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppWhiteboards_CourseId",
                table: "AppWhiteboards",
                column: "CourseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppWhiteboards_CourseId",
                table: "AppWhiteboards");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "AppWhiteboards");

            migrationBuilder.DropColumn(
                name: "CourseName",
                table: "AppWhiteboards");
        }
    }
}
