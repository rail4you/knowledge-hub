using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

/// <summary>
/// 课程案例库应用服务。
/// 案例三种来源：手动录入（CreateAsync）/ Excel 导入（ImportFromExcelAsync）/
/// AI 生成（走 AiGenerationJob，落库时 Source = Ai，见 AiGenerationJob.TryPersistCourseCaseAsync）。
/// </summary>
[IgnoreAntiforgeryToken]
public class CourseCaseAppService : KnowledgeHubAppService, ICourseCaseAppService
{
    private static readonly JsonSerializerOptions ContentJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IRepository<CourseCase, Guid> _caseRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;

    public CourseCaseAppService(
        IRepository<CourseCase, Guid> caseRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository)
    {
        _caseRepository = caseRepository;
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
    }

    public async Task<PagedResultDto<CourseCaseDto>> GetListAsync(CourseCaseQueryDto input)
    {
        var query = await _caseRepository.GetQueryableAsync();

        if (input.CourseId.HasValue)
        {
            query = query.Where(x => x.CourseId == input.CourseId.Value);
        }
        if (input.ChapterId.HasValue)
        {
            query = query.Where(x => x.ChapterId == input.ChapterId.Value);
        }
        if (input.Source.HasValue)
        {
            // AI 生成 vs 非 AI：来源筛选按语义分组，避免前端下拉过长
            query = query.Where(x => x.Source == input.Source.Value);
        }
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var kw = input.Filter.Trim();
            query = query.Where(x =>
                x.Title.Contains(kw) ||
                (x.Summary != null && x.Summary.Contains(kw)) ||
                (x.ChapterTitle != null && x.ChapterTitle.Contains(kw)) ||
                (x.Industry != null && x.Industry.Contains(kw)));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);
        var items = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        var dtos = items.Select(MapToDto).ToList();
        await FillCourseNamesAsync(dtos);

