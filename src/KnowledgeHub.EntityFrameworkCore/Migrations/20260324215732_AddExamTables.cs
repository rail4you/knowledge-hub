using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddExamTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppExams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    TotalScore = table.Column<int>(type: "integer", nullable: false),
                    PassingScore = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppExams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppExercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    KnowledgeResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    QuestionContent = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Options = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Answer = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AnswerExplanation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    IsAiGenerated = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppExercises", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentExams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    GradedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TotalScore = table.Column<int>(type: "integer", nullable: true),
                    IsPassed = table.Column<bool>(type: "boolean", nullable: true),
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
                    table.PrimaryKey("PK_AppStudentExams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppExamExercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppExamExercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppExamExercises_AppExams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "AppExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Score = table.Column<int>(type: "integer", nullable: true),
                    Feedback = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: true),
                    IsAiGraded = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppStudentAnswers_AppStudentExams_StudentExamId",
                        column: x => x.StudentExamId,
                        principalTable: "AppStudentExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppExamExercises_ExamId",
                table: "AppExamExercises",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExamExercises_ExerciseId",
                table: "AppExamExercises",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExams_ChapterId",
                table: "AppExams",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExams_CourseId",
                table: "AppExams",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExams_TenantId",
                table: "AppExams",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExercises_ChapterId",
                table: "AppExercises",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExercises_CourseId",
                table: "AppExercises",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExercises_KnowledgeResourceId",
                table: "AppExercises",
                column: "KnowledgeResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExercises_TenantId",
                table: "AppExercises",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAnswers_ExerciseId",
                table: "AppStudentAnswers",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentAnswers_StudentExamId",
                table: "AppStudentAnswers",
                column: "StudentExamId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentExams_ExamId",
                table: "AppStudentExams",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentExams_Status",
                table: "AppStudentExams",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentExams_StudentId",
                table: "AppStudentExams",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentExams_TenantId",
                table: "AppStudentExams",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppExamExercises");

            migrationBuilder.DropTable(
                name: "AppExercises");

            migrationBuilder.DropTable(
                name: "AppStudentAnswers");

            migrationBuilder.DropTable(
                name: "AppExams");

            migrationBuilder.DropTable(
                name: "AppStudentExams");
        }
    }
}
