using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KnowledgeHub.DoubleHigh.Dtos;
using KnowledgeHub.DoubleHigh.Enums;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.DoubleHigh;

/// <summary>
/// 评估资料包（zip）生成器。
///
/// 单个项目的包内结构：
/// <code>
/// 项目名称/
///   00_项目说明.md          流程 / 阶段 / 进度说明
///   01_指标体系.csv        指标、类型、来源、权重、评分规则、必备佐证、设置依据
///   02_填报内容.csv        教师的填报值 / 说明 / 自评备注
///   03_佐证材料清单.csv    佐证文件清单与包内路径
///   04_评分汇总.csv        各指标完成度与加权得分
///   佐证材料/
///     I01_指标名称/
///       佐证名称/
///         1_原始文件名.ext
/// </code>
/// </summary>
public class DoubleHighPackageBuilder : ITransientDependency
{
    private readonly DoubleHighEvidenceFileStore _fileStore;
    private readonly ILogger<DoubleHighPackageBuilder> _logger;

    public DoubleHighPackageBuilder(
        DoubleHighEvidenceFileStore fileStore,
        ILogger<DoubleHighPackageBuilder> logger)
    {
        _fileStore = fileStore;
        _logger = logger;
    }

    /// <summary>生成单个项目的资料包（zip 字节流）。</summary>
    public async Task<byte[]> BuildProjectPackageAsync(
        DoubleHighProjectDetailDto detail,
        IReadOnlyDictionary<Guid, string> storedPaths)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var dir = SanitizeFileName(detail.Title);

            WriteText(archive, dir, "00_项目说明.md", BuildProjectReadme(detail));
            WriteText(archive, dir, "01_指标体系.csv", BuildIndicatorCatalogCsv(detail));
            WriteText(archive, dir, "02_填报内容.csv", BuildReportingCsv(detail));
            WriteText(archive, dir, "03_佐证材料清单.csv", BuildEvidenceListCsv(detail));
            WriteText(archive, dir, "04_指标完成情况.csv", BuildScoreCsv(detail));

