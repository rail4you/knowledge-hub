using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecialEduReviewer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerUserId",
                table: "SpecialTeachingDesigns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "SpecialEduResources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerUserId",
                table: "SpecialEduResources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "SpecialEduResources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewerUserId",
                table: "IepPlans",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewerUserId",
                table: "SpecialTeachingDesigns");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "SpecialEduResources");

            migrationBuilder.DropColumn(
                name: "ReviewerUserId",
                table: "SpecialEduResources");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SpecialEduResources");

            migrationBuilder.DropColumn(
                name: "ReviewerUserId",
                table: "IepPlans");
        }
    }
}
