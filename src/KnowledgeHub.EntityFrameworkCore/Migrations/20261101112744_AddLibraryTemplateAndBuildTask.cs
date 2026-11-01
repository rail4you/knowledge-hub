using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryTemplateAndBuildTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfigSnapshotJson",
                table: "AppResourceLibraries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "AppResourceLibraries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TemplateVersion",
                table: "AppResourceLibraries",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppBuildTaskDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<byte>(type: "smallint", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmitterSchoolId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBuildTaskDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppBuildTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OwnerSchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Requirements = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
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
                    table.PrimaryKey("PK_AppBuildTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppBuildTaskTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppBuildTaskTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppLibraryRoleAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Role = table.Column<byte>(type: "smallint", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLibraryRoleAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppResourceLibraryTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ConfigJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
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
                    table.PrimaryKey("PK_AppResourceLibraryTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppBuildTaskCollaborators",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Capability = table.Column<byte>(type: "smallint", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBuildTaskCollaborators", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppBuildTaskCollaborators_AppBuildTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "AppBuildTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppBuildTaskItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<byte>(type: "smallint", nullable: false),
                    RequiredCount = table.Column<int>(type: "integer", nullable: false),
                    AcceptedCount = table.Column<int>(type: "integer", nullable: false),
                    Acceptance = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBuildTaskItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppBuildTaskItems_AppBuildTasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "AppBuildTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppBuildTaskTemplateItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<byte>(type: "smallint", nullable: false),
                    RequiredCount = table.Column<int>(type: "integer", nullable: false),
                    Acceptance = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppBuildTaskTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppBuildTaskTemplateItems_AppBuildTaskTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "AppBuildTaskTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildTaskCollaborators_SchoolId_UserId",
                table: "AppBuildTaskCollaborators",
                columns: new[] { "SchoolId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildTaskCollaborators_TaskId",
                table: "AppBuildTaskCollaborators",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildTaskDeliveries_SubmittedBy",
                table: "AppBuildTaskDeliveries",
                column: "SubmittedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildTaskDeliveries_TaskId_Status",
                table: "AppBuildTaskDeliveries",
                columns: new[] { "TaskId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildTaskDeliveries_TaskItemId",
                table: "AppBuildTaskDeliveries",
                column: "TaskItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildTaskItems_TaskId",
                table: "AppBuildTaskItems",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildTasks_LibraryId_Status",
                table: "AppBuildTasks",
                columns: new[] { "LibraryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildTasks_OwnerSchoolId",
                table: "AppBuildTasks",
                column: "OwnerSchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildTasks_OwnerUserId",
                table: "AppBuildTasks",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildTaskTemplateItems_TemplateId",
                table: "AppBuildTaskTemplateItems",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AppBuildTaskTemplates_LibraryId",
                table: "AppBuildTaskTemplates",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryRoleAssignments_LibraryId_UserId",
                table: "AppLibraryRoleAssignments",
                columns: new[] { "LibraryId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppLibraryRoleAssignments_TenantId",
                table: "AppLibraryRoleAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryTemplates_Code_Version",
                table: "AppResourceLibraryTemplates",
                columns: new[] { "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryTemplates_Status",
                table: "AppResourceLibraryTemplates",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppBuildTaskCollaborators");

            migrationBuilder.DropTable(
                name: "AppBuildTaskDeliveries");

            migrationBuilder.DropTable(
                name: "AppBuildTaskItems");

            migrationBuilder.DropTable(
                name: "AppBuildTaskTemplateItems");

            migrationBuilder.DropTable(
                name: "AppLibraryRoleAssignments");

            migrationBuilder.DropTable(
                name: "AppResourceLibraryTemplates");

            migrationBuilder.DropTable(
                name: "AppBuildTasks");

            migrationBuilder.DropTable(
                name: "AppBuildTaskTemplates");

            migrationBuilder.DropColumn(
                name: "ConfigSnapshotJson",
                table: "AppResourceLibraries");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "AppResourceLibraries");

            migrationBuilder.DropColumn(
                name: "TemplateVersion",
                table: "AppResourceLibraries");
        }
    }
}
