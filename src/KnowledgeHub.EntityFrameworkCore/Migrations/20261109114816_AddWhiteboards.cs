using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddWhiteboards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppWhiteboardElements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    WhiteboardId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElementKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ElementType = table.Column<byte>(type: "smallint", nullable: false),
                    Json = table.Column<string>(type: "text", nullable: false),
                    LastEditorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastEditorName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
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
                    table.PrimaryKey("PK_AppWhiteboardElements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppWhiteboards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Topic = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatorName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    ViewX = table.Column<double>(type: "double precision", nullable: false),
                    ViewY = table.Column<double>(type: "double precision", nullable: false),
                    ViewScale = table.Column<double>(type: "double precision", nullable: false),
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
                    table.PrimaryKey("PK_AppWhiteboards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppWhiteboardMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    WhiteboardId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Role = table.Column<byte>(type: "smallint", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppWhiteboardMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppWhiteboardMembers_AppWhiteboards_WhiteboardId",
                        column: x => x.WhiteboardId,
                        principalTable: "AppWhiteboards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppWhiteboardElements_WhiteboardId_ElementKey",
                table: "AppWhiteboardElements",
                columns: new[] { "WhiteboardId", "ElementKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppWhiteboardMembers_UserId",
                table: "AppWhiteboardMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppWhiteboardMembers_WhiteboardId_UserId",
                table: "AppWhiteboardMembers",
                columns: new[] { "WhiteboardId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppWhiteboards_CreatorId",
                table: "AppWhiteboards",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_AppWhiteboards_TenantId",
                table: "AppWhiteboards",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppWhiteboards_TenantId_LastModificationTime",
                table: "AppWhiteboards",
                columns: new[] { "TenantId", "LastModificationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppWhiteboardElements");

            migrationBuilder.DropTable(
                name: "AppWhiteboardMembers");

            migrationBuilder.DropTable(
                name: "AppWhiteboards");
        }
    }
}
