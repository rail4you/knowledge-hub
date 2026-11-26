using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Courses.Enums;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace KnowledgeHub.Courses;

/// <summary>
/// 知识点树 Excel 导入（只支持 xlsx，不支持 XMind/OPML）。
///
/// <para>
/// 表格格式（第 1 行标题、第 2 行表头，与「章节导入」一致，老师不用学两套）：
/// </para>
/// <code>
/// A 一级单元 | B 二级单元 | C 三级单元 | D 知识项 | E 所属章节 | F 重要度 | G 难度 | H 标签 | I 描述
/// 立体构成   | 材料      |           | 纸板     | 第 1 章    | 核心     | 2      | 考点    | 常用易加工
/// </code>
/// <para>
/// 规则：
/// ① A/B/C 建 <see cref="KnowledgeNodeType.Unit"/>（可嵌套），D 建
///    <see cref="KnowledgeNodeType.Item"/>（叶子，掌握度与练习的最小粒度）；
/// ② 只有 A/B/C 有值、D 为空 → 只建单元；
/// ③ **同一父级下同名节点复用**：重复导入不会产生重复节点（Idempotent，方便补数据）；
/// ④ E 列章节按标题匹配本课已有章节，匹配不到只提示、不阻断；
/// ⑤ F 列接受 core/important/normal 或 核心/重要/一般；G 列难度夹紧到 1~5。
/// </para>
/// </summary>
public class KnowledgeResourceExcelImporter : ITransientDependency
{
    /// <summary>表头行数（第 1 行是标题/说明，第 2 行是表头）。</summary>
    private const int HeaderRows = 2;

    private const int MaxDepth = 3;

