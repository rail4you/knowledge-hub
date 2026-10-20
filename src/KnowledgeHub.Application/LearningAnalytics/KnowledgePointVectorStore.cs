using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>向量存储状态（pgvector 可用与否 + 维度 + 已入库数量）。</summary>
public record KnowledgeVectorState(
    string Engine,
    int? Dimension,
    bool Available,
    int EmbeddedCount,
    string? Message)
{
    public const string EnginePgvector = Dtos.KnowledgeVectorEngines.Pgvector;
    public const string EngineMemory = Dtos.KnowledgeVectorEngines.Memory;
}

/// <summary>
/// 知识点向量存储：
/// - pgvector 可用时，向量写入 <c>AppKnowledgePoints."EmbeddingVector" (vector(1024))</c>，
///   相似度检索走 HNSW 余弦索引（ANN），支撑知识点量级上万后的跨课程归一化；
/// - 扩展 / 列不可用时自动降级为「仅内存向量」（调用方回退进程内余弦），业务不中断。
///
/// 设计取舍：保留既有 <c>Embedding real[]</c> 列作为可移植副本，
/// pgvector 列只是检索加速通道（写入时双写），因此迁移可以安全回滚。
/// </summary>
public class KnowledgePointVectorStore : ITransientDependency
{
    public const string ColumnName = "EmbeddingVector";
    public const string IndexName = "IX_AppKnowledgePoints_EmbeddingVector_Hnsw";

    private static readonly TimeSpan ProbeTtl = TimeSpan.FromMinutes(10);
    private static readonly object ProbeSync = new();
    private static DateTime _probedAt = DateTime.MinValue;
    private static bool _available;
    private static int? _columnDimension;
    private static string? _unavailableReason;

    private readonly KnowledgeHubDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KnowledgePointVectorStore> _logger;

    public KnowledgePointVectorStore(
        KnowledgeHubDbContext dbContext,
        IConfiguration configuration,
        ILogger<KnowledgePointVectorStore> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>期望维度：必须与 vector 列维度一致（默认 1024）。</summary>
    public int ConfiguredDimension => _configuration.GetValue("LearningAnalytics:VectorDimension", 1024);

    /// <summary>探测 pgvector 可用性（结果缓存 10 分钟）。</summary>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (DateTime.UtcNow - _probedAt < ProbeTtl)
        {
            return _available;
        }

        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandText = @"
SELECT
    (SELECT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'vector')),
    (SELECT a.atttypmod
       FROM pg_attribute a
      WHERE a.attrelid = '""AppKnowledgePoints""'::regclass
        AND a.attname = @column
        AND NOT a.attisdropped)";

                var columnParam = command.CreateParameter();
                columnParam.ParameterName = "column";
                columnParam.Value = ColumnName;
                command.Parameters.Add(columnParam);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var hasType = false;
                int? dimension = null;
                if (await reader.ReadAsync(cancellationToken))
                {
                    hasType = !reader.IsDBNull(0) && reader.GetBoolean(0);
                    if (!reader.IsDBNull(1))
                    {
                        dimension = reader.GetInt32(1);
                    }
                }

                var configured = ConfiguredDimension;
                if (!hasType)
                {
                    SetProbe(false, null, "数据库未安装 pgvector 扩展（vector 类型不存在）");
                }
                else if (dimension == null)
                {
                    SetProbe(false, null, $"缺少 {ColumnName} 列，请执行迁移 AddPgvectorKnowledgePointEmbeddings");
                }
                else if (dimension != configured)
                {
                    SetProbe(false, dimension, $"向量列维度 {dimension} 与配置 {configured} 不一致，已停用 pgvector 检索");
                }
                else
                {
                    SetProbe(true, dimension, null);
                }
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch (Exception ex)
        {
            SetProbe(false, null, "pgvector 探测失败：" + ex.Message);
            _logger.LogWarning(ex, "pgvector 探测失败，知识点向量检索将回退进程内余弦");
        }

