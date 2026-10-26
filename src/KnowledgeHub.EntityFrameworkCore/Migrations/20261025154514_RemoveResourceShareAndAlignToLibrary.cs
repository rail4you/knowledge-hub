using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <inheritdoc />
    public partial class RemoveResourceShareAndAlignToLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 数据迁移：把旧的「资源共享」关系重新对应到教学资源库 ──
            //
            // 旧模型：AppResourceShares 记录「资源 → 目标租户」的共享关系。
            // 新模型：共享关系由 AppResources.LibraryId 指向教学资源库承载，
            //         跨校可见性由「院校共建库」提供（主学校 + 关联学校）。
            //
            // 迁移策略（按「共享来源 → 目标」租户对分组）：
            //   1. 每个租户对新建一个「历史共享资源库」——院校共建、不限专业；
            //   2. 写入主学校(Owner) + 关联学校(Partner)两条成员记录；
            //   3. 把此前已共享、尚未挂到任何库的资源挂到该库上；
            //   4. 删除旧的共享记录表。
            //
            // 刻意不复用「限定专业」的老库：迁移过来的资源专业可能与该库主建专业不一致，
            // 挂进去会破坏「库内内容专业须与库一致」的约束。

            migrationBuilder.Sql(@"
-- 1) 为每个「来源 → 目标」租户对生成承载库 Id
CREATE TEMP TABLE ""_ShareLibraryMap"" AS
SELECT d.""SourceTenantId"", d.""TargetTenantId"", gen_random_uuid() AS ""LibraryId""
FROM (
    SELECT DISTINCT ""SourceTenantId"", ""TargetTenantId"" FROM ""AppResourceShares""
) d;

-- 2) 建立院校共建库（主学校 = 共享来源方，不限专业）
INSERT INTO ""AppResourceLibraries""
    (""Id"", ""TenantId"", ""Name"", ""Description"", ""OwnerTenantId"", ""Status"",
     ""IsPublic"", ""SortOrder"", ""ExtraProperties"", ""ConcurrencyStamp"",
     ""CreationTime"", ""IsDeleted"", ""Visibility"", ""Principal"")
SELECT m.""LibraryId"",
       m.""SourceTenantId"",
       '历史共享资源库',
       '由旧「资源共享」记录迁移而来',
       m.""SourceTenantId"",
       1, false, 0, '{}', md5(random()::text), now(), false, 2, NULL
FROM ""_ShareLibraryMap"" m;

-- 3) 成员：主学校(Owner=0)
INSERT INTO ""AppResourceLibraryMembers""
    (""Id"", ""LibraryId"", ""MemberTenantId"", ""MemberTenantName"", ""Role"", ""JoinedTime"", ""TenantId"")
SELECT gen_random_uuid(), m.""LibraryId"", m.""SourceTenantId"", t.""Name"", 0, now(), m.""SourceTenantId""
FROM ""_ShareLibraryMap"" m
JOIN ""AbpTenants"" t ON t.""Id"" = m.""SourceTenantId"";

-- 4) 成员：关联学校(Partner=1)
INSERT INTO ""AppResourceLibraryMembers""
    (""Id"", ""LibraryId"", ""MemberTenantId"", ""MemberTenantName"", ""Role"", ""JoinedTime"", ""TenantId"")
SELECT gen_random_uuid(), m.""LibraryId"", m.""TargetTenantId"", t.""Name"", 1, now(), m.""SourceTenantId""
FROM ""_ShareLibraryMap"" m
JOIN ""AbpTenants"" t ON t.""Id"" = m.""TargetTenantId"";

-- 5) 把此前已共享、尚未挂库的资源挂到承载库上（已挂库的保持原状）
UPDATE ""AppResources"" r
SET ""LibraryId"" = m.""LibraryId""
FROM ""_ShareLibraryMap"" m
JOIN ""AppResourceShares"" s
  ON s.""SourceTenantId"" = m.""SourceTenantId""
 AND s.""TargetTenantId"" = m.""TargetTenantId""
WHERE s.""ResourceId"" = r.""Id""
  AND r.""LibraryId"" IS NULL;

DROP TABLE ""_ShareLibraryMap"";
");

            migrationBuilder.DropTable(
                name: "AppResourceShares");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppResourceShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SharedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetTenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppResourceShares", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceShares_ResourceId",
                table: "AppResourceShares",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceShares_ResourceId_TargetTenantId",
                table: "AppResourceShares",
                columns: new[] { "ResourceId", "TargetTenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppResourceShares_TargetTenantId",
                table: "AppResourceShares",
                column: "TargetTenantId");
        }
    }
}