        return new PagedResultDto<CourseCaseDto>(totalCount, dtos);
    }

    public async Task<CourseCaseDto> GetAsync(Guid id)
    {
        var entity = await _caseRepository.GetAsync(id);
        var dto = MapToDto(entity);
        await FillCourseNamesAsync(new List<CourseCaseDto> { dto });
        return dto;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<CourseCaseDto> CreateAsync(CreateCourseCaseDto input)
    {
        var course = await FindCourseAsync(input.CourseId);
        if (course == null)
        {
            throw new UserFriendlyException("课程不存在");
        }

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("请填写案例标题");
        }

        var chapterTitle = await ResolveChapterTitleAsync(input.CourseId, input.ChapterId, input.ChapterTitle);

        var entity = new CourseCase(GuidGenerator.Create(), input.CourseId, input.Title.Trim())
        {
            TenantId = CurrentTenant.Id,
            ChapterId = input.ChapterId,
            ChapterTitle = chapterTitle,
            Source = CourseCaseSource.Manual,
            CreatorName = CurrentUser.Name ?? CurrentUser.UserName
        };

        ApplyContent(entity, input.Content, input.Summary);

        await _caseRepository.InsertAsync(entity);

        var dto = MapToDto(entity);
        dto.CourseName = course.Title;
        return dto;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<CourseCaseDto> UpdateAsync(Guid id, UpdateCourseCaseDto input)
    {
        var entity = await _caseRepository.GetAsync(id);

        entity.Title = input.Title.Trim();
        entity.ChapterId = input.ChapterId;
        entity.ChapterTitle = await ResolveChapterTitleAsync(entity.CourseId, input.ChapterId, input.ChapterTitle);

        ApplyContent(entity, input.Content, input.Summary);

        // 教师手工改过内容后仍保留原来源标记（AI 生成的案例改完还是 AI 来源，
        // 便于统计「AI 产出被采用的比例」）；只有原本没有内容时才升级为手动录入。
        if (string.IsNullOrWhiteSpace(entity.ContentJson))
        {
            entity.Source = CourseCaseSource.Manual;
        }

        await _caseRepository.UpdateAsync(entity);

        var dto = MapToDto(entity);
        await FillCourseNamesAsync(new List<CourseCaseDto> { dto });
        return dto;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        await _caseRepository.DeleteAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<CourseCaseImportResultDto> ImportFromExcelAsync(Guid courseId, IFormFile file)
    {
        var result = new CourseCaseImportResultDto();

        var course = await FindCourseAsync(courseId);
        if (course == null)
        {
            throw new UserFriendlyException("课程不存在");
        }

        try
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();

            var headerMap = ResolveCaseHeader(worksheet);
            var rows = worksheet.RowsUsed().Skip(headerMap.DataStartRow).ToList();
            result.TotalRows = rows.Count;

            if (rows.Count == 0)
            {
                result.Errors.Add("Excel 文件中没有数据行");
                return result;
            }

            var creatorName = CurrentUser.Name ?? CurrentUser.UserName;
            var tenantId = CurrentTenant.Id;

            foreach (var row in rows)
            {
                try
                {
                    var title = ReadCell(row, headerMap.Title).Trim();
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        continue;
                    }

                    var chapterTitle = ReadCell(row, headerMap.ChapterTitle).Trim();
                    Guid? chapterId = null;
                    if (!string.IsNullOrWhiteSpace(chapterTitle))
                    {
                        chapterId = await FindChapterIdByTitleAsync(courseId, chapterTitle);
                    }

                    var content = new CourseCaseContentDto
                    {
                        Title = title,
                        Summary = ReadCell(row, headerMap.Summary).Trim(),
                        Background = new CourseCaseBackgroundDto
                        {
                            Industry = ReadCell(row, headerMap.Industry).Trim(),
                            Timeframe = ReadCell(row, headerMap.Timeframe).Trim(),
                            Context = ReadCell(row, headerMap.Context).Trim(),
                            Stakeholders = SplitList(ReadCell(row, headerMap.Stakeholders))
                        },
                        KeyIssues = ParseKeyIssues(ReadCell(row, headerMap.KeyIssues)),
                        Solutions = ParseSolutions(ReadCell(row, headerMap.Solutions)),
                        KeyInsights = SplitList(ReadCell(row, headerMap.KeyInsights)),
                        Recommendations = SplitList(ReadCell(row, headerMap.Recommendations))
                    };

                    var entity = new CourseCase(GuidGenerator.Create(), courseId, title)
                    {
                        TenantId = tenantId,
                        ChapterId = chapterId,
                        ChapterTitle = string.IsNullOrWhiteSpace(chapterTitle) ? null : chapterTitle,
                        Source = CourseCaseSource.Import,
                        CreatorName = creatorName
                    };
                    ApplyContent(entity, content, content.Summary);

                    await _caseRepository.InsertAsync(entity);
                    result.SuccessCount++;
                }
                catch (Exception ex)
                {
                    result.FailCount++;
                    result.Errors.Add($"行 {row.RowNumber()}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"解析 Excel 文件失败: {ex.Message}");
        }

        return result;
    }

    // ────────────────────────── 内部工具 ──────────────────────────

    /// <summary>
    /// 查找课程。宿主管理员（CurrentTenant 为空）在后台能看全量课程列表，
    /// 因此这里对宿主放行跨租户查找，否则「系统管理员登录」下选任何租户课程
    /// 都会报「课程不存在」。租户用户仍受租户过滤器约束。
    /// 与「习题管理」的口径保持一致。
    /// </summary>
    private async Task<Course?> FindCourseAsync(Guid courseId)
    {
        if (CurrentTenant.Id.HasValue)
        {
            return await _courseRepository.FindAsync(courseId);
        }

        using (DataFilter.Disable<IMultiTenant>())
        {
            return await _courseRepository.FindAsync(courseId);
        }
    }

    /// <summary>查找章节（跨租户口径同 <see cref="FindCourseAsync"/>）。</summary>
    private async Task<Chapter?> FindChapterAsync(Guid chapterId)
    {
        if (CurrentTenant.Id.HasValue)
        {
            return await _chapterRepository.FindAsync(chapterId);
        }

        using (DataFilter.Disable<IMultiTenant>())
        {
            return await _chapterRepository.FindAsync(chapterId);
        }
    }

    /// <summary>
    /// 写入结构化内容：序列化 JSON，同时把标题 / 摘要 / 行业 / 时间范围
    /// 冗余到独立列，避免列表页为了筛选而反序列化每条案例的 JSON。
    /// </summary>
    private static void ApplyContent(CourseCase entity, CourseCaseContentDto? content, string? summaryFallback)
    {
        if (content == null)
        {
            entity.Summary = summaryFallback;
            entity.ContentJson = null;
            return;
        }

        content.Title = string.IsNullOrWhiteSpace(content.Title) ? entity.Title : content.Title.Trim();
        content.Summary = content.Summary ?? string.Empty;
        content.Background ??= new CourseCaseBackgroundDto();

        entity.Summary = !string.IsNullOrWhiteSpace(content.Summary)
            ? content.Summary
            : summaryFallback;
        entity.Industry = Truncate(content.Background.Industry, 128);
        entity.Timeframe = Truncate(content.Background.Timeframe, 64);
        entity.ContentJson = JsonSerializer.Serialize(content, ContentJsonOptions);
    }

    private async Task<string?> ResolveChapterTitleAsync(Guid courseId, Guid? chapterId, string? fallbackTitle)
    {
        if (!chapterId.HasValue)
        {
            return null;
        }

        var chapter = await FindChapterAsync(chapterId.Value);
        if (chapter == null)
        {
            return fallbackTitle;
        }
        if (chapter.CourseId != courseId)
        {
            throw new UserFriendlyException($"章节《{chapter.Title}》不属于当前课程");
        }
        return chapter.Title;
    }

    private async Task<Guid?> FindChapterIdByTitleAsync(Guid courseId, string chapterTitle)
    {
        var query = await _chapterRepository.GetQueryableAsync();
        if (!CurrentTenant.Id.HasValue)
        {
            using (DataFilter.Disable<IMultiTenant>())
            {
                var all = await AsyncExecuter.ToListAsync(
                    query.Where(x => x.CourseId == courseId && x.Title == chapterTitle).Take(1));
                return all.FirstOrDefault()?.Id;
            }
        }

        var chapter = await AsyncExecuter.FirstOrDefaultAsync(
            query.Where(x => x.CourseId == courseId && x.Title == chapterTitle));
        return chapter?.Id;
    }

    private static CourseCaseDto MapToDto(CourseCase entity)
    {
        return new CourseCaseDto
        {
            Id = entity.Id,
            CourseId = entity.CourseId,
            ChapterId = entity.ChapterId,
            ChapterTitle = entity.ChapterTitle,
            Title = entity.Title,
            Summary = entity.Summary,
            Industry = entity.Industry,
            Timeframe = entity.Timeframe,
            ContentJson = entity.ContentJson,
            Content = ParseContent(entity.ContentJson, entity.Title, entity.Summary),
            Source = entity.Source,
            AiTaskId = entity.AiTaskId,
            CreatorName = entity.CreatorName,
            CreationTime = entity.CreationTime,
            CreatorId = entity.CreatorId,
            LastModificationTime = entity.LastModificationTime,
            LastModifierId = entity.LastModifierId
        };
    }

    /// <summary>反序列化案例内容；脏数据不应让整页列表崩掉，解析失败时退回标题 + 摘要。</summary>
    public static CourseCaseContentDto ParseContent(string? contentJson, string title, string? summary)
    {
        var fallback = new CourseCaseContentDto
        {
            Title = title,
            Summary = summary ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(contentJson))
        {
            return fallback;
        }

        var clean = StripMarkdownFences(contentJson);
        try
        {
            var parsed = JsonSerializer.Deserialize<CourseCaseContentDto>(clean, ContentJsonOptions);
            if (parsed == null)
            {
                return fallback;
            }
            parsed.Title = string.IsNullOrWhiteSpace(parsed.Title) ? title : parsed.Title;
            parsed.Summary ??= summary ?? string.Empty;
            parsed.Background ??= new CourseCaseBackgroundDto();
            parsed.Background.Stakeholders ??= new List<string>();
            parsed.KeyIssues ??= new List<CourseCaseKeyIssueDto>();
            parsed.Solutions ??= new List<CourseCaseSolutionDto>();
            parsed.KeyInsights ??= new List<string>();
            parsed.Recommendations ??= new List<string>();
            return parsed;
        }
        catch (JsonException)
        {
            return fallback;
        }
    }

    public static string StripMarkdownFences(string text)
    {
        var t = text.Trim();
        if (t.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            t = t[7..];
            var idx = t.LastIndexOf("```", StringComparison.Ordinal);
            if (idx >= 0) t = t[..idx];
        }
        else if (t.StartsWith("```", StringComparison.Ordinal))
        {
            t = t[3..];
            var idx = t.LastIndexOf("```", StringComparison.Ordinal);
            if (idx >= 0) t = t[..idx];
        }
        return t.Trim();
    }

    private async Task FillCourseNamesAsync(List<CourseCaseDto> dtos)
    {
        if (dtos.Count == 0) return;

        var courseIds = dtos.Select(x => x.CourseId).Distinct().ToList();
        var courseQuery = await _courseRepository.GetQueryableAsync();
        var courses = await AsyncExecuter.ToListAsync(
            courseQuery.Where(x => courseIds.Contains(x.Id)).Select(x => new { x.Id, x.Title }));

        var nameMap = courses.ToDictionary(x => x.Id, x => x.Title);
        foreach (var dto in dtos)
        {
            if (nameMap.TryGetValue(dto.CourseId, out var name))
            {
                dto.CourseName = name;
            }
        }
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return v.Length <= max ? v : v[..max];
    }

    private static string ReadCell(IXLRow row, int colIndex)
    {
        return colIndex <= 0 ? string.Empty : (row.Cell(colIndex).GetString() ?? string.Empty);
    }

    /// <summary>多值列：按换行 / 分号 / 顿号切分。</summary>
    private static List<string> SplitList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
        return raw
            .Split(new[] { '\n', '\r', '；', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().TrimStart('-', '·', '•', '*').Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    /// <summary>
    /// 关键问题列：每行一条。支持两种写法：
    ///   1. 「标题：描述|影响|严重程度」（只写标题也可以）
    ///   2. 「标题：描述」
    /// 缺少严重程度时默认「中」。
    /// </summary>
    private static List<CourseCaseKeyIssueDto> ParseKeyIssues(string raw)
    {
        var list = new List<CourseCaseKeyIssueDto>();
        var index = 1;
        foreach (var line in SplitList(raw))
        {
            var parts = line
                .Replace('｜', '|')
                .Split('|', StringSplitOptions.None);

            var head = SplitTitleAndDescription(parts[0]);
            var issue = new CourseCaseKeyIssueDto
            {
                Id = index.ToString(),
                Title = head.Title,
                Description = head.Description,
                Severity = "中"
            };

            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])) issue.Impact = parts[1].Trim();
            if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]))
            {
                var severity = parts[2].Trim();
                if (severity is "高" or "中" or "低") issue.Severity = severity;
                else issue.Impact = string.IsNullOrWhiteSpace(issue.Impact) ? severity : $"{issue.Impact}；{severity}";
            }
            if (parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]))
            {
                var severity = parts[3].Trim();
                if (severity is "高" or "中" or "低") issue.Severity = severity;
            }

            if (!string.IsNullOrWhiteSpace(issue.Title))
            {
                list.Add(issue);
                index++;
            }
        }
        return list;
    }

    /// <summary>
    /// 解决方案列：一个单元格可写多个方案，按行拆分：
    ///   - 含「标题：描述」的行 → 开启一个新方案
    ///   - 不含分隔符的行 → 归为上一个方案的实施步骤
    /// </summary>
    private static List<CourseCaseSolutionDto> ParseSolutions(string raw)
    {
        var list = new List<CourseCaseSolutionDto>();
        foreach (var line in SplitList(raw))
        {
            var hasSeparator = line.IndexOfAny(new[] { '：', ':' }) > 0;
            if (hasSeparator || list.Count == 0)
            {
                var (title, description) = SplitTitleAndDescription(line);
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }
                list.Add(new CourseCaseSolutionDto
                {
                    Id = (list.Count + 1).ToString(),
                    Title = title,
                    Description = description
                });
            }
            else
            {
                list[^1].Steps.Add(line.Trim());
            }
        }
        return list;
    }

    private static (string Title, string Description) SplitTitleAndDescription(string line)
    {
        var value = (line ?? string.Empty).Trim();
        var idx = value.IndexOfAny(new[] { '：', ':' });
        if (idx <= 0)
        {
            return (value.TrimEnd('：', ':'), string.Empty);
        }
        var title = value[..idx].Trim();
        var description = value[(idx + 1)..].Trim();
        return (title, description);
    }

    /// <summary>
    /// 解析案例模板表头。支持列：标题 / 摘要 / 所属章节 / 所属行业 / 时间范围 /
    /// 背景描述 / 相关方 / 关键问题 / 解决方案 / 核心洞察 / 建议。
    /// 仅「标题」为必填列，其余缺失按空处理。
    /// </summary>
    private static CourseCaseHeaderMap ResolveCaseHeader(IXLWorksheet worksheet)
    {
        var map = new CourseCaseHeaderMap();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        for (var r = 1; r <= Math.Min(4, lastRow); r++)
        {
            var row = worksheet.Row(r);
            for (var c = 1; c <= Math.Min(16, lastCol); c++)
            {
                var header = (row.Cell(c).GetString() ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(header)) continue;

                if (map.Title == 0 && ContainsAny(header, "案例标题", "标题", "title")) { map.Title = c; map.DataStartRow = r; }
                else if (map.Summary == 0 && ContainsAny(header, "摘要", "简介", "summary")) { map.Summary = c; map.DataStartRow = r; }
                else if (map.ChapterTitle == 0 && ContainsAny(header, "章节", "chapter")) { map.ChapterTitle = c; map.DataStartRow = r; }
                else if (map.Industry == 0 && ContainsAny(header, "行业", "industry")) { map.Industry = c; map.DataStartRow = r; }
                else if (map.Timeframe == 0 && ContainsAny(header, "时间范围", "时间", "timeframe")) { map.Timeframe = c; map.DataStartRow = r; }
                else if (map.Context == 0 && ContainsAny(header, "背景", "context", "背景描述")) { map.Context = c; map.DataStartRow = r; }
                else if (map.Stakeholders == 0 && ContainsAny(header, "相关方", "stakeholder")) { map.Stakeholders = c; map.DataStartRow = r; }
                else if (map.KeyIssues == 0 && ContainsAny(header, "关键问题", "问题", "issue")) { map.KeyIssues = c; map.DataStartRow = r; }
                else if (map.Solutions == 0 && ContainsAny(header, "解决方案", "方案", "solution")) { map.Solutions = c; map.DataStartRow = r; }
                else if (map.KeyInsights == 0 && ContainsAny(header, "洞察", "insight")) { map.KeyInsights = c; map.DataStartRow = r; }
                else if (map.Recommendations == 0 && ContainsAny(header, "建议", "recommendation")) { map.Recommendations = c; map.DataStartRow = r; }
            }
            if (map.Title > 0) break;
        }

        if (map.Title == 0)
        {
            throw new UserFriendlyException("案例模板缺少「案例标题」列，请使用模板表头：案例标题 / 摘要 / 所属章节 / 所属行业 / 时间范围 / 背景描述 / 相关方 / 关键问题 / 解决方案 / 核心洞察 / 建议。");
        }

        return map;
    }

    private static bool ContainsAny(string value, params string[] keywords)
    {
        foreach (var kw in keywords)
        {
            if (value.Contains(kw, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private sealed class CourseCaseHeaderMap
    {
        public int Title { get; set; }
        public int Summary { get; set; }
        public int ChapterTitle { get; set; }
        public int Industry { get; set; }
        public int Timeframe { get; set; }
        public int Context { get; set; }
        public int Stakeholders { get; set; }
        public int KeyIssues { get; set; }
        public int Solutions { get; set; }
        public int KeyInsights { get; set; }
        public int Recommendations { get; set; }
        public int DataStartRow { get; set; }
    }
}
