using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddRecruitmentLive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppRecruitmentLives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RoomCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    InterviewScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppRecruitmentLives", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppRecruitmentLives_RoomCode",
                table: "AppRecruitmentLives",
                column: "RoomCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppRecruitmentLives_Status",
                table: "AppRecruitmentLives",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppRecruitmentLives_StudentId",
                table: "AppRecruitmentLives",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppRecruitmentLives_TeacherId",
                table: "AppRecruitmentLives",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_AppRecruitmentLives_TenantId",
                table: "AppRecruitmentLives",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppRecruitmentLives");
        }
    }
}
