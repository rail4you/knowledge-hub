using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class BackfillSpecialEduVersionNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 版本号列是后加的，老数据为 0，回填为 1（v1 = AI 生成版）
            migrationBuilder.Sql(@"UPDATE ""SpecialTeachingDesigns"" SET ""VersionNumber"" = 1 WHERE ""VersionNumber"" = 0;");
            migrationBuilder.Sql(@"UPDATE ""IepPlans"" SET ""VersionNumber"" = 1 WHERE ""VersionNumber"" = 0;");
            migrationBuilder.Sql(@"UPDATE ""SpecialEduResources"" SET ""VersionNumber"" = 1 WHERE ""VersionNumber"" = 0;");
            // v0 快照：已有 v1+ 同胞的行直接删除（被取代），其余重编号为 v1
            migrationBuilder.Sql(@"
DELETE FROM ""SpecialEduContentVersions"" v0
USING ""SpecialEduContentVersions"" v1
WHERE v0.""VersionNumber"" = 0
  AND v1.""ContentType"" = v0.""ContentType""
  AND v1.""EntityId"" = v0.""EntityId""
  AND v1.""VersionNumber"" >= 1;");
            migrationBuilder.Sql(@"UPDATE ""SpecialEduContentVersions"" SET ""VersionNumber"" = 1 WHERE ""VersionNumber"" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
