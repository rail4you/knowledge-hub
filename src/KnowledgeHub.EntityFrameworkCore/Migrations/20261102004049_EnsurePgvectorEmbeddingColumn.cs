using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <summary>
    /// 补建知识点语义向量列（幂等）。
    ///
    /// <para>
    /// 背景：<c>20261019135534_AddPgvectorKnowledgePointEmbeddings</c> 已经跑过，
    /// 但当时线上 PostgreSQL 镜像没有 pgvector（postgres:16-alpine 自带不含），
    /// 迁移里的 <c>CREATE EXTENSION</c> 失败并被静默跳过 → <c>EmbeddingVector</c> 列与 HNSW 索引一直没建，
    /// 语义检索只能回退进程内余弦。
    /// </para>
    ///
    /// <para>
    /// 部署侧已改用自带 pgvector 0.8.0 的自建镜像（etc/docker/Dockerfile.postgres-pgvector），
    /// 本迁移负责把「扩展 + 列 + 回填 + 索引」补齐；逻辑与原迁移同样带异常兜底，
    /// 在仍没有 vector 类型的环境里会打 NOTICE 并跳过，不会让升级失败。
    /// </para>
    /// </summary>
    public partial class EnsurePgvectorEmbeddingColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) 尝试安装扩展（无权限 / 镜像不含扩展时静默跳过）
            migrationBuilder.Sql(@"
DO $$
BEGIN
    EXECUTE 'CREATE EXTENSION IF NOT EXISTS vector';
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE '[KnowledgeHub] 未能安装 pgvector 扩展（可忽略，将回退进程内余弦）：%', SQLERRM;
END $$;");

            // 2) 列 + 回填 + HNSW 索引（仅在扩展可用时执行，全部幂等）
            migrationBuilder.Sql(@"
DO $$
DECLARE
    has_type boolean;
BEGIN
    SELECT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'vector') INTO has_type;
    IF NOT has_type THEN
        RAISE NOTICE '[KnowledgeHub] 未检测到 vector 类型，跳过知识点向量列与索引';
        RETURN;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'AppKnowledgePoints' AND column_name = 'EmbeddingVector')
    THEN
        EXECUTE 'ALTER TABLE ""AppKnowledgePoints"" ADD COLUMN ""EmbeddingVector"" vector(1024)';
        RAISE NOTICE '[KnowledgeHub] 已补建 AppKnowledgePoints.EmbeddingVector (vector(1024))';
    END IF;

    -- 从既有 real[] 回填（仅 1024 维，避免维度不匹配导致整个迁移失败）
    BEGIN
        EXECUTE '
            UPDATE ""AppKnowledgePoints""
               SET ""EmbeddingVector"" = CAST(""Embedding"" AS vector)
             WHERE ""EmbeddingVector"" IS NULL
               AND ""Embedding"" IS NOT NULL
               AND array_length(""Embedding"", 1) = 1024';
    EXCEPTION WHEN OTHERS THEN
        RAISE NOTICE '[KnowledgeHub] 回填知识点向量失败（可忽略，后续可用「重建语义向量」）：%', SQLERRM;
    END;

    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_AppKnowledgePoints_EmbeddingVector_Hnsw') THEN
        BEGIN
            EXECUTE 'CREATE INDEX ""IX_AppKnowledgePoints_EmbeddingVector_Hnsw""
                     ON ""AppKnowledgePoints"" USING hnsw (""EmbeddingVector"" vector_cosine_ops)';
            RAISE NOTICE '[KnowledgeHub] 已创建 HNSW 向量索引';
        EXCEPTION WHEN OTHERS THEN
            RAISE NOTICE '[KnowledgeHub] 创建 HNSW 索引失败（可忽略，将退化为顺序扫描）：%', SQLERRM;
        END;
    END IF;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚不删列/索引：向量数据是可重建的派生数据，保留更安全（与上游迁移一致）。
        }
    }
}
