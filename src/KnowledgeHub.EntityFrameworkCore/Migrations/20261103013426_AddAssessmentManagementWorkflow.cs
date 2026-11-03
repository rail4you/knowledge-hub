using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddAssessmentManagementWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssessmentTarget",
                table: "AppDoubleHighProjects",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReporterName",
                table: "AppDoubleHighProjects",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "AppDoubleHighProjects",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateKey",
                table: "AppDoubleHighProjects",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextValue",
                table: "AppDoubleHighIndicatorValues",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Basis",
                table: "AppDoubleHighIndicators",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataSourceName",
                table: "AppDoubleHighIndicators",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IndicatorType",
                table: "AppDoubleHighIndicators",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RequiredEvidences",
                table: "AppDoubleHighIndicators",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScoringRule",
                table: "AppDoubleHighIndicators",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceName",
                table: "AppDoubleHighEvidences",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "AppDoubleHighEvidences",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "AppDoubleHighEvidences",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "StoredPath",
                table: "AppDoubleHighEvidences",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            // 历史指标回填填报类型：
            //   DataSourceType = 0（手工填报）→ Number（填写数值，值 1）
            //   其余（系统统计）→ Auto（系统取数，值 0，列默认值即为此）
            // 这样历史项目的「自动采集指标」在新流程里仍然由平台取数，不会退化成手工填报。
            migrationBuilder.Sql(@"
UPDATE ""AppDoubleHighIndicators""
SET ""IndicatorType"" = 1
WHERE ""DataSourceType"" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssessmentTarget",
                table: "AppDoubleHighProjects");

            migrationBuilder.DropColumn(
                name: "ReporterName",
                table: "AppDoubleHighProjects");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "AppDoubleHighProjects");

            migrationBuilder.DropColumn(
                name: "TemplateKey",
                table: "AppDoubleHighProjects");

            migrationBuilder.DropColumn(
                name: "TextValue",
                table: "AppDoubleHighIndicatorValues");

            migrationBuilder.DropColumn(
                name: "Basis",
                table: "AppDoubleHighIndicators");

            migrationBuilder.DropColumn(
                name: "DataSourceName",
                table: "AppDoubleHighIndicators");

            migrationBuilder.DropColumn(
                name: "IndicatorType",
                table: "AppDoubleHighIndicators");

            migrationBuilder.DropColumn(
                name: "RequiredEvidences",
                table: "AppDoubleHighIndicators");

            migrationBuilder.DropColumn(
                name: "ScoringRule",
                table: "AppDoubleHighIndicators");

            migrationBuilder.DropColumn(
                name: "EvidenceName",
                table: "AppDoubleHighEvidences");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "AppDoubleHighEvidences");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "AppDoubleHighEvidences");

            migrationBuilder.DropColumn(
                name: "StoredPath",
                table: "AppDoubleHighEvidences");
        }
    }
}
