using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCompetency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppCompetencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    MajorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppCompetencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppCompetencyKnowledgeLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompetencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppCompetencyKnowledgeLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppCourseCompetencySupports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompetencyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppCourseCompetencySupports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppCompetencies_MajorId",
                table: "AppCompetencies",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCompetencies_MajorId_Code",
                table: "AppCompetencies",
                columns: new[] { "MajorId", "Code" },
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppCompetencies_ParentId",
                table: "AppCompetencies",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCompetencies_TenantId",
                table: "AppCompetencies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCompetencyKnowledgeLinks_CompetencyId_KnowledgeResourceId",
                table: "AppCompetencyKnowledgeLinks",
                columns: new[] { "CompetencyId", "KnowledgeResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCompetencyKnowledgeLinks_KnowledgeResourceId",
                table: "AppCompetencyKnowledgeLinks",
                column: "KnowledgeResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCompetencyKnowledgeLinks_TenantId",
                table: "AppCompetencyKnowledgeLinks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseCompetencySupports_CompetencyId",
                table: "AppCourseCompetencySupports",
                column: "CompetencyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseCompetencySupports_CourseId_CompetencyId",
                table: "AppCourseCompetencySupports",
                columns: new[] { "CourseId", "CompetencyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseCompetencySupports_TenantId",
                table: "AppCourseCompetencySupports",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppCompetencies");

            migrationBuilder.DropTable(
                name: "AppCompetencyKnowledgeLinks");

            migrationBuilder.DropTable(
                name: "AppCourseCompetencySupports");
        }
    }
}
