using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateMigratedSharedLibraries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 修正：旧共享数据迁移时按「共享记录」逐条建库，导致同一个
            // （主学校, 关联学校）组合下建出了多个「历史共享资源库」。
            // 这里把它们合并成每个组合一个库：资源改挂到保留库，重复库及其成员删除。
            //
            // 说明：对全新部署的库（迁移脚本已修正）本迁移是空操作。
            migrationBuilder.Sql(@"
-- 1) 每个（主学校, 关联学校）组合保留一个库
CREATE TEMP TABLE ""_KeepLibrary"" AS
SELECT DISTINCT ON (l.""OwnerTenantId"", m.""MemberTenantId"")
       l.""Id"" AS ""KeepId"",
       l.""OwnerTenantId"",
       m.""MemberTenantId"" AS ""PartnerTenantId""
FROM ""AppResourceLibraries"" l
JOIN ""AppResourceLibraryMembers"" m
  ON m.""LibraryId"" = l.""Id"" AND m.""Role"" = 1
WHERE l.""Name"" = '历史共享资源库' AND NOT l.""IsDeleted""
ORDER BY l.""OwnerTenantId"", m.""MemberTenantId"", l.""CreationTime"", l.""Id"";

-- 2) 重复库清单
CREATE TEMP TABLE ""_DupLibrary"" AS
SELECT l.""Id"" AS ""DupId"", k.""KeepId""
FROM ""AppResourceLibraries"" l
JOIN ""AppResourceLibraryMembers"" m
  ON m.""LibraryId"" = l.""Id"" AND m.""Role"" = 1
JOIN ""_KeepLibrary"" k
  ON k.""OwnerTenantId"" = l.""OwnerTenantId""
 AND k.""PartnerTenantId"" = m.""MemberTenantId""
WHERE l.""Name"" = '历史共享资源库' AND NOT l.""IsDeleted"" AND l.""Id"" <> k.""KeepId"";

-- 3) 资源改挂到保留库
UPDATE ""AppResources"" r
SET ""LibraryId"" = d.""KeepId""
FROM ""_DupLibrary"" d
WHERE r.""LibraryId"" = d.""DupId"";

-- 4) 删除重复库的成员
DELETE FROM ""AppResourceLibraryMembers"" m
USING ""_DupLibrary"" d
WHERE m.""LibraryId"" = d.""DupId"";

-- 5) 删除重复库
DELETE FROM ""AppResourceLibraries"" l
USING ""_DupLibrary"" d
WHERE l.""Id"" = d.""DupId"";

DROP TABLE ""_DupLibrary"";
DROP TABLE ""_KeepLibrary"";
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
