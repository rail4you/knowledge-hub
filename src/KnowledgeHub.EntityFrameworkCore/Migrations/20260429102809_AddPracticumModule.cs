using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddPracticumModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppPracticumAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<decimal>(type: "numeric", nullable: false),
                    GradeLevel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RubricJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AssessedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
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
                    table.PrimaryKey("PK_AppPracticumAssessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPracticumEnrollments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Progress = table.Column<decimal>(type: "numeric", nullable: false),
                    EnrolledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastSubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FinalScore = table.Column<decimal>(type: "numeric", nullable: true),
                    FinalComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppPracticumEnrollments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPracticumGuidanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsVisibleToStudent = table.Column<bool>(type: "boolean", nullable: false),
                    GuidedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
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
                    table.PrimaryKey("PK_AppPracticumGuidanceRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPracticumMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MaterialType = table.Column<int>(type: "integer", nullable: false),
                    ResourceUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppPracticumMaterials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPracticumProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Major = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ClassName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    MaxScore = table.Column<decimal>(type: "numeric", nullable: false),
                    AllowResubmission = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppPracticumProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPracticumSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNo = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AttachmentUrls = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LinkUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ScreenshotUrls = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TeacherFeedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Score = table.Column<decimal>(type: "numeric", nullable: true),
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
                    table.PrimaryKey("PK_AppPracticumSubmissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPracticumTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Requirement = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DueTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ScoreWeight = table.Column<decimal>(type: "numeric", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppPracticumTasks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumAssessments_EnrollmentId",
                table: "AppPracticumAssessments",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumAssessments_ProjectId",
                table: "AppPracticumAssessments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumAssessments_SubmissionId",
                table: "AppPracticumAssessments",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumAssessments_TeacherId",
                table: "AppPracticumAssessments",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumAssessments_TenantId",
                table: "AppPracticumAssessments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumEnrollments_ProjectId",
                table: "AppPracticumEnrollments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumEnrollments_ProjectId_StudentId",
                table: "AppPracticumEnrollments",
                columns: new[] { "ProjectId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumEnrollments_Status",
                table: "AppPracticumEnrollments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumEnrollments_StudentId",
                table: "AppPracticumEnrollments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumEnrollments_TenantId",
                table: "AppPracticumEnrollments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumGuidanceRecords_EnrollmentId",
                table: "AppPracticumGuidanceRecords",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumGuidanceRecords_ProjectId",
                table: "AppPracticumGuidanceRecords",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumGuidanceRecords_TaskId",
                table: "AppPracticumGuidanceRecords",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumGuidanceRecords_TeacherId",
                table: "AppPracticumGuidanceRecords",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumGuidanceRecords_TenantId",
                table: "AppPracticumGuidanceRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumMaterials_ProjectId",
                table: "AppPracticumMaterials",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumMaterials_TaskId",
                table: "AppPracticumMaterials",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumMaterials_TenantId",
                table: "AppPracticumMaterials",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumProjects_CourseId",
                table: "AppPracticumProjects",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumProjects_Status",
                table: "AppPracticumProjects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumProjects_TenantId",
                table: "AppPracticumProjects",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumProjects_Title",
                table: "AppPracticumProjects",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumSubmissions_EnrollmentId",
                table: "AppPracticumSubmissions",
                column: "EnrollmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumSubmissions_EnrollmentId_TaskId_VersionNo",
                table: "AppPracticumSubmissions",
                columns: new[] { "EnrollmentId", "TaskId", "VersionNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumSubmissions_ProjectId",
                table: "AppPracticumSubmissions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumSubmissions_Status",
                table: "AppPracticumSubmissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumSubmissions_StudentId",
                table: "AppPracticumSubmissions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumSubmissions_TaskId",
                table: "AppPracticumSubmissions",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumSubmissions_TenantId",
                table: "AppPracticumSubmissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumTasks_ProjectId",
                table: "AppPracticumTasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumTasks_ProjectId_SortOrder",
                table: "AppPracticumTasks",
                columns: new[] { "ProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumTasks_TenantId",
                table: "AppPracticumTasks",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppPracticumAssessments");

            migrationBuilder.DropTable(
                name: "AppPracticumEnrollments");

            migrationBuilder.DropTable(
                name: "AppPracticumGuidanceRecords");

            migrationBuilder.DropTable(
                name: "AppPracticumMaterials");

            migrationBuilder.DropTable(
                name: "AppPracticumProjects");

            migrationBuilder.DropTable(
                name: "AppPracticumSubmissions");

            migrationBuilder.DropTable(
                name: "AppPracticumTasks");
        }
    }
}
