using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddChapterCollaborationAuthors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatorId",
                table: "AppChapters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatorName",
                table: "AppChapters",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CreatorTenantId",
                table: "AppChapters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatorTenantName",
                table: "AppChapters",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastEditorId",
                table: "AppChapters",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastEditorName",
                table: "AppChapters",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppChapters_CreatorId",
                table: "AppChapters",
                column: "CreatorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppChapters_CreatorId",
                table: "AppChapters");

            migrationBuilder.DropColumn(
                name: "CreatorId",
                table: "AppChapters");

            migrationBuilder.DropColumn(
                name: "CreatorName",
                table: "AppChapters");

            migrationBuilder.DropColumn(
                name: "CreatorTenantId",
                table: "AppChapters");

            migrationBuilder.DropColumn(
                name: "CreatorTenantName",
                table: "AppChapters");

            migrationBuilder.DropColumn(
                name: "LastEditorId",
                table: "AppChapters");

            migrationBuilder.DropColumn(
                name: "LastEditorName",
                table: "AppChapters");
        }
    }
}
