using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddRecruitmentLiveParticipants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppRecruitmentLiveParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    LiveId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppRecruitmentLiveParticipants", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppRecruitmentLiveParticipants_LiveId",
                table: "AppRecruitmentLiveParticipants",
                column: "LiveId");

            migrationBuilder.CreateIndex(
                name: "IX_AppRecruitmentLiveParticipants_TenantId",
                table: "AppRecruitmentLiveParticipants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppRecruitmentLiveParticipants_UserId",
                table: "AppRecruitmentLiveParticipants",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppRecruitmentLiveParticipants");
        }
    }
}
