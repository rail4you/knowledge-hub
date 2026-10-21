using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddPaperUploadImageStudent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StudentId",
                table: "AppPaperUploadImages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentName",
                table: "AppPaperUploadImages",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StudentNumber",
                table: "AppPaperUploadImages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperUploadImages_BatchId_StudentId",
                table: "AppPaperUploadImages",
                columns: new[] { "BatchId", "StudentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppPaperUploadImages_BatchId_StudentId",
                table: "AppPaperUploadImages");

            migrationBuilder.DropColumn(
                name: "StudentId",
                table: "AppPaperUploadImages");

            migrationBuilder.DropColumn(
                name: "StudentName",
                table: "AppPaperUploadImages");

            migrationBuilder.DropColumn(
                name: "StudentNumber",
                table: "AppPaperUploadImages");
        }
    }
}
