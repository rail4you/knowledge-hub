using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddMicroMajorModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppMicroMajorCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MicroMajorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VerifyCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppMicroMajorCertificates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppMicroMajorCourses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MicroMajorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsCore = table.Column<bool>(type: "boolean", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppMicroMajorCourses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppMicroMajorEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MicroMajorId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Progress = table.Column<decimal>(type: "numeric", nullable: false),
                    EnrolledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CertificateIssuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppMicroMajorEnrollments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppMicroMajors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IndustryField = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CollaborationUnit = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequiredCompletionRate = table.Column<decimal>(type: "numeric", nullable: false),
                    IsCertificateEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppMicroMajors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorCertificates_CertificateNo",
                table: "AppMicroMajorCertificates",
                column: "CertificateNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorCertificates_EnrollmentId",
                table: "AppMicroMajorCertificates",
                column: "EnrollmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorCertificates_StudentId",
                table: "AppMicroMajorCertificates",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorCertificates_TenantId",
                table: "AppMicroMajorCertificates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorCertificates_VerifyCode",
                table: "AppMicroMajorCertificates",
                column: "VerifyCode");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorCourses_CourseId",
                table: "AppMicroMajorCourses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorCourses_MicroMajorId",
                table: "AppMicroMajorCourses",
                column: "MicroMajorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorCourses_MicroMajorId_CourseId",
                table: "AppMicroMajorCourses",
                columns: new[] { "MicroMajorId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorCourses_TenantId",
                table: "AppMicroMajorCourses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorEnrollments_MicroMajorId",
                table: "AppMicroMajorEnrollments",
                column: "MicroMajorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorEnrollments_MicroMajorId_StudentId",
                table: "AppMicroMajorEnrollments",
                columns: new[] { "MicroMajorId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorEnrollments_Status",
                table: "AppMicroMajorEnrollments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorEnrollments_StudentId",
                table: "AppMicroMajorEnrollments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajorEnrollments_TenantId",
                table: "AppMicroMajorEnrollments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajors_Status",
                table: "AppMicroMajors",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajors_TenantId",
                table: "AppMicroMajors",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMicroMajors_Title",
                table: "AppMicroMajors",
                column: "Title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppMicroMajorCertificates");

            migrationBuilder.DropTable(
                name: "AppMicroMajorCourses");

            migrationBuilder.DropTable(
                name: "AppMicroMajorEnrollments");

            migrationBuilder.DropTable(
                name: "AppMicroMajors");
        }
    }
}
