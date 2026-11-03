using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddExamAnswerSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StudentExamId",
                table: "AppAnswerRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAnswerRecords_StudentExamId",
                table: "AppAnswerRecords",
                column: "StudentExamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppAnswerRecords_StudentExamId",
                table: "AppAnswerRecords");

            migrationBuilder.DropColumn(
                name: "StudentExamId",
                table: "AppAnswerRecords");
        }
    }
}
