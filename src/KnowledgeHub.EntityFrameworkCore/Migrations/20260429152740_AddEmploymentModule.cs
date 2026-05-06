using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddEmploymentModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppEmploymentGuidanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    CareerGoal = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GuidedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
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
                    table.PrimaryKey("PK_AppEmploymentGuidanceRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppEmploymentOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    JobTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EmploymentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Region = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SalaryRange = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Remark = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppEmploymentOutcomes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppInterviewSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    InterviewerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InterviewerPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MeetingUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ResultComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResultRecordedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppInterviewSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppJobApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    JobPostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoverLetter = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EmployerRemark = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AppJobApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppJobPostings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmployerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Industry = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    JobType = table.Column<int>(type: "integer", nullable: false),
                    EducationRequirement = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SalaryRange = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RecruitmentCount = table.Column<int>(type: "integer", nullable: false),
                    SkillTags = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Benefits = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ContactName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Deadline = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppJobPostings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentResumes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SchoolName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Major = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Grade = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClassName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StudentNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Skills = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EducationExperience = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    InternshipExperience = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ProjectExperience = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CertificateText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AttachmentUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    VersionNo = table.Column<int>(type: "integer", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppStudentResumes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppEmploymentGuidanceRecords_ApplicationId",
                table: "AppEmploymentGuidanceRecords",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmploymentGuidanceRecords_GuidedAt",
                table: "AppEmploymentGuidanceRecords",
                column: "GuidedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmploymentGuidanceRecords_StudentId",
                table: "AppEmploymentGuidanceRecords",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmploymentGuidanceRecords_TeacherId",
                table: "AppEmploymentGuidanceRecords",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmploymentGuidanceRecords_TenantId",
                table: "AppEmploymentGuidanceRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmploymentOutcomes_ApplicationId",
                table: "AppEmploymentOutcomes",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmploymentOutcomes_ConfirmedAt",
                table: "AppEmploymentOutcomes",
                column: "ConfirmedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmploymentOutcomes_IsPrimary",
                table: "AppEmploymentOutcomes",
                column: "IsPrimary");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmploymentOutcomes_Status",
                table: "AppEmploymentOutcomes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmploymentOutcomes_StudentId",
                table: "AppEmploymentOutcomes",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEmploymentOutcomes_TenantId",
                table: "AppEmploymentOutcomes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppInterviewSchedules_ApplicationId",
                table: "AppInterviewSchedules",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppInterviewSchedules_JobPostingId",
                table: "AppInterviewSchedules",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppInterviewSchedules_Result",
                table: "AppInterviewSchedules",
                column: "Result");

            migrationBuilder.CreateIndex(
                name: "IX_AppInterviewSchedules_ScheduledAt",
                table: "AppInterviewSchedules",
                column: "ScheduledAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppInterviewSchedules_StudentId",
                table: "AppInterviewSchedules",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppInterviewSchedules_TenantId",
                table: "AppInterviewSchedules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobApplications_AppliedAt",
                table: "AppJobApplications",
                column: "AppliedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobApplications_JobPostingId",
                table: "AppJobApplications",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobApplications_JobPostingId_StudentId",
                table: "AppJobApplications",
                columns: new[] { "JobPostingId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppJobApplications_ResumeId",
                table: "AppJobApplications",
                column: "ResumeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobApplications_Status",
                table: "AppJobApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobApplications_StudentId",
                table: "AppJobApplications",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobApplications_TenantId",
                table: "AppJobApplications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobPostings_EmployerUserId",
                table: "AppJobPostings",
                column: "EmployerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobPostings_JobType",
                table: "AppJobPostings",
                column: "JobType");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobPostings_PublishedAt",
                table: "AppJobPostings",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobPostings_Status",
                table: "AppJobPostings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppJobPostings_TenantId",
                table: "AppJobPostings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentResumes_IsDefault",
                table: "AppStudentResumes",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentResumes_StudentId",
                table: "AppStudentResumes",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentResumes_TenantId",
                table: "AppStudentResumes",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppEmploymentGuidanceRecords");

            migrationBuilder.DropTable(
                name: "AppEmploymentOutcomes");

            migrationBuilder.DropTable(
                name: "AppInterviewSchedules");

            migrationBuilder.DropTable(
                name: "AppJobApplications");

            migrationBuilder.DropTable(
                name: "AppJobPostings");

            migrationBuilder.DropTable(
                name: "AppStudentResumes");
        }
    }
}
