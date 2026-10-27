using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceLibraryCertificatesAndLearners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OpenToExternalLearners",
                table: "AppResourceLibraries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // 存量数据按形态回填：校级私有库（Visibility=0）不对外开放自学，
            // 校级共享库（1）/ 校际共建库（2）对外开放自学。
            migrationBuilder.Sql(
                "UPDATE \"AppResourceLibraries\" SET \"OpenToExternalLearners\" = (\"Visibility\" <> 0);");

            migrationBuilder.CreateTable(
                name: "AppCertificateApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CourseTitle = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LibraryName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SchoolName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PhoneMasked = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsRealNameVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IsAssessmentPassed = table.Column<bool>(type: "boolean", nullable: false),
                    IsLearningCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    OwnerTeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    LearnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewerName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ReviewRemark = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppCertificateApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CourseTitle = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SchoolName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LibraryName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CertificateNo = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VerifyCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IssueDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    OwnerTeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    LearnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppCertificates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppCertificateTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    LayersJson = table.Column<string>(type: "text", nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppCertificateTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppResourceLibraryLearners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerTeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerTeacherName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LearnerTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsPhoneVerified = table.Column<bool>(type: "boolean", nullable: false),
                    PhoneVerifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RealName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IdCardMasked = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsRealNameVerified = table.Column<bool>(type: "boolean", nullable: false),
                    RealNameVerifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FirstLearningAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EnrolledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Source = table.Column<byte>(type: "smallint", nullable: false),
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
                    table.PrimaryKey("PK_AppResourceLibraryLearners", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraries_OpenToExternalLearners",
                table: "AppResourceLibraries",
                column: "OpenToExternalLearners");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificateApplications_CourseId",
                table: "AppCertificateApplications",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificateApplications_CourseId_StudentId_Status",
                table: "AppCertificateApplications",
                columns: new[] { "CourseId", "StudentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificateApplications_LibraryId",
                table: "AppCertificateApplications",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificateApplications_Status",
                table: "AppCertificateApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificateApplications_StudentId",
                table: "AppCertificateApplications",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificateApplications_TenantId",
                table: "AppCertificateApplications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificates_CertificateNo",
                table: "AppCertificates",
                column: "CertificateNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificates_CourseId",
                table: "AppCertificates",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificates_LearnerId",
                table: "AppCertificates",
                column: "LearnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificates_LibraryId",
                table: "AppCertificates",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificates_StudentId",
                table: "AppCertificates",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificates_TenantId",
                table: "AppCertificates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificates_VerifyCode",
                table: "AppCertificates",
                column: "VerifyCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificateTemplates_IsDefault",
                table: "AppCertificateTemplates",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_AppCertificateTemplates_IsEnabled",
                table: "AppCertificateTemplates",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryLearners_LibraryId",
                table: "AppResourceLibraryLearners",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryLearners_LibraryId_UserId",
                table: "AppResourceLibraryLearners",
                columns: new[] { "LibraryId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryLearners_OwnerTeacherId",
                table: "AppResourceLibraryLearners",
                column: "OwnerTeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryLearners_TenantId",
                table: "AppResourceLibraryLearners",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryLearners_UserId",
                table: "AppResourceLibraryLearners",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppCertificateApplications");

            migrationBuilder.DropTable(
                name: "AppCertificates");

            migrationBuilder.DropTable(
                name: "AppCertificateTemplates");

            migrationBuilder.DropTable(
                name: "AppResourceLibraryLearners");

            migrationBuilder.DropIndex(
                name: "IX_AppResourceLibraries_OpenToExternalLearners",
                table: "AppResourceLibraries");

            migrationBuilder.DropColumn(
                name: "OpenToExternalLearners",
                table: "AppResourceLibraries");
        }
    }
}