    private readonly IRepository<KnowledgeResource, Guid> _resourceRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public KnowledgeResourceExcelImporter(
        IRepository<KnowledgeResource, Guid> resourceRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IGuidGenerator guidGenerator,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _resourceRepository = resourceRepository;
        _chapterRepository = chapterRepository;
        _guidGenerator = guidGenerator;
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>生成导入模板（表头 + 2 行示例 + 一页填写说明）。</summary>
    public byte[] BuildTemplate()
    {
        using var workbook = new XLWorkbook();

        var sheet = workbook.Worksheets.Add("知识点");
        sheet.Cell(1, 1).Value = "知识点导入模板：A/B/C 列建「单元」（可嵌套），D 列建「知识项」（叶子）；同一父级下同名节点会复用，可重复导入补数据。";
        sheet.Range(1, 1, 1, 9).Merge();
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Alignment.WrapText = true;
        sheet.Row(1).Height = 34;

        var headers = new[] { "一级单元", "二级单元", "三级单元", "知识项", "所属章节", "重要度", "难度", "标签", "描述" };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(2, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F4F6");
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        sheet.Cell(3, 1).Value = "立体构成基础";
        sheet.Cell(3, 2).Value = "构成中的材料";
        sheet.Cell(3, 4).Value = "人工材料";
        sheet.Cell(3, 5).Value = "";
        sheet.Cell(3, 6).Value = "核心";
        sheet.Cell(3, 7).Value = 2;
        sheet.Cell(3, 8).Value = "考点";
        sheet.Cell(3, 9).Value = "按来源分类，常用于材料实验";

        sheet.Cell(4, 1).Value = "立体构成基础";
        sheet.Cell(4, 2).Value = "构成中的材料";
        sheet.Cell(4, 4).Value = "天然材料";
        sheet.Cell(4, 6).Value = "重要";
        sheet.Cell(4, 7).Value = 3;

        sheet.Cell(5, 1).Value = "立体构成基础";
        sheet.Cell(5, 2).Value = "构成中的肌理";
        sheet.Cell(5, 4).Value = "触觉肌理";

        sheet.Columns().AdjustToContents();
        sheet.Column(9).Width = 40;

        var readme = workbook.Worksheets.Add("填写说明");
        var lines = new[]
        {
            "1. 只支持 xlsx（不支持 XMind / OPML）。",
            "2. A/B/C 三列是「单元」，可以只填前两列；D 列「知识项」是叶子节点（习题/视频/掌握度都挂在它上面）。",
            "3. 同一行只有 A/B/C 有值、D 为空时，只建单元本身。",
            "4. 同一父级下同名节点会复用，不会重复创建；可以多次导入补数据。",
            "5. 「所属章节」填本课已有章节标题即可绑定（留空不绑定）。",
            "6. 「重要度」可填 core/important/normal 或 核心/重要/一般；「难度」1~5，超出会夹紧。",
            "7. 「标签」用逗号分隔；「描述」会进入 AI 出题 / 教案生成的提示词。",
            "8. 第 1 行是说明、第 2 行是表头，从第 3 行开始才是数据，请不要删改前两行。",
        };
        for (var i = 0; i < lines.Length; i++)
        {
            readme.Cell(i + 1, 1).Value = lines[i];
        }

        readme.Column(1).Width = 100;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<KnowledgeResourceImportResultDto> ImportAsync(Guid courseId, Stream stream, string? fileName = null)
    {
        var result = new KnowledgeResourceImportResultDto();

        if (fileName != null && !fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException("只支持 .xlsx 格式（不支持 XLS / XMind / OPML）。");
        }

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex)
        {
            throw new UserFriendlyException($"Excel 解析失败：{ex.Message}");
        }

        using (workbook)
        {
            var worksheet = workbook.Worksheets.First();

            // 已有节点：用于「同父级同名复用」，保证重复导入幂等
            var existing = (await _resourceRepository.GetListAsync(x => x.CourseId == courseId)).ToList();
            var byParentAndName = existing
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => (x.ParentId ?? Guid.Empty, x.Name.Trim()))
                .ToDictionary(g => g.Key, g => g.First());

            // 章节标题 → Id（E 列绑定）
            var chapters = await _chapterRepository.GetListAsync(x => x.CourseId == courseId);
            var chapterByName = chapters
                .Where(x => !string.IsNullOrWhiteSpace(x.Title))
                .GroupBy(x => x.Title.Trim())
                .ToDictionary(g => g.Key, g => g.First().Id);

            var rows = worksheet.RowsUsed().Skip(HeaderRows).ToList();
            result.TotalRows = rows.Count;
            if (rows.Count == 0)
            {
                result.Errors.Add("表格里没有数据行（第 3 行起才是数据）。");
                return result;
            }

            // 每一层当前生效的节点（父级链），跨行累积 —— 与章节导入同一套「层级列」思路
            var levelNodes = new KnowledgeResource?[MaxDepth + 1];
            var sortCounter = new Dictionary<Guid, int>(); // 父Id(空=根) → 下一个 sortOrder

            foreach (var row in rows)
            {
                try
                {
                    var levels = new string?[MaxDepth];
                    for (var i = 0; i < MaxDepth; i++)
                    {
                        var value = row.Cell(i + 1).GetString()?.Trim();
                        levels[i] = string.IsNullOrWhiteSpace(value) ? null : value;
                    }

                    var itemName = row.Cell(4).GetString()?.Trim();
                    var chapterName = row.Cell(5).GetString()?.Trim();
                    var importance = NormalizeImportance(row.Cell(6).GetString());
                    var difficultyRaw = row.Cell(7).GetString();
                    var tags = row.Cell(8).GetString()?.Trim();
                    var description = row.Cell(9).GetString()?.Trim();

                    var hasUnit = levels.Any(x => x != null);
                    if (!hasUnit && string.IsNullOrWhiteSpace(itemName))
                    {
                        continue; // 完全空行
                    }

                    Guid? chapterId = null;
                    if (!string.IsNullOrWhiteSpace(chapterName) &&
                        chapterByName.TryGetValue(chapterName, out var found))
                    {
                        chapterId = found;
                    }
                    else if (!string.IsNullOrWhiteSpace(chapterName) && result.Warnings.Count < 5)
                    {
                        result.Warnings.Add($"行 {row.RowNumber()}：章节「{chapterName}」不存在，已忽略该列");
                    }

                    // ① 逐级建/复用单元
                    Guid? parentId = null;
                    var depthReached = 0;
                    for (var level = 0; level < MaxDepth; level++)
                    {
                        var name = levels[level];
                        if (name == null)
                        {
                            // 该层为空 → 后续较深层不允许跳级（层级列语义：必须连续）
                            for (var clear = level; clear < MaxDepth; clear++)
                            {
                                levelNodes[clear] = null;
                            }
                            break;
                        }

                        var node = await GetOrCreateAsync(
                            courseId, parentId, name, KnowledgeNodeType.Unit, chapterId,
                            importance, ParseDifficulty(difficultyRaw), tags, null,
                            byParentAndName, sortCounter, result);

                        levelNodes[level] = node;
                        parentId = node.Id;
                        depthReached = level + 1;

                        // 出现更浅的层级时，清掉更深的引用
                        for (var deeper = level + 1; deeper < MaxDepth; deeper++)
                        {
                            if (levels[deeper] == null)
                            {
                                levelNodes[deeper] = null;
                            }
                        }
                    }

                    // ② 建/复用知识项（叶子）
                    if (!string.IsNullOrWhiteSpace(itemName))
                    {
                        await GetOrCreateAsync(
                            courseId, parentId, itemName, KnowledgeNodeType.Item, chapterId,
                            importance, ParseDifficulty(difficultyRaw), tags, description,
                            byParentAndName, sortCounter, result);
                    }

                    _ = depthReached;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"行 {row.RowNumber()}：{ex.Message}");
                }
            }
        }

        if (_unitOfWorkManager.Current is not null)
        {
            await _unitOfWorkManager.Current.SaveChangesAsync();
        }

        result.SortPathsRegenerated = await RecalculateSortPathsAsync(courseId);
        return result;
    }

