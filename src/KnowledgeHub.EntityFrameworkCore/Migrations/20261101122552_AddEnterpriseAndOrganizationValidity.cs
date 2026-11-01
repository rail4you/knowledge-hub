using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddEnterpriseAndOrganizationValidity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReadOnlyAfterExpiry",
                table: "AppSchools",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidFrom",
                table: "AppSchools",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidTo",
                table: "AppSchools",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppEnterpriseMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnterpriseProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Role = table.Column<byte>(type: "smallint", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppEnterpriseMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppEnterpriseProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UnifiedSocialCreditCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Industry = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ContactName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CertificationStatus = table.Column<byte>(type: "smallint", nullable: false),
                    CertifierUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CertifiedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ValidTo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
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
                    table.PrimaryKey("PK_AppEnterpriseProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSchools_ValidTo",
                table: "AppSchools",
                column: "ValidTo");

            migrationBuilder.CreateIndex(
                name: "IX_AppEnterpriseMembers_EnterpriseProfileId_UserId",
                table: "AppEnterpriseMembers",
                columns: new[] { "EnterpriseProfileId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppEnterpriseMembers_TenantId",
                table: "AppEnterpriseMembers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEnterpriseMembers_UserId",
                table: "AppEnterpriseMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEnterpriseProfiles_OwnerUserId",
                table: "AppEnterpriseProfiles",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppEnterpriseProfiles_TenantId_CertificationStatus",
                table: "AppEnterpriseProfiles",
                columns: new[] { "TenantId", "CertificationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_AppEnterpriseProfiles_ValidTo",
                table: "AppEnterpriseProfiles",
                column: "ValidTo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppEnterpriseMembers");

            migrationBuilder.DropTable(
                name: "AppEnterpriseProfiles");

            migrationBuilder.DropIndex(
                name: "IX_AppSchools_ValidTo",
                table: "AppSchools");

            migrationBuilder.DropColumn(
                name: "ReadOnlyAfterExpiry",
                table: "AppSchools");

            migrationBuilder.DropColumn(
                name: "ValidFrom",
                table: "AppSchools");

            migrationBuilder.DropColumn(
                name: "ValidTo",
                table: "AppSchools");
        }
    }
}
