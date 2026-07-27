using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddRecruitmentLiveChatMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppRecruitmentLiveChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    LiveId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppRecruitmentLiveChatMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppRecruitmentLiveChatMessages_LiveId",
                table: "AppRecruitmentLiveChatMessages",
                column: "LiveId");

            migrationBuilder.CreateIndex(
                name: "IX_AppRecruitmentLiveChatMessages_SentAt",
                table: "AppRecruitmentLiveChatMessages",
                column: "SentAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppRecruitmentLiveChatMessages");
        }
    }
}
