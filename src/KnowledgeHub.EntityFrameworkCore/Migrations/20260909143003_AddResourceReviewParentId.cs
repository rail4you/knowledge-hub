using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceReviewParentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KhResourceReviews_ResourceId_UserId",
                table: "KhResourceReviews");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "KhResourceReviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KhResourceReviews_ParentId",
                table: "KhResourceReviews",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_KhResourceReviews_ResourceId_UserId",
                table: "KhResourceReviews",
                columns: new[] { "ResourceId", "UserId" },
                unique: true,
                filter: "\"ParentId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KhResourceReviews_ParentId",
                table: "KhResourceReviews");

            migrationBuilder.DropIndex(
                name: "IX_KhResourceReviews_ResourceId_UserId",
                table: "KhResourceReviews");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "KhResourceReviews");

            migrationBuilder.CreateIndex(
                name: "IX_KhResourceReviews_ResourceId_UserId",
                table: "KhResourceReviews",
                columns: new[] { "ResourceId", "UserId" },
                unique: true);
        }
    }
}
