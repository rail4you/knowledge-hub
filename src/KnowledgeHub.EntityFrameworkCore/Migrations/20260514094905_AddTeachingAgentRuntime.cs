using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddTeachingAgentRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAgentRunMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Content = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    ToolCallsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
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
                    table.PrimaryKey("PK_AppAgentRunMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAgentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClassroomAgentAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RuntimeStatus = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_AppAgentRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppClassroomAgentAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClassroomAgentTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastActiveAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SubmissionSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    HelpReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AppClassroomAgentAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppClassroomAgentTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TeachingAgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeachingAgentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskPrompt = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetSnapshotJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    DueTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PublishStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_AppClassroomAgentTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTeachingAgents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PublishedVersionId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppTeachingAgents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTeachingAgentVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeachingAgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    SystemPrompt = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    WelcomeMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ModelId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: false),
                    SkillsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    VersionNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppTeachingAgentVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAgentRunMessages_AgentRunId",
                table: "AppAgentRunMessages",
                column: "AgentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAgentRunMessages_CreationTime",
                table: "AppAgentRunMessages",
                column: "CreationTime");

            migrationBuilder.CreateIndex(
                name: "IX_AppAgentRuns_ClassroomAgentAssignmentId",
                table: "AppAgentRuns",
                column: "ClassroomAgentAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAgentRuns_RuntimeStatus",
                table: "AppAgentRuns",
                column: "RuntimeStatus");

            migrationBuilder.CreateIndex(
                name: "IX_AppAgentRuns_ThreadId",
                table: "AppAgentRuns",
                column: "ThreadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppClassroomAgentAssignments_ClassroomAgentTaskId_StudentId",
                table: "AppClassroomAgentAssignments",
                columns: new[] { "ClassroomAgentTaskId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppClassroomAgentAssignments_Status",
                table: "AppClassroomAgentAssignments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppClassroomAgentTasks_CreatorUserId",
                table: "AppClassroomAgentTasks",
                column: "CreatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppClassroomAgentTasks_PublishStatus",
                table: "AppClassroomAgentTasks",
                column: "PublishStatus");

            migrationBuilder.CreateIndex(
                name: "IX_AppClassroomAgentTasks_TeachingAgentId",
                table: "AppClassroomAgentTasks",
                column: "TeachingAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingAgents_OwnerUserId",
                table: "AppTeachingAgents",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingAgents_Status",
                table: "AppTeachingAgents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingAgents_TenantId",
                table: "AppTeachingAgents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingAgentVersions_TeachingAgentId_IsPublished",
                table: "AppTeachingAgentVersions",
                columns: new[] { "TeachingAgentId", "IsPublished" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingAgentVersions_TeachingAgentId_VersionNumber",
                table: "AppTeachingAgentVersions",
                columns: new[] { "TeachingAgentId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAgentRunMessages");

            migrationBuilder.DropTable(
                name: "AppAgentRuns");

            migrationBuilder.DropTable(
                name: "AppClassroomAgentAssignments");

            migrationBuilder.DropTable(
                name: "AppClassroomAgentTasks");

            migrationBuilder.DropTable(
                name: "AppTeachingAgents");

            migrationBuilder.DropTable(
                name: "AppTeachingAgentVersions");
        }
    }
}
