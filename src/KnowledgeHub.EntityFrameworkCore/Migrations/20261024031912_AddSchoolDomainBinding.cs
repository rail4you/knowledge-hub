using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolDomainBinding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Domain",
                table: "AppSchools",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "AppSchools",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppSchools_Domain",
                table: "AppSchools",
                column: "Domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppSchools_Slug",
                table: "AppSchools",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppSchools_Domain",
                table: "AppSchools");

            migrationBuilder.DropIndex(
                name: "IX_AppSchools_Slug",
                table: "AppSchools");

            migrationBuilder.DropColumn(
                name: "Domain",
                table: "AppSchools");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "AppSchools");
        }
    }
}
