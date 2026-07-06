using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.Logging;
using NPOI.XWPF.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.Search;

/// <summary>
/// 纯 .NET 文档解析服务，使用 NPOI 解析 Office 文档（docx/pptx/xlsx）。
/// </summary>
public class NpoiDocumentParserService : IDocumentExtractionService
{
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDataFilter _dataFilter;
    private readonly ILogger<NpoiDocumentParserService> _logger;

    // NPOI 支持的文件格式
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx", ".pptx", ".xlsx"
    };

    public NpoiDocumentParserService(
        IRepository<Resource, Guid> resourceRepository,
        IFileStorageService fileStorageService,
        IDataFilter dataFilter,
        ILogger<NpoiDocumentParserService> logger)
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
                _logger.LogWarning("Resource not found during extraction: {ResourceId}", resourceId);
                return new List<PageContentDto>();
            }

            var extension = resource.FileExtension?.ToLowerInvariant();
            if (!SupportedExtensions.Contains(extension ?? ""))
            {
                _logger.LogWarning("Unsupported file extension for NPOI parser: {Extension} (resource {ResourceId})", extension, resourceId);
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
                _logger.LogWarning("File not found: {FilePath}", fullPath);
                return new List<PageContentDto>();
            }

            _logger.LogInformation("NPOI parsing {Extension}: {FilePath} ({FileSize} bytes)",
                extension, fullPath, new FileInfo(fullPath).Length);

            using var stream = File.OpenRead(fullPath);
            var pages = extension switch
            {
                ".docx" => ParseDocx(stream),
                ".pptx" => ParsePptx(stream),
                ".xlsx" => ParseXlsx(stream),
                _ => new List<PageContentDto>()
            };

            _logger.LogInformation("NPOI extracted {PageCount} pages from resource {ResourceId}", pages.Count, resourceId);
            return pages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NPOI extraction failed for resource {ResourceId}", resourceId);
            return new List<PageContentDto>();
        }
    }

    private List<PageContentDto> ParseDocx(Stream stream)
    {
        var pages = new List<PageContentDto>();
        using var doc = new XWPFDocument(stream);

        var sb = new StringBuilder();
        foreach (var para in doc.Paragraphs)
        {
            var text = para.ParagraphText ?? "";
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine(text.Trim());
            }
        }

        // 处理表格
        foreach (var table in doc.Tables)
        {
            sb.AppendLine();
            foreach (var row in table.Rows)
            {
                var cells = new List<string>();
                foreach (var cell in row.GetTableCells())
                {
                    cells.Add(cell.GetText()?.Trim() ?? "");
                }
                sb.AppendLine(string.Join("\t", cells));
            }
        }

        var content = sb.ToString().Trim();
        if (!string.IsNullOrEmpty(content))
        {
            pages.Add(new PageContentDto { PageNumber = 1, Content = content });
        }

        return pages;
    }

    private List<PageContentDto> ParsePptx(Stream stream)
    {
        var pages = new List<PageContentDto>();
        using var doc = PresentationDocument.Open(stream, false);
        var presentationPart = doc.PresentationPart;
        if (presentationPart == null)
        {
            _logger.LogWarning("PPTX has no PresentationPart");
            return pages;
        }

        var presentation = presentationPart.Presentation;
        var slideParts = presentationPart.SlideParts;
        int pageNum = 0;

        foreach (var slidePart in slideParts)
        {
            pageNum++;
            var sb = new StringBuilder();
            var slide = slidePart.Slide;

            if (slide == null) continue;

            // Extract all text from the slide
            foreach (var text in slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>())
            {
                if (!string.IsNullOrWhiteSpace(text.Text))
                {
                    sb.AppendLine(text.Text.Trim());
                }
            }

            var pageContent = sb.ToString().Trim();
            if (!string.IsNullOrEmpty(pageContent))
            {
                pages.Add(new PageContentDto { PageNumber = pageNum, Content = pageContent });
            }
            else
            {
                pages.Add(new PageContentDto { PageNumber = pageNum, Content = $"[Slide {pageNum}]" });
            }
        }

        return pages;
    }

    private List<PageContentDto> ParseXlsx(Stream stream)
    {
        var pages = new List<PageContentDto>();
        using var workbook = new XSSFWorkbook(stream);

        for (int i = 0; i < workbook.NumberOfSheets; i++)
        {
            var sheet = workbook.GetSheetAt(i);
            if (sheet == null) continue;

            var sb = new StringBuilder();
            sb.AppendLine($"=== {sheet.SheetName} ===");

            var lastRowNum = sheet.LastRowNum;
            if (lastRowNum < 0)
            {
                pages.Add(new PageContentDto { PageNumber = i + 1, Content = sb.ToString().Trim() });
                continue;
            }

            for (int rowIdx = sheet.FirstRowNum; rowIdx <= lastRowNum; rowIdx++)
            {
                var row = sheet.GetRow(rowIdx);
                if (row == null) continue;

                var cells = new List<string>();
                for (int colIdx = 0; colIdx <= row.LastCellNum; colIdx++)
                {
                    var cell = row.GetCell(colIdx);
                    cells.Add(GetCellStringValue(cell as NPOI.SS.UserModel.ICell));
                }
                var rowText = string.Join("\t", cells).Trim();
                if (!string.IsNullOrEmpty(rowText))
                {
                    sb.AppendLine(rowText);
                }
            }

            pages.Add(new PageContentDto { PageNumber = i + 1, Content = sb.ToString().Trim() });
        }

        return pages;
    }

    private static string GetCellStringValue(NPOI.SS.UserModel.ICell? cell)
    {
        if (cell == null) return "";
        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue?.Trim() ?? "",
            CellType.Numeric => cell.NumericCellValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            CellType.Boolean => cell.BooleanCellValue ? "TRUE" : "FALSE",
            CellType.Formula => cell.StringCellValue?.Trim() ?? cell.NumericCellValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => ""
        };
    }
}