        return _available;
    }

    private static void SetProbe(bool available, int? dimension, string? reason)
    {
        lock (ProbeSync)
        {
            _probedAt = DateTime.UtcNow;
            _available = available;
            _columnDimension = dimension;
            _unavailableReason = reason;
        }
    }

    /// <summary>批量写入向量（双写 pgvector 列；未启用时静默跳过）。</summary>
    public async Task<int> UpsertAsync(
        IReadOnlyList<(Guid Id, float[] Vector)> items,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0 || !await IsAvailableAsync(cancellationToken))
        {
            return 0;
        }

        var dimension = ConfiguredDimension;
        var usable = items.Where(i => i.Vector is { Length: > 0 } && i.Vector.Length == dimension).ToList();
        if (usable.Count == 0)
        {
            return 0;
        }

        var written = 0;
        // 分片：避免单条 SQL 参数过大（1000 × 1024 浮点文本约 20MB）
        foreach (var chunk in usable.Chunk(200))
        {
            var connection = _dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandText = $@"
UPDATE ""AppKnowledgePoints"" AS kp
   SET ""{ColumnName}"" = src.vec
  FROM (
        SELECT unnest(@ids::uuid[]) AS id,
               unnest(@vecs::text[])::vector AS vec
       ) AS src
 WHERE kp.""Id"" = src.id
   AND kp.""TenantId"" IS NOT DISTINCT FROM @tenantId::uuid";

                command.Parameters.Add(new NpgsqlParameter("ids", chunk.Select(i => i.Id).ToArray()));
                command.Parameters.Add(new NpgsqlParameter("vecs", chunk.Select(i => ToPgVectorLiteral(i.Vector)).ToArray()));
                command.Parameters.Add(new NpgsqlParameter("tenantId", (object?)tenantId ?? DBNull.Value));

                written += await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "写入 pgvector 知识点向量失败，已跳过该批（业务不受影响）");
                SetProbe(false, _columnDimension, "写入 pgvector 失败：" + ex.Message);
                break;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        return written;
    }

    /// <summary>ANN 检索（余弦相似度 = 1 - 余弦距离）。不可用时返回空列表，调用方回退进程内计算。</summary>
    public async Task<List<(Guid Id, double Similarity)>> SearchAsync(
        float[] probe,
        Guid? tenantId,
        Guid? courseId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var result = new List<(Guid, double)>();
        if (probe is not { Length: > 0 } || probe.Length != ConfiguredDimension ||
            !await IsAvailableAsync(cancellationToken))
        {
            return result;
        }

        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $@"
SELECT kp.""Id"", 1 - (kp.""{ColumnName}"" <=> @probe::vector) AS similarity
  FROM ""AppKnowledgePoints"" kp
 WHERE kp.""IsDeleted"" = false
   AND kp.""CanonicalId"" IS NULL
   AND kp.""{ColumnName}"" IS NOT NULL
   AND kp.""TenantId"" IS NOT DISTINCT FROM @tenantId::uuid
   AND (@courseId::uuid IS NULL OR kp.""CourseId"" = @courseId::uuid)
 ORDER BY kp.""{ColumnName}"" <=> @probe::vector
 LIMIT @take";

            command.Parameters.Add(new NpgsqlParameter("probe", ToPgVectorLiteral(probe)));
            command.Parameters.Add(new NpgsqlParameter("tenantId", (object?)tenantId ?? DBNull.Value));
            command.Parameters.Add(new NpgsqlParameter("courseId", (object?)courseId ?? DBNull.Value));
            command.Parameters.Add(new NpgsqlParameter("take", Math.Clamp(take, 1, 200)));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetGuid(0);
                var similarity = reader.IsDBNull(1) ? 0 : reader.GetDouble(1);
                result.Add((id, similarity));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "pgvector 检索失败，回退进程内余弦");
            SetProbe(false, _columnDimension, "检索失败：" + ex.Message);
            result.Clear();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return result;
    }

    /// <summary>当前状态（用于前端展示「向量引擎」）。</summary>
    public async Task<KnowledgeVectorState> GetStateAsync(
        Guid? tenantId,
        Guid? courseId = null,
        CancellationToken cancellationToken = default)
    {
        var available = await IsAvailableAsync(cancellationToken);
        if (!available)
        {
            return new KnowledgeVectorState(
                KnowledgeVectorState.EngineMemory,
                _columnDimension,
                false,
                0,
                _unavailableReason ?? "pgvector 不可用，相似度在应用进程内计算");
        }

        var count = 0;
        try
        {
            var connection = _dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandText = $@"
SELECT count(*)
  FROM ""AppKnowledgePoints""
 WHERE ""IsDeleted"" = false
   AND ""CanonicalId"" IS NULL
   AND ""{ColumnName}"" IS NOT NULL
   AND ""TenantId"" IS NOT DISTINCT FROM @tenantId::uuid
   AND (@courseId::uuid IS NULL OR ""CourseId"" = @courseId::uuid)";

                command.Parameters.Add(new NpgsqlParameter("tenantId", (object?)tenantId ?? DBNull.Value));
                command.Parameters.Add(new NpgsqlParameter("courseId", (object?)courseId ?? DBNull.Value));
                var scalar = await command.ExecuteScalarAsync(cancellationToken);
                count = scalar is long l ? (int)l : Convert.ToInt32(scalar ?? 0);
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "统计 pgvector 向量数量失败");
        }

        return new KnowledgeVectorState(
            KnowledgeVectorState.EnginePgvector,
            _columnDimension,
            true,
            count,
            count == 0 ? "pgvector 已启用，但尚未写入向量，可点击「重建语义向量」" : null);
    }

    /// <summary>清空指定知识点的向量（删除 / 归档时调用，节省索引空间）。</summary>
    public async Task ClearAsync(IReadOnlyList<Guid> knowledgePointIds, CancellationToken cancellationToken = default)
    {
        if (knowledgePointIds.Count == 0 || !await IsAvailableAsync(cancellationToken))
        {
            return;
        }

        try
        {
            var ids = string.Join(",", knowledgePointIds.Select(id => $"'{id}'::uuid"));
            await _dbContext.Database.ExecuteSqlRawAsync(
                $@"UPDATE ""AppKnowledgePoints"" SET ""{ColumnName}"" = NULL WHERE ""Id"" IN ({ids})",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "清空知识点向量失败（可忽略）");
        }
    }

    /// <summary>pgvector 文本字面量：[v1,v2,...]（不变量文化，避免小数点被本地化）。</summary>
    private static string ToPgVectorLiteral(float[] vector)
    {
        var builder = new StringBuilder(vector.Length * 12);
        builder.Append('[');
        for (var i = 0; i < vector.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(vector[i].ToString("R", CultureInfo.InvariantCulture));
        }

        builder.Append(']');
        return builder.ToString();
    }
}
