using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddClassroomInteraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppCheckInRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CheckedInAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Location = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Metadata = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AppCheckInRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppCheckInSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequireLocation = table.Column<bool>(type: "boolean", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    RadiusMeters = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_AppCheckInSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppDiscussionReplies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DiscussionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentReplyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Content = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsAccepted = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppDiscussionReplies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppDiscussions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    KnowledgeResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Content = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ReplyCount = table.Column<int>(type: "integer", nullable: false),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    LastReplyAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppDiscussions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppGroupTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GroupMode = table.Column<int>(type: "integer", nullable: false),
                    PublishAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxScore = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppGroupTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppGroupTaskSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    AttachmentResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedById = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedByName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Feedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    GradedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppGroupTaskSubmissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPracticeAttemptAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Answer = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPracticeAttemptAnswers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPracticeAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppPracticeAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPracticeSessionItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPracticeSessionItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPracticeSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    KnowledgeResourceIds = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppPracticeSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentActivityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActionType = table.Column<int>(type: "integer", nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Metadata = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentGroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentGroupMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    GroupTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppCheckInRecords_SessionId_StudentId",
                table: "AppCheckInRecords",
                columns: new[] { "SessionId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCheckInRecords_StudentId",
                table: "AppCheckInRecords",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCheckInRecords_TenantId",
                table: "AppCheckInRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCheckInSessions_CourseId",
                table: "AppCheckInSessions",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCheckInSessions_CourseId_Status",
                table: "AppCheckInSessions",
                columns: new[] { "CourseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppCheckInSessions_InitiatorId",
                table: "AppCheckInSessions",
                column: "InitiatorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCheckInSessions_TenantId",
                table: "AppCheckInSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCheckInSessions_Token",
                table: "AppCheckInSessions",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppDiscussionReplies_DiscussionId",
                table: "AppDiscussionReplies",
                column: "DiscussionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDiscussionReplies_ParentReplyId",
                table: "AppDiscussionReplies",
                column: "ParentReplyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDiscussionReplies_TenantId",
                table: "AppDiscussionReplies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDiscussions_AuthorId",
                table: "AppDiscussions",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDiscussions_ChapterId",
                table: "AppDiscussions",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDiscussions_CourseId",
                table: "AppDiscussions",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDiscussions_CourseId_IsResolved",
                table: "AppDiscussions",
                columns: new[] { "CourseId", "IsResolved" });

            migrationBuilder.CreateIndex(
                name: "IX_AppDiscussions_KnowledgeResourceId",
                table: "AppDiscussions",
                column: "KnowledgeResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDiscussions_TenantId",
                table: "AppDiscussions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppGroupTasks_CourseId",
                table: "AppGroupTasks",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppGroupTasks_CourseId_Status",
                table: "AppGroupTasks",
                columns: new[] { "CourseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppGroupTasks_InitiatorId",
                table: "AppGroupTasks",
                column: "InitiatorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppGroupTasks_TenantId",
                table: "AppGroupTasks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppGroupTasks_Token",
                table: "AppGroupTasks",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppGroupTaskSubmissions_GroupTaskId_GroupId",
                table: "AppGroupTaskSubmissions",
                columns: new[] { "GroupTaskId", "GroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppGroupTaskSubmissions_TenantId",
                table: "AppGroupTaskSubmissions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticeAttemptAnswers_AttemptId_ExerciseId",
                table: "AppPracticeAttemptAnswers",
                columns: new[] { "AttemptId", "ExerciseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticeAttemptAnswers_TenantId",
                table: "AppPracticeAttemptAnswers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticeAttempts_SessionId_StudentId",
                table: "AppPracticeAttempts",
                columns: new[] { "SessionId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticeAttempts_StudentId",
                table: "AppPracticeAttempts",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticeAttempts_TenantId",
                table: "AppPracticeAttempts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticeSessionItems_SessionId_ExerciseId",
                table: "AppPracticeSessionItems",
                columns: new[] { "SessionId", "ExerciseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticeSessionItems_TenantId",
                table: "AppPracticeSessionItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticeSessions_CourseId",
                table: "AppPracticeSessions",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticeSessions_CourseId_Status",
                table: "AppPracticeSessions",
                columns: new[] { "CourseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticeSessions_InitiatorId",
                table: "AppPracticeSessions",
                column: "InitiatorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticeSessions_TenantId",
                table: "AppPracticeSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticeSessions_Token",
                table: "AppPracticeSessions",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentActivityLogs_CourseId",
                table: "AppStudentActivityLogs",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentActivityLogs_StudentId_CreationTime",
                table: "AppStudentActivityLogs",
                columns: new[] { "StudentId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentActivityLogs_TenantId",
                table: "AppStudentActivityLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGroupMembers_GroupId_StudentId",
                table: "AppStudentGroupMembers",
                columns: new[] { "GroupId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGroupMembers_StudentId",
                table: "AppStudentGroupMembers",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGroupMembers_TenantId",
                table: "AppStudentGroupMembers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGroups_GroupTaskId",
                table: "AppStudentGroups",
                column: "GroupTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentGroups_TenantId",
                table: "AppStudentGroups",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppCheckInRecords");

            migrationBuilder.DropTable(
                name: "AppCheckInSessions");

            migrationBuilder.DropTable(
                name: "AppDiscussionReplies");

            migrationBuilder.DropTable(
                name: "AppDiscussions");

            migrationBuilder.DropTable(
                name: "AppGroupTasks");

            migrationBuilder.DropTable(
                name: "AppGroupTaskSubmissions");

            migrationBuilder.DropTable(
                name: "AppPracticeAttemptAnswers");

            migrationBuilder.DropTable(
                name: "AppPracticeAttempts");

            migrationBuilder.DropTable(
                name: "AppPracticeSessionItems");

            migrationBuilder.DropTable(
                name: "AppPracticeSessions");

            migrationBuilder.DropTable(
                name: "AppStudentActivityLogs");

            migrationBuilder.DropTable(
                name: "AppStudentGroupMembers");

            migrationBuilder.DropTable(
                name: "AppStudentGroups");
        }
    }
}
