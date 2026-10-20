using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddPaperPageMergeAndCourseStartDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MergeGroup",
                table: "AppPaperUploadImages",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PageIndex",
                table: "AppPaperUploadImages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedStartDate",
                table: "AppCourseLinks",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperUploadImages_BatchId_MergeGroup",
                table: "AppPaperUploadImages",
                columns: new[] { "BatchId", "MergeGroup" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppPaperUploadImages_BatchId_MergeGroup",
                table: "AppPaperUploadImages");

            migrationBuilder.DropColumn(
                name: "MergeGroup",
                table: "AppPaperUploadImages");

            migrationBuilder.DropColumn(
                name: "PageIndex",
                table: "AppPaperUploadImages");

            migrationBuilder.DropColumn(
                name: "PlannedStartDate",
                table: "AppCourseLinks");
        }
    }
}
