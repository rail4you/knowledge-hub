using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewKernel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppReviewTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<byte>(type: "smallint", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<byte>(type: "smallint", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Stage = table.Column<byte>(type: "smallint", nullable: false),
                    RoundNo = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedByName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewerName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_AppReviewTickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppReviewActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<byte>(type: "smallint", nullable: false),
                    ActionType = table.Column<byte>(type: "smallint", nullable: false),
                    Decision = table.Column<byte>(type: "smallint", nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RoundNo = table.Column<int>(type: "integer", nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppReviewActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppReviewActions_AppReviewTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "AppReviewTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppReviewActions_TicketId",
                table: "AppReviewActions",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_AppReviewActions_TicketId_CreationTime",
                table: "AppReviewActions",
                columns: new[] { "TicketId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AppReviewTickets_LibraryId_Status",
                table: "AppReviewTickets",
                columns: new[] { "LibraryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppReviewTickets_Pending_Dedup",
                table: "AppReviewTickets",
                columns: new[] { "TargetType", "TargetId", "ActionType", "PayloadHash" },
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AppReviewTickets_Status_Stage",
                table: "AppReviewTickets",
                columns: new[] { "Status", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_AppReviewTickets_SubmittedBy",
                table: "AppReviewTickets",
                column: "SubmittedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AppReviewTickets_TargetType_TargetId_ActionType_Status",
                table: "AppReviewTickets",
                columns: new[] { "TargetType", "TargetId", "ActionType", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppReviewActions");

            migrationBuilder.DropTable(
                name: "AppReviewTickets");
        }
    }
}
