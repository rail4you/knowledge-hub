using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class ImproveMicroMajorCertificateIssuance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LayersJson",
                table: "AppMicroMajorCertificateTemplates",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Advisor",
                table: "AppMicroMajorCertificates",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IssueDate",
                table: "AppMicroMajorCertificates",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentNo",
                table: "AppMicroMajorCertificates",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidUntil",
                table: "AppMicroMajorCertificates",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LayersJson",
                table: "AppMicroMajorCertificateTemplates");

            migrationBuilder.DropColumn(
                name: "Advisor",
                table: "AppMicroMajorCertificates");

            migrationBuilder.DropColumn(
                name: "IssueDate",
                table: "AppMicroMajorCertificates");

            migrationBuilder.DropColumn(
                name: "StudentNo",
                table: "AppMicroMajorCertificates");

            migrationBuilder.DropColumn(
                name: "ValidUntil",
                table: "AppMicroMajorCertificates");
        }
    }
}
