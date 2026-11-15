using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddWhiteboardChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppWhiteboardChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    WhiteboardId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SenderColor = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Kind = table.Column<byte>(type: "smallint", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppWhiteboardChatMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppWhiteboardChatMessages_WhiteboardId_CreationTime",
                table: "AppWhiteboardChatMessages",
                columns: new[] { "WhiteboardId", "CreationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppWhiteboardChatMessages");
        }
    }
}
