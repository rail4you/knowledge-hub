using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppCourses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Major = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Semester = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Credits = table.Column<int>(type: "integer", nullable: true),
                    SemesterHours = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppCourses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppKnowledgeMastery",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    PracticeCount = table.Column<int>(type: "integer", nullable: false),
                    CorrectCount = table.Column<int>(type: "integer", nullable: false),
                    LastPracticeAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppKnowledgeMastery", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppLearningProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Progress = table.Column<decimal>(type: "numeric", nullable: false),
                    LastPosition = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TimeSpent = table.Column<TimeSpan>(type: "interval", nullable: false),
                    LastAccessAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppLearningProgress", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppStudentCourses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EnrolledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Progress = table.Column<decimal>(type: "numeric", nullable: false),
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
                    table.PrimaryKey("PK_AppStudentCourses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatThreads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SessionData = table.Column<string>(type: "text", maxLength: -1, nullable: true),
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
                    table.PrimaryKey("PK_ChatThreads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppChapters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppChapters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppChapters_AppChapters_ParentId",
                        column: x => x.ParentId,
                        principalTable: "AppChapters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppChapters_AppCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "AppCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ToolCalls = table.Column<string>(type: "text", maxLength: -1, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "ChatThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppKnowledgeResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ImportanceLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Difficulty = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Tags = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Embedding = table.Column<float[]>(type: "real[]", nullable: true),
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
                    table.PrimaryKey("PK_AppKnowledgeResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppKnowledgeResources_AppChapters_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "AppChapters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppKnowledgeResources_AppKnowledgeResources_ParentId",
                        column: x => x.ParentId,
                        principalTable: "AppKnowledgeResources",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppChapters_CourseId",
                table: "AppChapters",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppChapters_ParentId",
                table: "AppChapters",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppChapters_SortOrder",
                table: "AppChapters",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourses_Major",
                table: "AppCourses",
                column: "Major");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourses_Status",
                table: "AppCourses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourses_TeacherId",
                table: "AppCourses",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourses_TenantId",
                table: "AppCourses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourses_Title",
                table: "AppCourses",
                column: "Title");

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeMastery_KnowledgeResourceId",
                table: "AppKnowledgeMastery",
                column: "KnowledgeResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeMastery_StudentId",
                table: "AppKnowledgeMastery",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeMastery_StudentId_KnowledgeResourceId",
                table: "AppKnowledgeMastery",
                columns: new[] { "StudentId", "KnowledgeResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeMastery_TenantId",
                table: "AppKnowledgeMastery",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeResources_ChapterId",
                table: "AppKnowledgeResources",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeResources_CourseId",
                table: "AppKnowledgeResources",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeResources_ParentId",
                table: "AppKnowledgeResources",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLearningProgress_CourseId",
                table: "AppLearningProgress",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLearningProgress_StudentId",
                table: "AppLearningProgress",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppLearningProgress_StudentId_ResourceId",
                table: "AppLearningProgress",
                columns: new[] { "StudentId", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppLearningProgress_TenantId",
                table: "AppLearningProgress",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentCourses_Status",
                table: "AppStudentCourses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentCourses_StudentId_CourseId",
                table: "AppStudentCourses",
                columns: new[] { "StudentId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentCourses_TenantId",
                table: "AppStudentCourses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ThreadId",
                table: "ChatMessages",
                column: "ThreadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppKnowledgeMastery");

            migrationBuilder.DropTable(
                name: "AppKnowledgeResources");

            migrationBuilder.DropTable(
                name: "AppLearningProgress");

            migrationBuilder.DropTable(
                name: "AppStudentCourses");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "AppChapters");

            migrationBuilder.DropTable(
                name: "ChatThreads");

            migrationBuilder.DropTable(
                name: "AppCourses");
        }
    }
}
