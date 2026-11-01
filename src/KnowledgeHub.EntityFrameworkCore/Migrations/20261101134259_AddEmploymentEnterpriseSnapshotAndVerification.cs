using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddEmploymentEnterpriseSnapshotAndVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EnterpriseProfileId",
                table: "AppJobPostings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeSnapshotJson",
                table: "AppJobApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResumeVersionId",
                table: "AppJobApplications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SnapshotAt",
                table: "AppJobApplications",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "AppInterviewSchedules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceUrl",
                table: "AppEmploymentOutcomes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "AppEmploymentOutcomes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "AppEmploymentOutcomes",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VerifiedByUserId",
                table: "AppEmploymentOutcomes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifyNote",
                table: "AppEmploymentOutcomes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnterpriseProfileId",
                table: "AppJobPostings");

            migrationBuilder.DropColumn(
                name: "ResumeSnapshotJson",
                table: "AppJobApplications");

            migrationBuilder.DropColumn(
                name: "ResumeVersionId",
                table: "AppJobApplications");

            migrationBuilder.DropColumn(
                name: "SnapshotAt",
                table: "AppJobApplications");

            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "AppInterviewSchedules");

            migrationBuilder.DropColumn(
                name: "EvidenceUrl",
                table: "AppEmploymentOutcomes");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "AppEmploymentOutcomes");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "AppEmploymentOutcomes");

            migrationBuilder.DropColumn(
                name: "VerifiedByUserId",
                table: "AppEmploymentOutcomes");

            migrationBuilder.DropColumn(
                name: "VerifyNote",
                table: "AppEmploymentOutcomes");
        }
    }
}
