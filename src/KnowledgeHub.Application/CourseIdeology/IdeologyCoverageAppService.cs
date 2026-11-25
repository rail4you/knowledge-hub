using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.CourseIdeology.Dtos;
using KnowledgeHub.CourseIdeology.Enums;
using KnowledgeHub.Courses;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.CourseIdeology;

/// <summary>
/// 覆盖分析：章节覆盖度 / 思政元素分布 / 盲区提示，并导出 xlsx 报告（可直接当申报 / 评估材料附件）。
/// </summary>
[Authorize(KnowledgeHubPermissions.CourseIdeology.Default)]
public class IdeologyCoverageAppService : KnowledgeHubAppService, IIdeologyCoverageAppService
{
    private readonly IRepository<IdeologyCase, Guid> _caseRepository;
    private readonly IRepository<IdeologyElement, Guid> _elementRepository;
    private readonly IRepository<CourseIdeologySetting, Guid> _settingRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;

    public IdeologyCoverageAppService(
        IRepository<IdeologyCase, Guid> caseRepository,
        IRepository<IdeologyElement, Guid> elementRepository,
        IRepository<CourseIdeologySetting, Guid> settingRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository)
    {
        _caseRepository = caseRepository;
        _elementRepository = elementRepository;
        _settingRepository = settingRepository;
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
    }

    public async Task<IdeologyCoverageDto> GetAsync(Guid courseId)
    {
        var (course, cases, chapters, elements, setting) = await LoadAsync(courseId);

        var published = cases.Where(x => x.Status == IdeologyCaseStatus.Published).ToList();

        var chapterItems = chapters.Select(chapter =>
        {
            var count = published.Count(x => x.ChapterId == chapter.Id);
            return new IdeologyCoverageItemDto
            {
                Id = chapter.Id,
                Name = chapter.Title,
                CaseCount = count,
                Percent = Percent(count, published.Count),
            };
        }).ToList();

        // 课程级通用案例（未绑章节）也算覆盖，但单独一行，避免和章节混在一起。
        var generalCount = published.Count(x => !x.ChapterId.HasValue);
        if (generalCount > 0)
        {
            chapterItems.Add(new IdeologyCoverageItemDto
            {
                Id = Guid.Empty,
                Name = "课程通用（未绑章节）",
                CaseCount = generalCount,
                Percent = Percent(generalCount, published.Count),
            });
        }

        var elementItems = elements.Select(element =>
        {
            var count = published.Count(x => x.Elements.Any(e => e.ElementId == element.Id));
            return new IdeologyCoverageItemDto
            {
                Id = element.Id,
                Name = element.Name,
                CaseCount = count,
                Percent = Percent(count, published.Count),
            };
        }).ToList();

        var gaps = new List<string>();
        foreach (var chapter in chapters.Where(c => published.All(x => x.ChapterId != c.Id)))
        {
            gaps.Add($"章节「{chapter.Title}」还没有已发布的思政案例");
        }

        foreach (var element in elements.Where(e => published.All(x => x.Elements.All(el => el.ElementId != e.Id))))
        {
            gaps.Add($"思政元素「{element.Name}」还没有已发布案例覆盖");
        }

        if (published.All(x => x.SourceType != IdeologySourceType.AiGenerated))
        {
            gaps.Add("暂无 AI 生成的案例（如需快速起步，可用 AI 生成草稿后人工核验）");
        }

        return new IdeologyCoverageDto
        {
            CourseId = courseId,
            CourseTitle = course.Title,
            TotalCases = cases.Count,
            PublishedCases = published.Count,
            DraftCases = cases.Count(x => x.Status == IdeologyCaseStatus.Draft),
            ArchivedCases = cases.Count(x => x.Status == IdeologyCaseStatus.Archived),
            AiGeneratedCases = cases.Count(x => x.AiGenerated),
            IdeologyGoal = setting?.IdeologyGoal,
            Chapters = chapterItems,
            Elements = elementItems,
            Gaps = gaps,
            GeneratedAt = Clock.Now,
        };
    }

