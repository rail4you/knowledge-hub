using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCourseLinkSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeadWeeks",
                table: "AppCourseLinks");

            migrationBuilder.DropColumn(
                name: "PlannedStartDate",
                table: "AppCourseLinks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeadWeeks",
                table: "AppCourseLinks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedStartDate",
                table: "AppCourseLinks",
                type: "timestamp without time zone",
                nullable: true);
        }
    }
}
