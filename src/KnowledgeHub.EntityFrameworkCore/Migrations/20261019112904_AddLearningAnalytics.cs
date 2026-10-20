using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAnalysisReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Scope = table.Column<int>(type: "integer", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaperBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Summary = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true),
                    FactsJson = table.Column<string>(type: "text", nullable: true),
                    ContentJson = table.Column<string>(type: "text", nullable: true),
                    FactsHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    GeneratedByLlm = table.Column<bool>(type: "boolean", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PushedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAnalysisReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppAnswerRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    FullScore = table.Column<double>(type: "double precision", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    AnsweredAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PaperScriptId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAnswerRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppCourseLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PredecessorCourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuccessorCourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeadWeeks = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppCourseLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppExerciseKnowledgePoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgePointId = table.Column<Guid>(type: "uuid", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppExerciseKnowledgePoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppKnowledgePoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    CanonicalId = table.Column<Guid>(type: "uuid", nullable: true),
                    SyllabusOrder = table.Column<int>(type: "integer", nullable: false),
                    Embedding = table.Column<float[]>(type: "real[]", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppKnowledgePoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppKpPrerequisites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    FromKpId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToKpId = table.Column<Guid>(type: "uuid", nullable: false),
                    Strength = table.Column<double>(type: "double precision", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Condition = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppKpPrerequisites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppMasteryThresholds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgePointId = table.Column<Guid>(type: "uuid", nullable: true),
                    WeakLine = table.Column<double>(type: "double precision", nullable: false),
                    OkLine = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppMasteryThresholds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPaperBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExamDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ScriptCount = table.Column<int>(type: "integer", nullable: false),
                    NeedsConfirmCount = table.Column<int>(type: "integer", nullable: false),
                    PushedCount = table.Column<int>(type: "integer", nullable: false),
                    AverageScore = table.Column<double>(type: "double precision", nullable: false),
                    SourceFileNames = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    IsMockOcr = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppPaperBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppReviewPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpcomingCourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpcomingCourseTitle = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Strategy = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    PushedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppReviewPackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentKpMasteries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgePointId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mastery = table.Column<double>(type: "double precision", nullable: false),
                    Evidence = table.Column<double>(type: "double precision", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentKpMasteries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Content = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Source = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStudentNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppTeachingInsights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaperBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChapterTitle = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AverageRate = table.Column<double>(type: "double precision", nullable: false),
                    StdDev = table.Column<double>(type: "double precision", nullable: false),
                    TopQuartileRate = table.Column<double>(type: "double precision", nullable: false),
                    SampleSize = table.Column<int>(type: "integer", nullable: false),
                    Diagnosis = table.Column<int>(type: "integer", nullable: false),
                    Advice = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppTeachingInsights", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppPaperScripts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StudentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TotalScore = table.Column<double>(type: "double precision", nullable: false),
                    OcrConfidence = table.Column<double>(type: "double precision", nullable: false),
                    Confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    PaperBatchId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPaperScripts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppPaperScripts_AppPaperBatches_PaperBatchId",
                        column: x => x.PaperBatchId,
                        principalTable: "AppPaperBatches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppReviewPackageItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PackageId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgePointId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Mastery = table.Column<double>(type: "double precision", nullable: false),
                    Impacts = table.Column<int>(type: "integer", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ReviewPackageId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppReviewPackageItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppReviewPackageItems_AppReviewPackages_ReviewPackageId",
                        column: x => x.ReviewPackageId,
                        principalTable: "AppReviewPackages",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "AppPaperItemScores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScriptId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionNo = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    MatchStatus = table.Column<int>(type: "integer", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    FullScore = table.Column<double>(type: "double precision", nullable: false),
                    OcrConfidence = table.Column<double>(type: "double precision", nullable: false),
                    PaperScriptId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPaperItemScores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppPaperItemScores_AppPaperScripts_PaperScriptId",
                        column: x => x.PaperScriptId,
                        principalTable: "AppPaperScripts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAnalysisReports_FactsHash",
                table: "AppAnalysisReports",
                column: "FactsHash");

            migrationBuilder.CreateIndex(
                name: "IX_AppAnalysisReports_PaperBatchId",
                table: "AppAnalysisReports",
                column: "PaperBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAnalysisReports_StudentId_CourseId",
                table: "AppAnalysisReports",
                columns: new[] { "StudentId", "CourseId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAnalysisReports_TenantId",
                table: "AppAnalysisReports",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAnswerRecords_ExerciseId",
                table: "AppAnswerRecords",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAnswerRecords_PaperScriptId",
                table: "AppAnswerRecords",
                column: "PaperScriptId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAnswerRecords_StudentId_CourseId",
                table: "AppAnswerRecords",
                columns: new[] { "StudentId", "CourseId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppAnswerRecords_TenantId",
                table: "AppAnswerRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseLinks_PredecessorCourseId_SuccessorCourseId",
                table: "AppCourseLinks",
                columns: new[] { "PredecessorCourseId", "SuccessorCourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseLinks_TenantId",
                table: "AppCourseLinks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExerciseKnowledgePoints_ExerciseId_KnowledgePointId",
                table: "AppExerciseKnowledgePoints",
                columns: new[] { "ExerciseId", "KnowledgePointId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppExerciseKnowledgePoints_KnowledgePointId",
                table: "AppExerciseKnowledgePoints",
                column: "KnowledgePointId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExerciseKnowledgePoints_TenantId",
                table: "AppExerciseKnowledgePoints",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgePoints_CanonicalId",
                table: "AppKnowledgePoints",
                column: "CanonicalId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgePoints_ChapterId",
                table: "AppKnowledgePoints",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgePoints_CourseId_Name",
                table: "AppKnowledgePoints",
                columns: new[] { "CourseId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgePoints_TenantId",
                table: "AppKnowledgePoints",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKpPrerequisites_FromKpId",
                table: "AppKpPrerequisites",
                column: "FromKpId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKpPrerequisites_FromKpId_ToKpId_Status",
                table: "AppKpPrerequisites",
                columns: new[] { "FromKpId", "ToKpId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppKpPrerequisites_TenantId",
                table: "AppKpPrerequisites",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKpPrerequisites_ToKpId",
                table: "AppKpPrerequisites",
                column: "ToKpId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMasteryThresholds_CourseId_KnowledgePointId",
                table: "AppMasteryThresholds",
                columns: new[] { "CourseId", "KnowledgePointId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppMasteryThresholds_TenantId",
                table: "AppMasteryThresholds",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperBatches_CourseId",
                table: "AppPaperBatches",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperBatches_Status",
                table: "AppPaperBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperBatches_TenantId",
                table: "AppPaperBatches",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperItemScores_BatchId",
                table: "AppPaperItemScores",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperItemScores_ExerciseId",
                table: "AppPaperItemScores",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperItemScores_PaperScriptId",
                table: "AppPaperItemScores",
                column: "PaperScriptId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperItemScores_ScriptId",
                table: "AppPaperItemScores",
                column: "ScriptId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperItemScores_TenantId",
                table: "AppPaperItemScores",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperScripts_BatchId",
                table: "AppPaperScripts",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperScripts_PaperBatchId",
                table: "AppPaperScripts",
                column: "PaperBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperScripts_StudentId",
                table: "AppPaperScripts",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPaperScripts_TenantId",
                table: "AppPaperScripts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppReviewPackageItems_KnowledgePointId",
                table: "AppReviewPackageItems",
                column: "KnowledgePointId");

            migrationBuilder.CreateIndex(
                name: "IX_AppReviewPackageItems_PackageId",
                table: "AppReviewPackageItems",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_AppReviewPackageItems_ReviewPackageId",
                table: "AppReviewPackageItems",
                column: "ReviewPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_AppReviewPackageItems_TenantId",
                table: "AppReviewPackageItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppReviewPackages_StudentId_UpcomingCourseId",
                table: "AppReviewPackages",
                columns: new[] { "StudentId", "UpcomingCourseId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppReviewPackages_TenantId",
                table: "AppReviewPackages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentKpMasteries_KnowledgePointId",
                table: "AppStudentKpMasteries",
                column: "KnowledgePointId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentKpMasteries_StudentId_KnowledgePointId",
                table: "AppStudentKpMasteries",
                columns: new[] { "StudentId", "KnowledgePointId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentKpMasteries_TenantId",
                table: "AppStudentKpMasteries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentNotifications_CreationTime",
                table: "AppStudentNotifications",
                column: "CreationTime");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentNotifications_StudentId_IsRead",
                table: "AppStudentNotifications",
                columns: new[] { "StudentId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentNotifications_TenantId",
                table: "AppStudentNotifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingInsights_CourseId_PaperBatchId",
                table: "AppTeachingInsights",
                columns: new[] { "CourseId", "PaperBatchId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingInsights_TenantId",
                table: "AppTeachingInsights",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAnalysisReports");

            migrationBuilder.DropTable(
                name: "AppAnswerRecords");

            migrationBuilder.DropTable(
                name: "AppCourseLinks");

            migrationBuilder.DropTable(
                name: "AppExerciseKnowledgePoints");

            migrationBuilder.DropTable(
                name: "AppKnowledgePoints");

            migrationBuilder.DropTable(
                name: "AppKpPrerequisites");

            migrationBuilder.DropTable(
                name: "AppMasteryThresholds");

            migrationBuilder.DropTable(
                name: "AppPaperItemScores");

            migrationBuilder.DropTable(
                name: "AppReviewPackageItems");

            migrationBuilder.DropTable(
                name: "AppStudentKpMasteries");

            migrationBuilder.DropTable(
                name: "AppStudentNotifications");

            migrationBuilder.DropTable(
                name: "AppTeachingInsights");

            migrationBuilder.DropTable(
                name: "AppPaperScripts");

            migrationBuilder.DropTable(
                name: "AppReviewPackages");

            migrationBuilder.DropTable(
                name: "AppPaperBatches");
        }
    }
}