    public async Task<IRemoteStreamContent> ExportAsync(Guid courseId)
    {
        var (course, cases, chapters, elements, setting) = await LoadAsync(courseId);
        var coverage = await GetAsync(courseId);
        var elementNameMap = elements.ToDictionary(x => x.Id, x => x.Name);
        var chapterNameMap = chapters.ToDictionary(x => x.Id, x => x.Title);

        using var workbook = new XLWorkbook();

        // ── Sheet 1：总览 ──
        var overview = workbook.Worksheets.Add("总览");
        overview.Cell(1, 1).Value = "课程思政覆盖分析报告";
        overview.Cell(1, 1).Style.Font.Bold = true;
        overview.Cell(1, 1).Style.Font.FontSize = 14;
        overview.Range(1, 1, 1, 2).Merge();

        var overviewRows = new (string Label, XLCellValue Value)[]
        {
            ("课程", course.Title),
            ("课程思政总目标", setting?.IdeologyGoal ?? "（未设置）"),
            ("案例总数", coverage.TotalCases),
            ("已发布", coverage.PublishedCases),
            ("草稿", coverage.DraftCases),
            ("已下架", coverage.ArchivedCases),
            ("AI 生成案例", coverage.AiGeneratedCases),
            ("生成时间", coverage.GeneratedAt.ToString("yyyy-MM-dd HH:mm")),
        };

        var row = 3;
        foreach (var item in overviewRows)
        {
            overview.Cell(row, 1).Value = item.Label;
            overview.Cell(row, 1).Style.Font.Bold = true;
            overview.Cell(row, 2).Value = item.Value;
            row++;
        }

        if (coverage.Gaps.Count > 0)
        {
            row++;
            overview.Cell(row, 1).Value = "待补充";
            overview.Cell(row, 1).Style.Font.Bold = true;
            row++;
            foreach (var gap in coverage.Gaps)
            {
                overview.Cell(row, 1).Value = gap;
                overview.Range(row, 1, row, 2).Merge();
                row++;
            }
        }

        overview.Columns().AdjustToContents();

        // ── Sheet 2：章节覆盖 ──
        var chapterSheet = workbook.Worksheets.Add("章节覆盖");
        WriteHeader(chapterSheet, "章节", "已发布案例数", "占已发布案例比例(%)");
        var chapterRow = 2;
        foreach (var item in coverage.Chapters)
        {
            chapterSheet.Cell(chapterRow, 1).Value = item.Name;
            chapterSheet.Cell(chapterRow, 2).Value = item.CaseCount;
            chapterSheet.Cell(chapterRow, 3).Value = item.Percent;
            chapterRow++;
        }

        chapterSheet.Columns().AdjustToContents();

        // ── Sheet 3：元素分布 ──
        var elementSheet = workbook.Worksheets.Add("元素分布");
        WriteHeader(elementSheet, "思政元素", "覆盖案例数", "占已发布案例比例(%)");
        var elementRow = 2;
        foreach (var item in coverage.Elements)
        {
            elementSheet.Cell(elementRow, 1).Value = item.Name;
            elementSheet.Cell(elementRow, 2).Value = item.CaseCount;
            elementSheet.Cell(elementRow, 3).Value = item.Percent;
            elementRow++;
        }

        elementSheet.Columns().AdjustToContents();

        // ── Sheet 4：案例清单（申报材料要能逐条对上） ──
        var caseSheet = workbook.Worksheets.Add("案例清单");
        WriteHeader(caseSheet, "章节", "案例标题", "思政元素", "来源类别", "出处", "融入方式", "状态", "AI 生成", "创建人", "创建时间");
        var caseRow = 2;
        foreach (var item in cases.OrderBy(x => x.ChapterTitle).ThenByDescending(x => x.CreationTime))
        {
            caseSheet.Cell(caseRow, 1).Value = item.ChapterId.HasValue
                ? chapterNameMap.GetValueOrDefault(item.ChapterId.Value) ?? item.ChapterTitle ?? "-"
                : "课程通用";
            caseSheet.Cell(caseRow, 2).Value = item.Title;
            caseSheet.Cell(caseRow, 3).Value = string.Join("、", item.Elements
                .Select(e => elementNameMap.GetValueOrDefault(e.ElementId))
                .Where(x => !string.IsNullOrWhiteSpace(x)));
            caseSheet.Cell(caseRow, 4).Value = SourceTypeLabel(item.SourceType);
            caseSheet.Cell(caseRow, 5).Value = item.SourceText;
            caseSheet.Cell(caseRow, 6).Value = UsageMethodLabel(item.UsageMethod);
            caseSheet.Cell(caseRow, 7).Value = StatusLabel(item.Status);
            caseSheet.Cell(caseRow, 8).Value = item.AiGenerated ? "是" : "否";
            caseSheet.Cell(caseRow, 9).Value = item.CreatorName ?? "-";
            caseSheet.Cell(caseRow, 10).Value = item.CreationTime.ToString("yyyy-MM-dd HH:mm");
            caseRow++;
        }

        caseSheet.Columns().AdjustToContents();

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var fileName = $"课程思政覆盖分析_{SanitizeFileName(course.Title)}_{Clock.Now:yyyyMMddHHmm}.xlsx";
        return new RemoteStreamContent(
            stream,
            fileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    // ── 内部辅助 ────────────────────────────────────────────

    private async Task<(Course Course, List<IdeologyCase> Cases, List<Chapter> Chapters,
        List<IdeologyElement> Elements, CourseIdeologySetting? Setting)> LoadAsync(Guid courseId)
    {
        var course = await _courseRepository.FindAsync(courseId)
                     ?? throw new UserFriendlyException("课程不存在或无权访问。");

        var caseQuery = await _caseRepository.WithDetailsAsync(x => x.Elements);
        var cases = await AsyncExecuter.ToListAsync(caseQuery.Where(x => x.CourseId == courseId));

        var chapterQuery = await _chapterRepository.GetQueryableAsync();
        var chapters = await AsyncExecuter.ToListAsync(
            chapterQuery.Where(x => x.CourseId == courseId).OrderBy(x => x.SortOrder));

        var elements = await AsyncExecuter.ToListAsync(
            (await _elementRepository.GetQueryableAsync()).Where(x => x.IsEnabled).OrderBy(x => x.SortOrder));

        var settingQuery = await _settingRepository.GetQueryableAsync();
        var setting = await AsyncExecuter.FirstOrDefaultAsync(settingQuery.Where(x => x.CourseId == courseId));

        return (course, cases, chapters, elements, setting);
    }

    private static void WriteHeader(IXLWorksheet sheet, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
            sheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F3");
        }

        sheet.SheetView.FreezeRows(1);
    }

    private static decimal Percent(int count, int total)
        => total <= 0 ? 0 : Math.Round(count * 100m / total, 1);

    private static string StatusLabel(IdeologyCaseStatus status) => status switch
    {
        IdeologyCaseStatus.Draft => "草稿",
        IdeologyCaseStatus.Published => "已发布",
        IdeologyCaseStatus.Archived => "已下架",
        _ => status.ToString(),
    };

    private static string SourceTypeLabel(IdeologySourceType type) => type switch
    {
        IdeologySourceType.Textbook => "教材",
        IdeologySourceType.AuthorityMedia => "权威媒体",
        IdeologySourceType.Law => "法律法规",
        IdeologySourceType.Enterprise => "校企合作",
        IdeologySourceType.AiGenerated => "AI 生成",
        _ => "其它",
    };

    private static string UsageMethodLabel(IdeologyUsageMethod method) => method switch
    {
        IdeologyUsageMethod.Import => "导入·讨论",
        IdeologyUsageMethod.CaseAnalysis => "案例分析",
        IdeologyUsageMethod.Discussion => "课堂讨论",
        IdeologyUsageMethod.PracticeComment => "实训点评",
        IdeologyUsageMethod.ExtendedReading => "拓展阅读",
        _ => method.ToString(),
    };

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string((name ?? string.Empty).Where(c => !invalid.Contains(c)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "课程" : cleaned;
    }
}