    /// <summary>
    /// 按「父级 + 名称」复用或新建。
    /// 复用时**只补空字段**（不让导入覆盖老师手改的内容）。
    /// </summary>
    private async Task<KnowledgeResource> GetOrCreateAsync(
        Guid courseId,
        Guid? parentId,
        string name,
        KnowledgeNodeType nodeType,
        Guid? chapterId,
        string importance,
        int difficulty,
        string? tags,
        string? description,
        Dictionary<(Guid, string), KnowledgeResource> cache,
        Dictionary<Guid, int> sortCounter,
        KnowledgeResourceImportResultDto result)
    {
        var key = (parentId ?? Guid.Empty, name);

        if (cache.TryGetValue(key, out var existing))
        {
            // 复用时补齐空值（章节 / 描述 / 标签），不覆盖已有内容
            var dirty = false;
            if (!existing.ChapterId.HasValue && chapterId.HasValue)
            {
                existing.ChapterId = chapterId;
                dirty = true;
            }

            if (string.IsNullOrWhiteSpace(existing.Description) && !string.IsNullOrWhiteSpace(description))
            {
                existing.Description = description;
                dirty = true;
            }

            if (string.IsNullOrWhiteSpace(existing.Tags) && !string.IsNullOrWhiteSpace(tags))
            {
                existing.Tags = tags;
                dirty = true;
            }

            if (dirty)
            {
                await _resourceRepository.UpdateAsync(existing);
            }

            result.ReusedCount++;
            return existing;
        }

        var parentKey = parentId ?? Guid.Empty;
        var sortOrder = sortCounter.TryGetValue(parentKey, out var next) ? next : 0;
        sortCounter[parentKey] = sortOrder + 1;

        var node = new KnowledgeResource(_guidGenerator.Create(), courseId, name)
        {
            ParentId = parentId,
            NodeType = nodeType,
            ChapterId = chapterId,
            ImportanceLevel = importance,
            Difficulty = difficulty,
            Tags = tags,
            Description = description,
            SortOrder = sortOrder
        };

        await _resourceRepository.InsertAsync(node);
        cache[key] = node;

        if (nodeType == KnowledgeNodeType.Unit)
        {
            result.CreatedUnits++;
        }
        else
        {
            result.CreatedItems++;
        }

        return node;
    }

    /// <summary>重算物化路径（同 AppService 的规则；导入后必须调用，否则树序靠 SortOrder 兜底）。</summary>
    private async Task<int> RecalculateSortPathsAsync(Guid courseId)
    {
        var items = await _resourceRepository.GetListAsync(x => x.CourseId == courseId);
        if (items.Count == 0)
        {
            return 0;
        }

        var byParent = items.GroupBy(x => x.ParentId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToList());

        var changed = 0;
        void Walk(Guid parentKey, string parentPath, int depth)
        {
            if (depth > 16 || !byParent.TryGetValue(parentKey, out var siblings))
            {
                return;
            }

            for (var i = 0; i < siblings.Count; i++)
            {
                var node = siblings[i];
                var segment = (i + 1).ToString("D4");
                var path = string.IsNullOrEmpty(parentPath) ? segment : parentPath + "." + segment;
                if (node.SortPath != path)
                {
                    node.SortPath = path;
                    changed++;
                }

                Walk(node.Id, path, depth + 1);
            }
        }

        Walk(Guid.Empty, string.Empty, 0);

        // 课程内节点数量级有限，导入是低频操作：整体更新即可（避免维护"哪些变了"的集合）
        foreach (var node in items)
        {
            await _resourceRepository.UpdateAsync(node);
        }

        return changed;
    }

    private static string NormalizeImportance(string? raw)
    {
        var v = raw?.Trim().ToLowerInvariant();
        return v switch
        {
            "core" or "核心" => "core",
            "important" or "重要" => "important",
            "normal" or "一般" or "" or null => "normal",
            _ => "normal"
        };
    }

    private static int ParseDifficulty(string? raw)
    {
        if (int.TryParse(raw?.Trim(), out var value))
        {
            return Math.Clamp(value, 1, 5);
        }

        return 2;
    }
}
