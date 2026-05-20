using System;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.News.Dtos;
using KnowledgeHub.News.Enums;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.News;

[Authorize(KnowledgeHubPermissions.News.Create)]
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

    public async Task<NewsImportResultDto> ImportAsync(byte[] excelFile)
    {
        var result = new NewsImportResultDto();

        using var stream = new System.IO.MemoryStream(excelFile);
        using var workbook = new XLWorkbook(stream);

        var worksheet = workbook.Worksheet(1);
        if (worksheet == null)
        {
            throw new UserFriendlyException("Excel文件中没有工作表");
        }

        var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1); // Skip header
        if (rows == null)
        {
            return result;
        }

        // Pre-load category name -> id mapping
        var categories = await _categoryRepository.GetListAsync();
        var categoryMap = categories
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .ToDictionary(c => c.Name.Trim(), c => c.Id);

        var rowNumber = 2;
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
                    AuthorId = CurrentUser.GetId(),
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
}
