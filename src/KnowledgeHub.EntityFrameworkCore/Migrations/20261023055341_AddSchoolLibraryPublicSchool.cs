using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolLibraryPublicSchool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LibraryId",
                table: "AppResources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LibraryId",
                table: "AppExercises",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MajorId",
                table: "AppExercises",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LibraryId",
                table: "AppCourses",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppCourseShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppCourseShares", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppResourceLibraries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CoverUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PrimaryMajorId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppResourceLibraries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSchools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LogoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    BannerUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Introduction = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_AppSchools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppResourceLibraryMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberTenantName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Role = table.Column<byte>(type: "smallint", nullable: false),
                    JoinedTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppResourceLibraryMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppResourceLibraryMembers_AppResourceLibraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "AppResourceLibraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppResources_LibraryId",
                table: "AppResources",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExercises_LibraryId",
                table: "AppExercises",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppExercises_MajorId",
                table: "AppExercises",
                column: "MajorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourses_LibraryId",
                table: "AppCourses",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseShares_CourseId",
                table: "AppCourseShares",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseShares_CourseId_TargetTenantId",
                table: "AppCourseShares",
                columns: new[] { "CourseId", "TargetTenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseShares_SourceTenantId",
                table: "AppCourseShares",
                column: "SourceTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseShares_TargetTenantId",
                table: "AppCourseShares",
                column: "TargetTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCourseShares_TenantId",
                table: "AppCourseShares",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraries_OwnerTenantId",
                table: "AppResourceLibraries",
                column: "OwnerTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraries_PrimaryMajorId",
                table: "AppResourceLibraries",
                column: "PrimaryMajorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraries_Status",
                table: "AppResourceLibraries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraries_TenantId",
                table: "AppResourceLibraries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryMembers_LibraryId",
                table: "AppResourceLibraryMembers",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryMembers_LibraryId_MemberTenantId",
                table: "AppResourceLibraryMembers",
                columns: new[] { "LibraryId", "MemberTenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceLibraryMembers_MemberTenantId",
                table: "AppResourceLibraryMembers",
                column: "MemberTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSchools_IsPublic",
                table: "AppSchools",
                column: "IsPublic");

            migrationBuilder.CreateIndex(
                name: "IX_AppSchools_TenantId",
                table: "AppSchools",
                column: "TenantId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppCourses_AppResourceLibraries_LibraryId",
                table: "AppCourses",
                column: "LibraryId",
                principalTable: "AppResourceLibraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AppResources_AppResourceLibraries_LibraryId",
                table: "AppResources",
                column: "LibraryId",
                principalTable: "AppResourceLibraries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppCourses_AppResourceLibraries_LibraryId",
                table: "AppCourses");

            migrationBuilder.DropForeignKey(
                name: "FK_AppResources_AppResourceLibraries_LibraryId",
                table: "AppResources");

            migrationBuilder.DropTable(
                name: "AppCourseShares");

            migrationBuilder.DropTable(
                name: "AppResourceLibraryMembers");

            migrationBuilder.DropTable(
                name: "AppSchools");

            migrationBuilder.DropTable(
                name: "AppResourceLibraries");

            migrationBuilder.DropIndex(
                name: "IX_AppResources_LibraryId",
                table: "AppResources");

            migrationBuilder.DropIndex(
                name: "IX_AppExercises_LibraryId",
                table: "AppExercises");

            migrationBuilder.DropIndex(
                name: "IX_AppExercises_MajorId",
                table: "AppExercises");

            migrationBuilder.DropIndex(
                name: "IX_AppCourses_LibraryId",
                table: "AppCourses");

            migrationBuilder.DropColumn(
                name: "LibraryId",
                table: "AppResources");

            migrationBuilder.DropColumn(
                name: "LibraryId",
                table: "AppExercises");

            migrationBuilder.DropColumn(
                name: "MajorId",
                table: "AppExercises");

            migrationBuilder.DropColumn(
                name: "LibraryId",
                table: "AppCourses");
        }
    }
}
