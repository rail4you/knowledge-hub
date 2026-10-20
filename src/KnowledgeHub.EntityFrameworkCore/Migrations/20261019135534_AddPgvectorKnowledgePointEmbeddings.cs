using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KnowledgeHub.Migrations
{
    /// <summary>
    /// 知识点语义向量落地到 pgvector：
    /// 1) 尝试安装 vector 扩展（无权限 / 未安装时静默跳过，业务自动回退进程内余弦）；
    /// 2) 给 AppKnowledgePoints 增加 vector(1024) 列 EmbeddingVector（保留原 real[] Embedding 列做兜底）；
    /// 3) 从既有 real[] 回填（仅 1024 维）；
    /// 4) 建 HNSW 余弦索引，支撑 ANN 检索。
    ///
    /// 注意：列维度固定 1024（与 LearningAnalytics:VectorDimension / Qwen text-embedding-v4 一致）。
    /// 更换向量模型导致维度变化时，需要新建迁移调整该列维度并重建向量。
    /// </summary>
    public partial class AddPgvectorKnowledgePointEmbeddings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) 安装扩展（失败不影响迁移，仅提示）
            migrationBuilder.Sql(@"
DO $$
BEGIN
    EXECUTE 'CREATE EXTENSION IF NOT EXISTS vector';
EXCEPTION WHEN OTHERS THEN
    RAISE NOTICE '[KnowledgeHub] 未能安装 pgvector 扩展（可忽略，将回退进程内余弦）：%', SQLERRM;
END $$;");

            // 2) 列 + 回填 + 索引（仅在扩展可用时执行）
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
        EXCEPTION WHEN OTHERS THEN
            RAISE NOTICE '[KnowledgeHub] 创建 HNSW 索引失败（可忽略，将退化为顺序扫描）：%', SQLERRM;
        END;
    END IF;
END $$;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP INDEX IF EXISTS ""IX_AppKnowledgePoints_EmbeddingVector_Hnsw"";
ALTER TABLE ""AppKnowledgePoints"" DROP COLUMN IF EXISTS ""EmbeddingVector"";");
        }
    }
}
