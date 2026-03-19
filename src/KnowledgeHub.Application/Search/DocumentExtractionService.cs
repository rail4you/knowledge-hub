using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.FileStorage;
using NPOI.XWPF.UserModel;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using Volo.Abp.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace KnowledgeHub.Application.Search;

public class DocumentExtractionService : IDocumentExtractionService
{
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<DocumentExtractionService> _logger;

    public DocumentExtractionService(
        IRepository<Resource, Guid> resourceRepository,
        IFileStorageService fileStorageService,
        ILogger<DocumentExtractionService> logger)
    {
        _resourceRepository = resourceRepository;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    public async Task<List<PageContentDto>> ExtractPagesAsync(Guid resourceId)
    {
        var resource = await _resourceRepository.GetAsync(resourceId);
        
        if (string.IsNullOrEmpty(resource.FilePath))
        {
            return new List<PageContentDto>();
        }

        var extension = resource.FileExtension?.ToLowerInvariant();
        
        try
        {
            var fileStream = await _fileStorageService.GetAsync(resource.FilePath);
            
            return extension switch
            {
                ".docx" => ExtractFromDocx(fileStream),
                ".doc" => ExtractFromDocx(fileStream),
                ".pdf" => ExtractFromPdf(fileStream),
                _ => new List<PageContentDto>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting pages from resource {ResourceId}", resourceId);
            return new List<PageContentDto>();
        }
    }

    private List<PageContentDto> ExtractFromDocx(Stream fileStream)
    {
        var pages = new List<PageContentDto>();
        
        try
        {
            using var document = new XWPFDocument(fileStream);
            var pageNumber = 1;
            var currentContent = new System.Text.StringBuilder();
            var currentTitle = new System.Text.StringBuilder();
            
            foreach (var para in document.Paragraphs)
            {
                var text = para.Text?.Trim();
                
                if (string.IsNullOrEmpty(text))
                    continue;
                
                if (para.Style?.StartsWith("Heading") == true || para.Style?.StartsWith("1") == true)
                {
                    if (currentContent.Length > 0)
                    {
                        pages.Add(new PageContentDto
                        {
                            PageNumber = pageNumber++,
                            Content = currentContent.ToString().Trim(),
                            Title = currentTitle.Length > 0 ? currentTitle.ToString().Trim() : null
                        });
                        currentContent.Clear();
                        currentTitle.Clear();
                    }
                    currentTitle.Append(text);
                }
                else
                {
                    currentContent.AppendLine(text);
                }
            }
            
            if (currentContent.Length > 0)
            {
                pages.Add(new PageContentDto
                {
                    PageNumber = pageNumber,
                    Content = currentContent.ToString().Trim(),
                    Title = currentTitle.Length > 0 ? currentTitle.ToString().Trim() : null
                });
            }
            
            if (pages.Count == 0 && document.Paragraphs.Count > 0)
            {
                var allText = string.Join(Environment.NewLine, document.Paragraphs.Select(p => p.Text));
                pages.Add(new PageContentDto
                {
                    PageNumber = 1,
                    Content = allText,
                    Title = null
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting from DOCX");
        }
        
        return pages;
    }

    private List<PageContentDto> ExtractFromPdf(Stream fileStream)
    {
        var pages = new List<PageContentDto>();
        
        try
        {
            using var document = PdfReader.Open(fileStream, PdfDocumentOpenMode.InformationOnly);
            
            for (int i = 0; i < document.PageCount; i++)
            {
                var page = document.Pages[i];
                var content = page.Contents.ToString();
                
                if (!string.IsNullOrWhiteSpace(content))
                {
                    pages.Add(new PageContentDto
                    {
                        PageNumber = i + 1,
                        Content = content.Trim(),
                        Title = null
                    });
                }
            }
            
            if (pages.Count == 0)
            {
                pages.Add(new PageContentDto
                {
                    PageNumber = 1,
                    Content = "Unable to extract text from PDF",
                    Title = null
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting from PDF");
        }
        
        return pages;
    }
}
