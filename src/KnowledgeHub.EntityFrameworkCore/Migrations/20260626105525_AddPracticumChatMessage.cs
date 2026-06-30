using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddPracticumChatMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgentName",
                table: "AppPracticumProjects",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentPrompt",
                table: "AppPracticumProjects",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppPracticumChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: true),
                    SenderType = table.Column<int>(type: "integer", nullable: false),
                    SenderName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    MessageType = table.Column<int>(type: "integer", nullable: false),
                    AttachmentUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AttachmentName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AttachmentSize = table.Column<long>(type: "bigint", nullable: true),
                    IsAgentReply = table.Column<bool>(type: "boolean", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppPracticumChatMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumChatMessages_CreationTime",
                table: "AppPracticumChatMessages",
                column: "CreationTime");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumChatMessages_ProjectId",
                table: "AppPracticumChatMessages",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumChatMessages_SenderId",
                table: "AppPracticumChatMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumChatMessages_SenderType",
                table: "AppPracticumChatMessages",
                column: "SenderType");

            migrationBuilder.CreateIndex(
                name: "IX_AppPracticumChatMessages_TenantId",
                table: "AppPracticumChatMessages",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppPracticumChatMessages");

            migrationBuilder.DropColumn(
                name: "AgentName",
                table: "AppPracticumProjects");

            migrationBuilder.DropColumn(
                name: "AgentPrompt",
                table: "AppPracticumProjects");
        }
    }
}
