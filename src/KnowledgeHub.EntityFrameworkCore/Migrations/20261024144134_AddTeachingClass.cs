using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddTeachingClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClassId",
                table: "AppStudentCourses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppTeachingClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MajorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: true),
                    GradeYear = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
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
                    table.PrimaryKey("PK_AppTeachingClasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppTeachingClasses_AppCourses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "AppCourses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AppTeachingClasses_AppMajors_MajorId",
                        column: x => x.MajorId,
                        principalTable: "AppMajors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AppTeachingClassMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    StudentNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    JoinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Source = table.Column<byte>(type: "smallint", nullable: false),
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
                    table.PrimaryKey("PK_AppTeachingClassMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppTeachingClassMembers_AppTeachingClasses_ClassId",
                        column: x => x.ClassId,
                        principalTable: "AppTeachingClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppStudentCourses_ClassId",
                table: "AppStudentCourses",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingClasses_CourseId",
                table: "AppTeachingClasses",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingClasses_MajorId",
                table: "AppTeachingClasses",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingClasses_Name",
                table: "AppTeachingClasses",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingClasses_Status",
                table: "AppTeachingClasses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingClasses_TeacherId",
                table: "AppTeachingClasses",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingClasses_TenantId",
                table: "AppTeachingClasses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingClassMembers_ClassId",
                table: "AppTeachingClassMembers",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingClassMembers_ClassId_StudentId",
                table: "AppTeachingClassMembers",
                columns: new[] { "ClassId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingClassMembers_StudentId",
                table: "AppTeachingClassMembers",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppTeachingClassMembers_TenantId",
                table: "AppTeachingClassMembers",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppTeachingClassMembers");

            migrationBuilder.DropTable(
                name: "AppTeachingClasses");

            migrationBuilder.DropIndex(
                name: "IX_AppStudentCourses_ClassId",
                table: "AppStudentCourses");

            migrationBuilder.DropColumn(
                name: "ClassId",
                table: "AppStudentCourses");
        }
    }
}
