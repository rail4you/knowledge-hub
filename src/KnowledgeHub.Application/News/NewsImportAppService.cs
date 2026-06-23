using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.News.Dtos;
using KnowledgeHub.News.Enums;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.News;

[Authorize(KnowledgeHubPermissions.News.Create)]
[IgnoreAntiforgeryToken]
public class NewsImportAppService : KnowledgeHubAppService, INewsImportAppService
{
    private readonly IRepository<NewsArticle, Guid> _articleRepository;
    private readonly IRepository<NewsCategory, Guid> _categoryRepository;

    public NewsImportAppService(
        IRepository<NewsArticle, Guid> articleRepository,
        IRepository<NewsCategory, Guid> categoryRepository)
    {
        _articleRepository = articleRepository;
        _categoryRepository = categoryRepository;
    }

    // 模板表头（与 ImportAsync 中读取的列顺序严格对应）
    private static readonly string[] TemplateHeaders =
    {
        "分类(可选)", "标题(必填)", "摘要(可选)", "正文(必填)", "封面图URL(可选)", "标签(可选)"
    };

    public async Task<NewsImportResultDto> ImportAsync(IFormFile file)
    {
        var result = new NewsImportResultDto();

        using var stream = new System.IO.MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;
        using var workbook = new XLWorkbook(stream);

        var worksheet = workbook.Worksheet(1);
        if (worksheet == null)
        {
            throw new UserFriendlyException("Excel文件中没有工作表");
        }

        // P1-13 修复：表头校验，不匹配则提示使用最新模板
        var headerRowNumber = ValidateTemplateHeader(worksheet);

        var rows = worksheet.RangeUsed()?.RowsUsed().Skip(headerRowNumber); // Skip header row
        if (rows == null)
        {
            return result;
        }

        // Pre-load category name -> id mapping
        var categories = await _categoryRepository.GetListAsync();
        var categoryMap = categories
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .ToDictionary(c => c.Name.Trim(), c => c.Id);

        var rowNumber = headerRowNumber + 1;
        foreach (var row in rows)
        {
            try
            {
                var categoryName = row.Cell(1).GetString().Trim();
                var title = row.Cell(2).GetString().Trim();
                var summary = row.Cell(3).GetString().Trim();
                var content = row.Cell(4).GetString().Trim();
                var coverImageUrl = row.Cell(5).GetString().Trim();
                var tags = row.Cell(6).GetString().Trim();

                if (string.IsNullOrWhiteSpace(title))
                {
                    result.FailCount++;
                    result.FailItems.Add(new NewsImportFailItemDto
                    {
                        RowNumber = rowNumber,
                        Reason = "标题不能为空"
                    });
                    rowNumber++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    result.FailCount++;
                    result.FailItems.Add(new NewsImportFailItemDto
                    {
                        RowNumber = rowNumber,
                        Title = title,
                        Reason = "正文内容不能为空"
                    });
                    rowNumber++;
                    continue;
                }

                // Resolve category
                Guid? categoryId = null;
                if (!string.IsNullOrWhiteSpace(categoryName))
                {
                    if (categoryMap.TryGetValue(categoryName, out var catId))
                    {
                        categoryId = catId;
                    }
                }

                var article = new NewsArticle(
                    GuidGenerator.Create(),
                    categoryId ?? Guid.Empty,
                    title,
                    content)
                {
                    Summary = summary,
                    CoverImageUrl = coverImageUrl,
                    Tags = tags,
                    IsTop = false,
                    IsHot = false,
                    AllowComments = true,
                    Status = NewsArticleStatus.Draft,
                    AuthorId = CurrentUser.Id,
                    TenantId = CurrentTenant.Id
                };

                await _articleRepository.InsertAsync(article);
                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailCount++;
                result.FailItems.Add(new NewsImportFailItemDto
                {
                    RowNumber = rowNumber,
                    Reason = ex.Message
                });
            }

            rowNumber++;
        }

        result.TotalCount = result.SuccessCount + result.FailCount;
        return result;
    }

    [HttpGet]
    public Task<IRemoteStreamContent> DownloadTemplateAsync()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("资讯导入模板");

        // 第 1 行：标题
        worksheet.Cell(1, 1).Value = "KnowledgeHub 资讯批量导入模板";
        worksheet.Range(1, 1, 1, TemplateHeaders.Length).Merge();
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;
        worksheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(30, 108, 232);
        worksheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;
        worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // 第 2 行：说明
        worksheet.Cell(2, 1).Value = "分类必须与已有分类名一致（留空则归到未分类）；标题/正文必填。导入前请删除示例行。";
        worksheet.Range(2, 1, 2, TemplateHeaders.Length).Merge();
        worksheet.Cell(2, 1).Style.Font.Italic = true;
        worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;
        worksheet.Cell(2, 1).Style.Alignment.WrapText = true;

        // 第 3 行：表头
        for (var i = 0; i < TemplateHeaders.Length; i++)
        {
            var cell = worksheet.Cell(3, i + 1);
            cell.Value = TemplateHeaders[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(232, 244, 255);
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // 第 4 行：示例
        worksheet.Cell(4, 1).Value = "校园公告"; // 分类
        worksheet.Cell(4, 2).Value = "示例标题：2025 年秋季运动会开幕"; // 标题
        worksheet.Cell(4, 3).Value = "一句话摘要"; // 摘要
        worksheet.Cell(4, 4).Value = "正文内容，请写完整的资讯正文……"; // 正文
        worksheet.Cell(4, 5).Value = "https://example.com/cover.jpg"; // 封面
        worksheet.Cell(4, 6).Value = "校园,公告,运动会"; // 标签
        for (var i = 1; i <= TemplateHeaders.Length; i++)
        {
            worksheet.Cell(4, i).Style.Font.FontColor = XLColor.Gray;
            worksheet.Cell(4, i).Style.Font.Italic = true;
        }

        worksheet.Columns().AdjustToContents();
        worksheet.Row(2).Height = 30;

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return Task.FromResult<IRemoteStreamContent>(
            new RemoteStreamContent(
                stream,
                $"资讯导入模板_{Clock.Now:yyyyMMdd}.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
    }

    /// <summary>
    /// 校验表头并返回表头所在行号（数据从 headerRow + 1 开始）。
    /// 模板第 1-2 行为标题/说明，第 3 行为表头；也兼容表头在第 1 行的旧格式。
    /// </summary>
    private static int ValidateTemplateHeader(IXLWorksheet worksheet)
    {
        for (var row = 1; row <= 3; row++)
        {
            var firstCell = worksheet.Cell(row, 1).GetString().Trim();
            if (firstCell == TemplateHeaders[0])
            {
                var matched = true;
                for (var i = 1; i < TemplateHeaders.Length; i++)
                {
                    if (worksheet.Cell(row, i + 1).GetString().Trim() != TemplateHeaders[i])
                    {
                        matched = false;
                        break;
                    }
                }
                if (matched) return row;
            }
        }

        throw new UserFriendlyException(
            "Excel 表头与最新模板不匹配，请点击「下载导入模板」获取最新模板。期望第 1 列为「" + TemplateHeaders[0] + "」。");
    }
}
