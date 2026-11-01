using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseSharedRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppCourseSharedRefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<byte>(type: "smallint", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    RevokedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RevokedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppCourseSharedRefs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppResourceUsageEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppResourceUsageEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseSharedRefs_ContentId_TargetTenantId",
                table: "AppCourseSharedRefs",
                columns: new[] { "ContentId", "TargetTenantId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseSharedRefs_LibraryId",
                table: "AppCourseSharedRefs",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseSharedRefs_SourceTenantId",
                table: "AppCourseSharedRefs",
                column: "SourceTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseSharedRefs_TargetTenantId_CourseId_Status",
                table: "AppCourseSharedRefs",
                columns: new[] { "TargetTenantId", "CourseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceUsageEvents_EventId",
                table: "AppResourceUsageEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceUsageEvents_RefId",
                table: "AppResourceUsageEvents",
                column: "RefId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceUsageEvents_SchoolId_ResourceId_OccurredAt",
                table: "AppResourceUsageEvents",
                columns: new[] { "SchoolId", "ResourceId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppCourseSharedRefs");

            migrationBuilder.DropTable(
                name: "AppResourceUsageEvents");
        }
    }
}
