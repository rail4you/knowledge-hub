using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class AddSimulationContentHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "KnowledgeHubPracticumSimulations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeHubPracticumSimulations_ContentHash",
                table: "KnowledgeHubPracticumSimulations",
                column: "ContentHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KnowledgeHubPracticumSimulations_ContentHash",
                table: "KnowledgeHubPracticumSimulations");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "KnowledgeHubPracticumSimulations");
        }
    }
}
