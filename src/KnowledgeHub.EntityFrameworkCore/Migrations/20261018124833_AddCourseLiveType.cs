using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseLiveType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CourseId",
                table: "AppRecruitmentLives",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CourseTitle",
                table: "AppRecruitmentLives",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "AppRecruitmentLives",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AppRecruitmentLives_CourseId",
                table: "AppRecruitmentLives",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppRecruitmentLives_TenantId_Type_CreationTime",
                table: "AppRecruitmentLives",
                columns: new[] { "TenantId", "Type", "CreationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppRecruitmentLives_CourseId",
                table: "AppRecruitmentLives");

            migrationBuilder.DropIndex(
                name: "IX_AppRecruitmentLives_TenantId_Type_CreationTime",
                table: "AppRecruitmentLives");

            migrationBuilder.DropColumn(
                name: "CourseId",
                table: "AppRecruitmentLives");

            migrationBuilder.DropColumn(
                name: "CourseTitle",
                table: "AppRecruitmentLives");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "AppRecruitmentLives");
        }
    }
}
