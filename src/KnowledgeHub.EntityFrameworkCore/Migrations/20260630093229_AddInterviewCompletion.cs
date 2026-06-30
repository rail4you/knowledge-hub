using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddInterviewCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "AppInterviewSchedules",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompletionMessage",
                table: "AppInterviewSchedules",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeResources_ResourceId",
                table: "AppKnowledgeResources",
                column: "ResourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppKnowledgeResources_ResourceId",
                table: "AppKnowledgeResources");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "AppInterviewSchedules");

            migrationBuilder.DropColumn(
                name: "CompletionMessage",
                table: "AppInterviewSchedules");
        }
    }
}
