using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseTeachingProfileAndKnowledgeTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NodeType",
                table: "AppKnowledgeResources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SortPath",
                table: "AppKnowledgeResources",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "KnowledgeResourceId",
                table: "AppKnowledgePoints",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Evidence",
                table: "AppKnowledgeMastery",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "Mastery",
                table: "AppKnowledgeMastery",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AppKnowledgeMastery",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "AppCourseInfos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Background = table.Column<string>(type: "text", nullable: true),
                    Objectives = table.Column<string>(type: "text", nullable: true),
                    Target = table.Column<string>(type: "text", nullable: true),
                    Highlights = table.Column<string>(type: "text", nullable: true),
                    Introduction = table.Column<string>(type: "text", nullable: true),
                    Structure = table.Column<string>(type: "text", nullable: true),
                    GraduationRequirements = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_AppCourseInfos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppCourseTextbooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Author = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Publisher = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Edition = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Isbn = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CoverUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    AttachmentResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_AppCourseTextbooks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgeResources_CourseId_SortPath",
                table: "AppKnowledgeResources",
                columns: new[] { "CourseId", "SortPath" });

            migrationBuilder.CreateIndex(
                name: "IX_AppKnowledgePoints_KnowledgeResourceId",
                table: "AppKnowledgePoints",
                column: "KnowledgeResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseInfos_CourseId",
                table: "AppCourseInfos",
                column: "CourseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseInfos_TenantId",
                table: "AppCourseInfos",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseTextbooks_AttachmentResourceId",
                table: "AppCourseTextbooks",
                column: "AttachmentResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseTextbooks_CourseId",
                table: "AppCourseTextbooks",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseTextbooks_TenantId",
                table: "AppCourseTextbooks",
                column: "TenantId");

            // 存量知识点：按「有无子节点」回填 NodeType（0=单元 / 1=知识项）。
            // EF 的列默认值是 0（Unit），直接留着会让所有历史叶子节点显示成“可再挂子节点”的单元。
            migrationBuilder.Sql(
                "UPDATE \"AppKnowledgeResources\" k SET \"NodeType\" = CASE " +
                "WHEN EXISTS (SELECT 1 FROM \"AppKnowledgeResources\" c WHERE c.\"ParentId\" = k.\"Id\") " +
                "THEN 0 ELSE 1 END");

            // 存量掌握度行的 UpdatedAt 被 EF 兑底成 0001-01-01，前端会渲染成异常时间。
            migrationBuilder.Sql(
                "UPDATE \"AppKnowledgeMastery\" SET \"UpdatedAt\" = now() WHERE \"UpdatedAt\" < '2000-01-01'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppCourseInfos");

            migrationBuilder.DropTable(
                name: "AppCourseTextbooks");

            migrationBuilder.DropIndex(
                name: "IX_AppKnowledgeResources_CourseId_SortPath",
                table: "AppKnowledgeResources");

            migrationBuilder.DropIndex(
                name: "IX_AppKnowledgePoints_KnowledgeResourceId",
                table: "AppKnowledgePoints");

            migrationBuilder.DropColumn(
                name: "NodeType",
                table: "AppKnowledgeResources");

            migrationBuilder.DropColumn(
                name: "SortPath",
                table: "AppKnowledgeResources");

            migrationBuilder.DropColumn(
                name: "KnowledgeResourceId",
                table: "AppKnowledgePoints");

            migrationBuilder.DropColumn(
                name: "Evidence",
                table: "AppKnowledgeMastery");

            migrationBuilder.DropColumn(
                name: "Mastery",
                table: "AppKnowledgeMastery");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AppKnowledgeMastery");
        }
    }
}
