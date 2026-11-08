using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceSourceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceCourseId",
                table: "AppResources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceLibraryId",
                table: "AppResources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceResourceId",
                table: "AppResources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "SourceType",
                table: "AppResources",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.CreateIndex(
                name: "IX_AppResources_SourceResourceId",
                table: "AppResources",
                column: "SourceResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResources_SourceType",
                table: "AppResources",
                column: "SourceType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppResources_SourceResourceId",
                table: "AppResources");

            migrationBuilder.DropIndex(
                name: "IX_AppResources_SourceType",
                table: "AppResources");

            migrationBuilder.DropColumn(
                name: "SourceCourseId",
                table: "AppResources");

            migrationBuilder.DropColumn(
                name: "SourceLibraryId",
                table: "AppResources");

            migrationBuilder.DropColumn(
                name: "SourceResourceId",
                table: "AppResources");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "AppResources");
        }
    }
}
