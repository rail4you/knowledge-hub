using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddAiUsageRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiUsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Roles = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FeatureGroup = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Feature = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    IsEstimated = table.Column<bool>(type: "boolean", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "numeric", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", maxLength: -1, nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_TenantId_CreationTime",
                table: "AiUsageRecords",
                columns: new[] { "TenantId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_TenantId_UserId_FeatureGroup_CreationTime",
                table: "AiUsageRecords",
                columns: new[] { "TenantId", "UserId", "FeatureGroup", "CreationTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiUsageRecords");
        }
    }
}
