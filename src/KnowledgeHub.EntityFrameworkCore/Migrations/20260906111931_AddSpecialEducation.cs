using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecialEducation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IepPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProfileJson = table.Column<string>(type: "text", nullable: false),
                    LongTermGoalsJson = table.Column<string>(type: "text", nullable: false),
                    ShortTermGoalsJson = table.Column<string>(type: "text", nullable: false),
                    StrategiesJson = table.Column<string>(type: "text", nullable: false),
                    EvaluationJson = table.Column<string>(type: "text", nullable: false),
                    HomeSchoolJson = table.Column<string>(type: "text", nullable: false),
                    LegalBasis = table.Column<string>(type: "text", nullable: false),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    SourceInputJson = table.Column<string>(type: "text", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ParentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewComment = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_IepPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpecialEduResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeachingDesignId = table.Column<Guid>(type: "uuid", nullable: true),
                    IepPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Modality = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    SourceInputJson = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_SpecialEduResources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpecialTeachingDesigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Subject = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Grade = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Duration = table.Column<int>(type: "integer", nullable: false),
                    ObjectivesJson = table.Column<string>(type: "text", nullable: false),
                    KeyPointsJson = table.Column<string>(type: "text", nullable: false),
                    DifficultiesJson = table.Column<string>(type: "text", nullable: false),
                    SectionsJson = table.Column<string>(type: "text", nullable: false),
                    MethodsJson = table.Column<string>(type: "text", nullable: false),
                    ResourcesJson = table.Column<string>(type: "text", nullable: false),
                    AssessmentJson = table.Column<string>(type: "text", nullable: false),
                    HomeworkJson = table.Column<string>(type: "text", nullable: false),
                    BoardDesignJson = table.Column<string>(type: "text", nullable: false),
                    SlidesOutlineJson = table.Column<string>(type: "text", nullable: false),
                    ActivitiesJson = table.Column<string>(type: "text", nullable: false),
                    AssessmentToolsJson = table.Column<string>(type: "text", nullable: false),
                    StandardBasis = table.Column<string>(type: "text", nullable: false),
                    RawJson = table.Column<string>(type: "text", nullable: false),
                    SourceInputJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReviewComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_SpecialTeachingDesigns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IepPlans_ParentVersionId",
                table: "IepPlans",
                column: "ParentVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_IepPlans_TenantId_StudentUserId",
                table: "IepPlans",
                columns: new[] { "TenantId", "StudentUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SpecialEduResources_IepPlanId",
                table: "SpecialEduResources",
                column: "IepPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialEduResources_TeachingDesignId",
                table: "SpecialEduResources",
                column: "TeachingDesignId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialEduResources_TenantId_Category_Modality",
                table: "SpecialEduResources",
                columns: new[] { "TenantId", "Category", "Modality" });

            migrationBuilder.CreateIndex(
                name: "IX_SpecialTeachingDesigns_CourseId",
                table: "SpecialTeachingDesigns",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecialTeachingDesigns_TenantId_Category",
                table: "SpecialTeachingDesigns",
                columns: new[] { "TenantId", "Category" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IepPlans");

            migrationBuilder.DropTable(
                name: "SpecialEduResources");

            migrationBuilder.DropTable(
                name: "SpecialTeachingDesigns");
        }
    }
}