            await WriteDocumentFilesAsync(archive, dir, detail, storedPaths);
            await WriteEvidenceFilesAsync(archive, dir, detail, storedPaths);
        }

        return buffer.ToArray();
    }

    /// <summary>生成包含多个项目的资料包（按项目分目录）。</summary>
    public async Task<byte[]> BuildAllPackagesAsync(
        IReadOnlyList<DoubleHighProjectDetailDto> details,
        IReadOnlyDictionary<Guid, string> storedPaths)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            const string root = "评估资料包";

            WriteText(archive, root, "00_导出总览.md", BuildOverview(details));

            var usedNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var detail in details)
            {
                var dir = $"{root}/{BuildUniqueFolderName(detail.Title, usedNames)}";

                WriteText(archive, dir, "00_项目说明.md", BuildProjectReadme(detail));
                WriteText(archive, dir, "01_指标体系.csv", BuildIndicatorCatalogCsv(detail));
                WriteText(archive, dir, "02_填报内容.csv", BuildReportingCsv(detail));
                WriteText(archive, dir, "03_佐证材料清单.csv", BuildEvidenceListCsv(detail));
                WriteText(archive, dir, "04_指标完成情况.csv", BuildScoreCsv(detail));

                await WriteDocumentFilesAsync(archive, dir, detail, storedPaths);
                await WriteEvidenceFilesAsync(archive, dir, detail, storedPaths);
            }
        }

        return buffer.ToArray();
    }

    // ── 文档内容 ────────────────────────────────────────────────────────

    private static string BuildOverview(IReadOnlyList<DoubleHighProjectDetailDto> details)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 评估资料包导出总览");
        sb.AppendLine();
        sb.AppendLine($"- 导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- 项目数量：{details.Count}");
        sb.AppendLine();
        sb.AppendLine("| 项目 | 模板 | 评估对象 | 流程阶段 | 填报截止 | 填报进度 |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- |");

        foreach (var d in details)
        {
            var progress = d.IndicatorCount == 0 ? 0 : (int)Math.Round(d.CompletedIndicatorCount * 100m / d.IndicatorCount);
            sb.AppendLine($"| {Escape(d.Title)} | {Escape(d.TemplateName ?? "-")} | {Escape(GetObjectLabel(d))} | {GetStageLabel(d.Status)} | {FormatDate(d.EndTime)} | {d.CompletedIndicatorCount}/{d.IndicatorCount}（{progress}%） |");
        }

        sb.AppendLine();
        sb.AppendLine("每个项目目录内含：项目说明、指标体系、填报内容、佐证材料清单、评分汇总，以及按「指标 / 佐证名称」分目录存放的佐证文件原件。");
        return sb.ToString();
    }

    private static string BuildProjectReadme(DoubleHighProjectDetailDto detail)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {detail.Title}");
        sb.AppendLine();
        sb.AppendLine($"- 评估对象：{GetObjectLabel(detail)}");
        sb.AppendLine($"- 评估模板：{detail.TemplateName ?? "-"}");
        sb.AppendLine($"- 批次编码：{detail.BatchCode}");
        sb.AppendLine($"- 填报截止：{FormatDate(detail.EndTime)}");
        sb.AppendLine($"- 填报人：{detail.ReporterName ?? "-"}");
        sb.AppendLine($"- 流程阶段：{GetStageLabel(detail.Status)}");
        sb.AppendLine($"- 填报进度：{detail.CompletedIndicatorCount}/{detail.IndicatorCount}");
        sb.AppendLine($"- 导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("## 评估流程");
        sb.AppendLine();
        sb.AppendLine("1. 创建项目（选择评估模板，确定评估对象与截止日期）");
        sb.AppendLine("2. 配置指标（指标类型、数据来源、权重、评分规则、必备佐证、设置依据）");
        sb.AppendLine("3. 填报并提交（系统取数自动带出，教师填写内容并上传佐证）");
        sb.AppendLine();
        sb.AppendLine("本项目到教师提交为止，不含审核、评分、公示等后续环节。");
        return sb.ToString();
    }

    private static string BuildIndicatorCatalogCsv(DoubleHighProjectDetailDto detail)
    {
        var rows = new List<string[]>
        {
            new[] { "编号", "指标名称", "填报类型", "单位", "数据来源", "取值范围", "权重", "目标值", "必备佐证", "设置依据" }
        };

        for (var i = 0; i < detail.Indicators.Count; i++)
        {
            var item = detail.Indicators[i];
            rows.Add(new[]
            {
                IndicatorCode(i),
                item.Name,
                GetIndicatorTypeLabel(item.IndicatorType),
                item.Unit ?? string.Empty,
                item.DataSourceName ?? GetDataSourceLabel(item.DataSourceType),
                item.ScopeLabel,
                item.Weight.ToString("0.####"),
                item.TargetValue?.ToString("0.####") ?? string.Empty,
                string.Join("、", item.RequiredEvidences ?? new List<string>()),
                GetBasisText(item)
            });
        }

        return ToCsv(rows);
    }

    private static string BuildReportingCsv(DoubleHighProjectDetailDto detail)
    {
        var rows = new List<string[]>
        {
            new[] { "编号", "指标名称", "填报类型", "系统取数/填报值", "填报说明", "自评说明", "数据来源", "已上传佐证数", "完成状态" }
        };

        for (var i = 0; i < detail.Indicators.Count; i++)
        {
            var item = detail.Indicators[i];
            var value = item.IndicatorType switch
            {
                DoubleHighIndicatorType.Auto => item.LatestValue == null
                    ? string.Empty
                    : FormatValue(item.LatestValue.Value, item.Unit),
                // 文字类指标的值在「填报说明」列，数值列留空
                DoubleHighIndicatorType.Text => string.Empty,
                _ => item.LatestValue == null ? string.Empty : item.LatestValue.Value.ToString("0.####")
            };

            rows.Add(new[]
            {
                IndicatorCode(i),
                item.Name,
                GetIndicatorTypeLabel(item.IndicatorType),
                value,
                item.LatestValue?.TextValue ?? string.Empty,
                item.LatestValue?.Note ?? string.Empty,
                item.DataSourceName ?? GetDataSourceLabel(item.DataSourceType),
                item.EvidenceCount.ToString(),
                item.CompletionStatus
            });
        }

        return ToCsv(rows);
    }

    private static string BuildEvidenceListCsv(DoubleHighProjectDetailDto detail)
    {
        var rows = new List<string[]>
        {
            new[] { "编号", "指标名称", "佐证名称", "文件名", "大小(字节)", "类型", "包内路径" }
        };

        var index = BuildIndicatorIndex(detail);
        var sequence = 0;
        foreach (var evidence in detail.Evidences.OrderBy(x => x.IndicatorId).ThenBy(x => x.SortOrder))
        {
            var code = index.TryGetValue(evidence.IndicatorId, out var value) ? value : "-";
            var indicatorName = detail.Indicators.FirstOrDefault(x => x.Id == evidence.IndicatorId)?.Name ?? string.Empty;
            var evidenceName = string.IsNullOrWhiteSpace(evidence.EvidenceName) ? evidence.Title : evidence.EvidenceName!;
            var fileName = string.IsNullOrWhiteSpace(evidence.FileName) ? evidence.Title : evidence.FileName!;

            rows.Add(new[]
            {
                code,
                indicatorName,
                evidenceName,
                fileName,
                evidence.FileSize.ToString(),
                evidence.IsFile ? "上传文件" : "链接",
                evidence.IsFile ? BuildEvidencePath(code, indicatorName, evidenceName, sequence, fileName) : string.Empty
            });

            sequence++;
        }

        return ToCsv(rows);
    }

    /// <summary>
    /// 指标完成情况（只反映"信息是否采集到位"，不做评分）。
    /// 说明：评分规则与加权得分属于后续评审环节，当前阶段只采集填报内容，因此不输出分数。
    /// </summary>
    private static string BuildScoreCsv(DoubleHighProjectDetailDto detail)
    {
        var rows = new List<string[]>
        {
            new[] { "编号", "指标名称", "填报类型", "权重", "取值范围", "设置依据", "已上传佐证数", "完成状态" }
        };

        for (var i = 0; i < detail.Indicators.Count; i++)
        {
            var item = detail.Indicators[i];
            rows.Add(new[]
            {
                IndicatorCode(i),
                item.Name,
                GetIndicatorTypeLabel(item.IndicatorType),
                item.Weight.ToString("0.####"),
                item.ScopeLabel,
                GetBasisText(item),
                item.EvidenceCount.ToString(),
                item.CompletionStatus
            });
        }

        return ToCsv(rows);
    }

    // ── 佐证文件 ────────────────────────────────────────────────────────

    private async Task WriteEvidenceFilesAsync(
        ZipArchive archive,
        string dir,
        DoubleHighProjectDetailDto detail,
        IReadOnlyDictionary<Guid, string> storedPaths)
    {
        var index = BuildIndicatorIndex(detail);
        var sequence = 0;

        foreach (var evidence in detail.Evidences.OrderBy(x => x.IndicatorId).ThenBy(x => x.SortOrder))
        {
            if (!evidence.IsFile)
            {
                sequence++;
                continue;
            }

            var code = index.TryGetValue(evidence.IndicatorId, out var value) ? value : "I00";
            var indicatorName = detail.Indicators.FirstOrDefault(x => x.Id == evidence.IndicatorId)?.Name ?? "未关联指标";
            var evidenceName = string.IsNullOrWhiteSpace(evidence.EvidenceName) ? evidence.Title : evidence.EvidenceName!;
            var fileName = string.IsNullOrWhiteSpace(evidence.FileName) ? evidence.Title : evidence.FileName!;
            var path = BuildEvidencePath(code, indicatorName, evidenceName, sequence, fileName);

            byte[]? content = null;
            if (storedPaths.TryGetValue(evidence.Id, out var storedPath) && !string.IsNullOrWhiteSpace(storedPath))
            {
                try
                {
                    content = await _fileStore.ReadAsync(storedPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "读取评估佐证文件失败：{EvidenceId}", evidence.Id);
                }
            }

            if (content == null)
            {
                // 取不到原件时写一个占位说明文件，保证清单与包内结构一致，不至于静默丢文件。
                var notice = $"{evidenceName} / {fileName}\r\n原始文件未能读取（可能已从存储中清理）。\r\n佐证地址：{evidence.AttachmentUrl}\r\n";
                WriteText(archive, dir, path + ".说明.txt", notice);
            }
            else
            {
                var entry = archive.CreateEntry($"{dir}/{path}");
                using var stream = entry.Open();
                await stream.WriteAsync(content);
            }

            sequence++;
        }
    }

    /// <summary>评估对象文案（类型 + 名称）。</summary>
    private static string GetObjectLabel(DoubleHighProjectDetailDto detail)
    {
        var type = detail.ObjectType switch
        {
            Enums.DoubleHighObjectType.ResourceLibrary => "教学资源库",
            Enums.DoubleHighObjectType.Major => "专业",
            Enums.DoubleHighObjectType.Course => "课程",
            _ => "其他对象（手动填写）"
        };

        return string.IsNullOrWhiteSpace(detail.ObjectName) ? type : $"{type}：{detail.ObjectName}";
    }

    /// <summary>指标的依据文案：引用项目依据文件时写「文件：xxx」，否则写手写文本。</summary>
    private static string GetBasisText(DoubleHighIndicatorDto item)
    {
        if (!string.IsNullOrWhiteSpace(item.BasisDocumentTitle))
        {
            return $"文件：{item.BasisDocumentTitle}";
        }

        return item.Basis ?? string.Empty;
    }

    /// <summary>写入项目依据文件（政策文件等）。</summary>
    private async Task WriteDocumentFilesAsync(
        ZipArchive archive,
        string dir,
        DoubleHighProjectDetailDto detail,
        IReadOnlyDictionary<Guid, string> storedPaths)
    {
        var documents = detail.Documents ?? new List<DoubleHighProjectDocumentDto>();
        var index = 0;

        foreach (var document in documents.OrderBy(x => x.SortOrder))
        {
            index++;
            var fileName = string.IsNullOrWhiteSpace(document.FileName) ? document.Title : document.FileName!;
            var path = $"依据文件/{index}_{SanitizeFileName(fileName)}";

            byte[]? content = null;
            if (storedPaths.TryGetValue(document.Id, out var storedPath) && !string.IsNullOrWhiteSpace(storedPath))
            {
                try
                {
                    content = await _fileStore.ReadAsync(storedPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "读取项目依据文件失败：{DocumentId}", document.Id);
                }
            }

            if (content == null)
            {
                var notice = $"{document.Title}\r\n原始文件未能读取（可能已从存储中清理）。\r\n地址：{document.FileUrl}\r\n";
                WriteText(archive, dir, path + ".说明.txt", notice);
            }
            else
            {
                var entry = archive.CreateEntry($"{dir}/{path}");
                using var stream = entry.Open();
                await stream.WriteAsync(content);
            }
        }
    }

    private static Dictionary<Guid, string> BuildIndicatorIndex(DoubleHighProjectDetailDto detail)
    {
        var map = new Dictionary<Guid, string>();
        for (var i = 0; i < detail.Indicators.Count; i++)
        {
            map[detail.Indicators[i].Id] = IndicatorCode(i);
        }

        return map;
    }

    private static string BuildEvidencePath(string code, string indicatorName, string evidenceName, int sequence, string fileName)
    {
        return string.Join("/",
            "佐证材料",
            $"{code}_{SanitizeFileName(indicatorName)}",
            SanitizeFileName(evidenceName),
            $"{sequence + 1}_{SanitizeFileName(fileName)}");
    }

    // ── 工具方法 ────────────────────────────────────────────────────────

    private static void WriteText(ZipArchive archive, string dir, string path, string content)
    {
        var entry = archive.CreateEntry($"{dir}/{path}");
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string ToCsv(IEnumerable<string[]> rows)
    {
        var sb = new StringBuilder();
        // BOM：保证 Excel 直接双击打开不乱码
        sb.Append('\uFEFF');
        var first = true;
        foreach (var row in rows)
        {
            if (!first)
            {
                sb.Append("\r\n");
            }

            first = false;
            sb.Append(string.Join(",", row.Select(EscapeCsvCell)));
        }

        return sb.ToString();
    }

    private static string EscapeCsvCell(string? value)
    {
        var text = value ?? string.Empty;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private static string Escape(string? value)
    {
        return (value ?? string.Empty).Replace("|", "\\|").Replace("\r\n", " ").Replace("\n", " ");
    }

    /// <summary>
    /// 生成不重复的目录名：同名项目（历史数据里常见）自动追加序号，避免在 zip 里互相覆盖。
    /// </summary>
    private static string BuildUniqueFolderName(string? title, Dictionary<string, int> usedNames)
    {
        var name = SanitizeFileName(title);
        if (!usedNames.TryGetValue(name, out var count))
        {
            usedNames[name] = 1;
            return name;
        }

        count++;
        usedNames[name] = count;
        return $"{name} ({count})";
    }

    internal static string SanitizeFileName(string? name)    {
        var text = (name ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            text = "未命名";
        }

        var invalid = new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '\r', '\n' };
        foreach (var ch in invalid)
        {
            text = text.Replace(ch, '_');
        }

        return text.Length > 60 ? text[..60] : text;
    }

    internal static string IndicatorCode(int index) => "I" + (index + 1).ToString("00");

    internal static string GetIndicatorTypeLabel(DoubleHighIndicatorType type) => type switch
    {
        DoubleHighIndicatorType.Auto => "系统取数",
        DoubleHighIndicatorType.Number => "填写数值",
        DoubleHighIndicatorType.Text => "填写说明",
        DoubleHighIndicatorType.EvidenceOnly => "仅佐证",
        _ => "填写数值"
    };

    internal static string GetScoringRuleLabel(DoubleHighScoringRule rule) => rule switch
    {
        DoubleHighScoringRule.Proportional => "比例制",
        DoubleHighScoringRule.Threshold => "达标制",
        DoubleHighScoringRule.Direct => "直接计分",
        DoubleHighScoringRule.None => "不计分",
        _ => "比例制"
    };

    internal static string GetDataSourceLabel(DoubleHighDataSourceType type) => type switch
    {
        DoubleHighDataSourceType.Manual => "手工填报",
        DoubleHighDataSourceType.ResourceCount => "资源数量",
        DoubleHighDataSourceType.CourseCount => "课程数量",
        DoubleHighDataSourceType.MicroMajorCount => "微专业数量",
        DoubleHighDataSourceType.PracticumProjectCount => "实训项目数量",
        DoubleHighDataSourceType.NewsArticleCount => "资讯数量",
        DoubleHighDataSourceType.MicroMajorEnrollmentCount => "微专业报名量",
        DoubleHighDataSourceType.PracticumEnrollmentCount => "实训参与量",
        DoubleHighDataSourceType.CourseEnrollmentCount => "课程选课人次",
        DoubleHighDataSourceType.CourseCompletionRate => "课程学习完成率",
        DoubleHighDataSourceType.EmploymentRate => "就业率",
        _ => "手工填报"
    };

    internal static string GetStageLabel(DoubleHighProjectStatus status) => status switch
    {
        DoubleHighProjectStatus.Draft => "① 配置中",
        DoubleHighProjectStatus.Active => "② 填报中",
        DoubleHighProjectStatus.Submitted => "③ 已提交",
        DoubleHighProjectStatus.Closed => "④ 已关闭",
        _ => "① 配置中"
    };

    private static string FormatDate(DateTime? value) => value?.ToString("yyyy-MM-dd") ?? "-";

    private static string FormatValue(decimal value, string? unit) => value.ToString("0.####") + (unit ?? string.Empty);
}
