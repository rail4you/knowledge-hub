using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddMajorModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppCourses_Major",
                table: "AppCourses");

            migrationBuilder.DropColumn(
                name: "Major",
                table: "AppCourses");

            migrationBuilder.AddColumn<Guid>(
                name: "MajorId",
                table: "AppResources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MajorId",
                table: "AppCourses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppMajors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_AppMajors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppResources_MajorId",
                table: "AppResources",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourses_MajorId",
                table: "AppCourses",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMajors_Name",
                table: "AppMajors",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_AppMajors_TenantId",
                table: "AppMajors",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppMajors_TenantId_Code",
                table: "AppMajors",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppCourses_AppMajors_MajorId",
                table: "AppCourses",
                column: "MajorId",
                principalTable: "AppMajors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AppResources_AppMajors_MajorId",
                table: "AppResources",
                column: "MajorId",
                principalTable: "AppMajors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppCourses_AppMajors_MajorId",
                table: "AppCourses");

            migrationBuilder.DropForeignKey(
                name: "FK_AppResources_AppMajors_MajorId",
                table: "AppResources");

            migrationBuilder.DropTable(
                name: "AppMajors");

            migrationBuilder.DropIndex(
                name: "IX_AppResources_MajorId",
                table: "AppResources");

            migrationBuilder.DropIndex(
                name: "IX_AppCourses_MajorId",
                table: "AppCourses");

            migrationBuilder.DropColumn(
                name: "MajorId",
                table: "AppResources");

            migrationBuilder.DropColumn(
                name: "MajorId",
                table: "AppCourses");

            migrationBuilder.AddColumn<string>(
                name: "Major",
                table: "AppCourses",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCourses_Major",
                table: "AppCourses",
                column: "Major");
        }
    }
}
