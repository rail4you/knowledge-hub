using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppCourseAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    MaxScore = table.Column<decimal>(type: "numeric", nullable: false),
                    Deadline = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    AllowLate = table.Column<bool>(type: "boolean", nullable: false),
                    LatePenaltyPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    AllowedFileTypes = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    MaxFileSizeMB = table.Column<int>(type: "integer", nullable: false),
                    MaxFileCount = table.Column<int>(type: "integer", nullable: false),
                    AllowResubmit = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClosedTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppCourseAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAssignmentCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MaxScore = table.Column<decimal>(type: "numeric", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppAssignmentCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAssignmentCriteria_AppCourseAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "AppCourseAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppAssignmentSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StudentNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    HonorPledge = table.Column<bool>(type: "boolean", nullable: false),
                    FirstSubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsLate = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<decimal>(type: "numeric", nullable: true),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewerName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AllowResubmit = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppAssignmentSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAssignmentSubmissions_AppCourseAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "AppCourseAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppAssignmentFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppAssignmentFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAssignmentFiles_AppAssignmentSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "AppAssignmentSubmissions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppAssignmentFiles_AppCourseAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "AppCourseAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppSubmissionScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CriterionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CriterionName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MaxScore = table.Column<decimal>(type: "numeric", nullable: false),
                    Score = table.Column<decimal>(type: "numeric", nullable: false),
                    Comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppSubmissionScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppSubmissionScores_AppAssignmentSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "AppAssignmentSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAssignmentCriteria_AssignmentId",
                table: "AppAssignmentCriteria",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAssignmentCriteria_TenantId",
                table: "AppAssignmentCriteria",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAssignmentFiles_AssignmentId",
                table: "AppAssignmentFiles",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAssignmentFiles_SubmissionId",
                table: "AppAssignmentFiles",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAssignmentFiles_TenantId",
                table: "AppAssignmentFiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAssignmentSubmissions_AssignmentId",
                table: "AppAssignmentSubmissions",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAssignmentSubmissions_AssignmentId_StudentId",
                table: "AppAssignmentSubmissions",
                columns: new[] { "AssignmentId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAssignmentSubmissions_Status",
                table: "AppAssignmentSubmissions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppAssignmentSubmissions_StudentId",
                table: "AppAssignmentSubmissions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAssignmentSubmissions_TenantId",
                table: "AppAssignmentSubmissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseAssignments_CourseId",
                table: "AppCourseAssignments",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseAssignments_CourseId_Deadline",
                table: "AppCourseAssignments",
                columns: new[] { "CourseId", "Deadline" });

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseAssignments_Status",
                table: "AppCourseAssignments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseAssignments_TenantId",
                table: "AppCourseAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSubmissionScores_SubmissionId",
                table: "AppSubmissionScores",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSubmissionScores_TenantId",
                table: "AppSubmissionScores",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAssignmentCriteria");

            migrationBuilder.DropTable(
                name: "AppAssignmentFiles");

            migrationBuilder.DropTable(
                name: "AppSubmissionScores");

            migrationBuilder.DropTable(
                name: "AppAssignmentSubmissions");

            migrationBuilder.DropTable(
                name: "AppCourseAssignments");
        }
    }
}
