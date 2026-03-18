using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddUserExtensionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClassName",
                table: "AbpUsers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "AbpUsers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Course",
                table: "AbpUsers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "AbpUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeNumber",
                table: "AbpUsers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Grade",
                table: "AbpUsers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Industry",
                table: "AbpUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Major",
                table: "AbpUsers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagementScope",
                table: "AbpUsers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartnerSchool",
                table: "AbpUsers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Position",
                table: "AbpUsers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remark",
                table: "AbpUsers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoleType",
                table: "AbpUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SchoolId",
                table: "AbpUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentNumber",
                table: "AbpUsers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "AbpUsers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnifiedSocialCreditCode",
                table: "AbpUsers",
                type: "character varying(18)",
                maxLength: 18,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClassName",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "Course",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "Department",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "EmployeeNumber",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "Grade",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "Industry",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "Major",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "ManagementScope",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "PartnerSchool",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "Remark",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "RoleType",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "StudentNumber",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "AbpUsers");

            migrationBuilder.DropColumn(
                name: "UnifiedSocialCreditCode",
                table: "AbpUsers");
        }
    }
}
