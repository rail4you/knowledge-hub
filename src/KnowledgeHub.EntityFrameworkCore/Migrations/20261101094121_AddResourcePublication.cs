using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddResourcePublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppResourcePublications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LibraryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<byte>(type: "smallint", nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentTenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<byte>(type: "smallint", nullable: false),
                    SubmitterType = table.Column<byte>(type: "smallint", nullable: false),
                    ReviewTicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    PolicyJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    RejectReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_AppResourcePublications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppResourcePublications_ContentTenantId",
                table: "AppResourcePublications",
                column: "ContentTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourcePublications_LibraryId_ContentType_ContentId",
                table: "AppResourcePublications",
                columns: new[] { "LibraryId", "ContentType", "ContentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppResourcePublications_LibraryId_Status",
                table: "AppResourcePublications",
                columns: new[] { "LibraryId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AppResourcePublications_ReviewTicketId",
                table: "AppResourcePublications",
                column: "ReviewTicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppResourcePublications");
        }
    }
}
