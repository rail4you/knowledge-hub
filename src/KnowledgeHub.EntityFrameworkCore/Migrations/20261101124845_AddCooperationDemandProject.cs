using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCooperationDemandProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppCooperationDemands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnterpriseProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<byte>(type: "smallint", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Majors = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Industries = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Region = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Background = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    EnterpriseInput = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SchoolConditions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Cycle = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Scale = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    BudgetNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeliveryAcceptance = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IpTerms = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ContactName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Scope = table.Column<byte>(type: "smallint", nullable: false),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_AppCooperationDemands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppCooperationProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DemandId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponseId = table.Column<Guid>(type: "uuid", nullable: true),
                    EnterpriseProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Stage = table.Column<byte>(type: "smallint", nullable: false),
                    AgreementRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EndAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    AcceptanceNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AppCooperationProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppCooperationResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DemandId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchoolTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponderUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponderName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppCooperationResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppCooperationResponses_AppCooperationDemands_DemandId",
                        column: x => x.DemandId,
                        principalTable: "AppCooperationDemands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppProjectMilestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EvidenceRef = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppProjectMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppProjectMilestones_AppCooperationProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "AppCooperationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppCooperationDemands_EnterpriseProfileId",
                table: "AppCooperationDemands",
                column: "EnterpriseProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCooperationDemands_Status_Type",
                table: "AppCooperationDemands",
                columns: new[] { "Status", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_AppCooperationDemands_TenantId",
                table: "AppCooperationDemands",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCooperationProjects_EnterpriseProfileId",
                table: "AppCooperationProjects",
                column: "EnterpriseProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCooperationProjects_SchoolTenantId_Stage",
                table: "AppCooperationProjects",
                columns: new[] { "SchoolTenantId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_AppCooperationResponses_DemandId_SchoolTenantId",
                table: "AppCooperationResponses",
                columns: new[] { "DemandId", "SchoolTenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCooperationResponses_SchoolTenantId",
                table: "AppCooperationResponses",
                column: "SchoolTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppProjectMilestones_ProjectId",
                table: "AppProjectMilestones",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppCooperationResponses");

            migrationBuilder.DropTable(
                name: "AppProjectMilestones");

            migrationBuilder.DropTable(
                name: "AppCooperationDemands");

            migrationBuilder.DropTable(
                name: "AppCooperationProjects");
        }
    }
}
