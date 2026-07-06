using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.Search;

/// <summary>
/// 使用 PdfPig 提取 PDF 文档文本内容。
/// 替代已删除的 Java document-parser.jar。
/// </summary>
public class PdfTextExtractorService
{
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDataFilter _dataFilter;
    private readonly ILogger<PdfTextExtractorService> _logger;

    public PdfTextExtractorService(
        IRepository<Resource, Guid> resourceRepository,
        IFileStorageService fileStorageService,
        IDataFilter dataFilter,
        ILogger<PdfTextExtractorService> logger)
    {
        _resourceRepository = resourceRepository;
        _fileStorageService = fileStorageService;
        _dataFilter = dataFilter;
        _logger = logger;
    }

    public async Task<List<PageContentDto>> ExtractPagesAsync(Guid resourceId)
    {
        try
        {
            Resource? resource;
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var q = await _resourceRepository.GetQueryableAsync();
                resource = await q.Where(x => x.Id == resourceId).AsNoTracking().FirstOrDefaultAsync();
            }

            if (resource == null)
            {
                _logger.LogWarning("Resource not found: {ResourceId}", resourceId);
                return new List<PageContentDto>();
            }

            var ext = resource.FileExtension?.ToLowerInvariant();
            if (ext != ".pdf")
            {
                return new List<PageContentDto>();
            }

            var filePath = resource.FilePath;
            if (string.IsNullOrEmpty(filePath))
            {
                _logger.LogWarning("No file path for resource {ResourceId}", resourceId);
                return new List<PageContentDto>();
            }

            var fullPath = Path.Combine(_fileStorageService.RootPath, filePath);
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("PDF file not found: {FullPath}", fullPath);
                return new List<PageContentDto>();
            }

            _logger.LogInformation("Extracting PDF text via PdfPig: {FilePath}", fullPath);

            var pages = new List<PageContentDto>();
            using var pdf = PdfDocument.Open(fullPath);

            foreach (var page in pdf.GetPages())
            {
                var text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    pages.Add(new PageContentDto
                    {
                        PageNumber = (int)page.Number,
                        Content = text.Trim(),
                        Title = null
                    });
                }
            }

            _logger.LogInformation("PdfPig extracted {PageCount} pages from resource {ResourceId}", pages.Count, resourceId);
            return pages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PdfPig extraction failed for resource {ResourceId}", resourceId);
            return new List<PageContentDto>();
        }
    }
}
