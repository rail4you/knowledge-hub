using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class RecreateResourcePageIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Re-create the KhResourcePageIndices table that was dropped by RemoveResourcePageIndex.
            // The entity is still in the codebase (DbSet<ResourcePageIndex> in KnowledgeHubDbContext)
            // but the snapshot doesn't include it after RemoveResourcePageIndex.
            // We create it manually here with the correct schema.
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS ""KhResourcePageIndices"" (
                    ""Id"" uuid NOT NULL,
                    ""ResourceId"" uuid NOT NULL,
                    ""ResourceVersionId"" uuid NOT NULL,
                    ""CreatedAt"" timestamp without time zone NOT NULL,
                    ""Model"" character varying(64),
                    ""NodeCount"" integer NOT NULL DEFAULT 0,
                    ""PageIndexJson"" jsonb NOT NULL DEFAULT '{}',
                    ""SourceFormat"" character varying(32),
                    ""TenantId"" uuid,
                    CONSTRAINT ""PK_KhResourcePageIndices"" PRIMARY KEY (""Id""),
                    CONSTRAINT ""FK_KhResourcePageIndices_AppResourceVersions_ResourceVersionId"" FOREIGN KEY (""ResourceVersionId"") REFERENCES ""AppResourceVersions"" (""Id"") ON DELETE CASCADE,
                    CONSTRAINT ""FK_KhResourcePageIndices_AppResources_ResourceId"" FOREIGN KEY (""ResourceId"") REFERENCES ""AppResources"" (""Id"") ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS ""IX_KhResourcePageIndices_ResourceId""
                    ON ""KhResourcePageIndices"" (""ResourceId"");

                CREATE UNIQUE INDEX IF NOT EXISTS ""IX_KhResourcePageIndices_ResourceVersionId""
                    ON ""KhResourcePageIndices"" (""ResourceVersionId"");
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "KhResourcePageIndices");
        }
    }
}
