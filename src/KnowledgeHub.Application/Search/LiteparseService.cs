using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Application.Search;

public class LiteparseService : IDocumentExtractionService, ITransientDependency
{
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<LiteparseService> _logger;
    private static readonly string[] SupportedExtensions = { ".pdf", ".docx", ".pptx" };

    public LiteparseService(
        IRepository<Resource, Guid> resourceRepository,
        IFileStorageService fileStorageService,
        ILogger<LiteparseService> logger)
    {
        _resourceRepository = resourceRepository;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    public async Task<List<PageContentDto>> ExtractPagesAsync(Guid resourceId)
    {
        try
        {
            var resource = await _resourceRepository.GetAsync(resourceId);
            var extension = resource.FileExtension?.ToLowerInvariant();

            if (!SupportedExtensions.Contains(extension))
            {
                _logger.LogWarning("Unsupported file extension: {Extension} for resource {ResourceId}", extension, resourceId);
                return new List<PageContentDto>();
            }

            var filePath = resource.FilePath;
            if (string.IsNullOrEmpty(filePath))
            {
                _logger.LogWarning("No file path for resource {ResourceId}", resourceId);
                return new List<PageContentDto>();
            }

            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", filePath);
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("File not found: {FilePath}", fullPath);
                return new List<PageContentDto>();
            }

            var result = await ParseDocumentAsync(fullPath);
            return result.Pages.Select(p => new PageContentDto
            {
                PageNumber = p.Page,
                Content = p.Text ?? string.Empty,
                Title = null
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting pages from resource {ResourceId}", resourceId);
            return new List<PageContentDto>();
        }
    }

    private static readonly string PdfParserPath = Path.Combine(
        Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName 
        ?? Directory.GetCurrentDirectory(),
        "utils", "pdf-parser", "cli.js");

    public async Task<LiteparseResultDto> ParseDocumentAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            throw new UserFriendlyException($"File not found: {filePath}");
        }

        var cliPath = PdfParserPath;
        if (!File.Exists(cliPath))
        {
            _logger.LogWarning("PDF parser not found at {Path}, falling back to lit CLI", cliPath);
            return await ParseWithLitAsync(filePath, ct);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            Arguments = $"\"{cliPath}\" --format json \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null)
                outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null)
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(); } catch { }
            throw new UserFriendlyException("Document parsing timed out");
        }

        if (process.ExitCode != 0)
        {
            var error = errorBuilder.ToString();
            _logger.LogError("PDF parser failed with exit code {ExitCode}: {Error}, falling back to lit", process.ExitCode, error);
            return await ParseWithLitAsync(filePath, ct);
        }

        var output = outputBuilder.ToString();
        return JsonSerializer.Deserialize<LiteparseResultDto>(output, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new LiteparseResultDto();
    }

    private async Task<LiteparseResultDto> ParseWithLitAsync(string filePath, CancellationToken ct = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "lit",
            Arguments = $"parse --format json \"{filePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null)
                outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null)
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(); } catch { }
            throw new UserFriendlyException("Document parsing timed out");
        }

        if (process.ExitCode != 0)
        {
            var error = errorBuilder.ToString();
            _logger.LogError("Liteparse failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
            throw new Exception($"Liteparse failed: {error}");
        }

        var output = outputBuilder.ToString();
        return JsonSerializer.Deserialize<LiteparseResultDto>(output, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new LiteparseResultDto();
    }
}

public class LiteparseResultDto
{
    public List<LiteparsePageDto> Pages { get; set; } = new();
}

public class LiteparsePageDto
{
    public int Page { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public string? Text { get; set; }
    public List<LiteparseTextItemDto>? TextItems { get; set; } = new();
}

public class LiteparseTextItemDto
{
    public string Text { get; set; } = string.Empty;
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public string? FontName { get; set; }
    public float? FontSize { get; set; }
}
