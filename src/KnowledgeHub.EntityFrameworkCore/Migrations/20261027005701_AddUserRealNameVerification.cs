using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRealNameVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdCardNumber",
                table: "AbpUsers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRealNameVerified",
                table: "AbpUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RealName",
                table: "AbpUsers",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RealNameVerifiedAt",
                table: "AbpUsers",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdCardNumber",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "IsRealNameVerified",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "RealName",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "RealNameVerifiedAt",
                table: "AbpUsers");
        }
    }
}
