using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentExerciseRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppStudentExerciseRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentAnswer = table.Column<string>(type: "text", nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    HasViewedAnswer = table.Column<bool>(type: "boolean", nullable: false),
                    ViewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SelfAssessment = table.Column<int>(type: "integer", nullable: false),
                    TimeSpent = table.Column<TimeSpan>(type: "interval", nullable: false),
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
                    table.PrimaryKey("PK_AppStudentExerciseRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentExerciseRecords_ChapterId",
                table: "AppStudentExerciseRecords",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentExerciseRecords_CompletedAt",
                table: "AppStudentExerciseRecords",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentExerciseRecords_CourseId",
                table: "AppStudentExerciseRecords",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentExerciseRecords_StudentId",
                table: "AppStudentExerciseRecords",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentExerciseRecords_TenantId_StudentId_ExerciseId",
                table: "AppStudentExerciseRecords",
                columns: new[] { "TenantId", "StudentId", "ExerciseId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppStudentExerciseRecords");
        }
    }
}
