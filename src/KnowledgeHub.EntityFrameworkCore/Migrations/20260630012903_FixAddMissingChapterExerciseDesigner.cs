using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class FixAddMissingChapterExerciseDesigner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppChapterExercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
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
                    table.PrimaryKey("PK_AppChapterExercises", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppChapterExercises_ChapterId",
                table: "AppChapterExercises",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_AppChapterExercises_ExerciseId",
                table: "AppChapterExercises",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppChapterExercises_TenantId",
                table: "AppChapterExercises",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppChapterExercises_ChapterId_ExerciseId",
                table: "AppChapterExercises",
                columns: new[] { "ChapterId", "ExerciseId" },
                unique: true);

            // 数据回填：把现有 Exercise.ChapterId（已发布/未删除）回填到关联表
            migrationBuilder.Sql(@"
                INSERT INTO ""AppChapterExercises""
                  (""Id"", ""TenantId"", ""ChapterId"", ""ExerciseId"", ""SortOrder"",
                   ""ExtraProperties"", ""ConcurrencyStamp"", ""CreationTime"",
                   ""IsDeleted"")
                SELECT
                  gen_random_uuid(),
                  e.""TenantId"",
                  e.""ChapterId"",
                  e.""Id"",
                  0,
                  '{}'::text,
                  gen_random_uuid()::text,
                  NOW(),
                  false
                FROM ""AppExercises"" e
                WHERE e.""ChapterId"" IS NOT NULL
                  AND e.""IsDeleted"" = false
                ON CONFLICT (""ChapterId"", ""ExerciseId"") DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AppChapterExercises");
        }
    }
}
