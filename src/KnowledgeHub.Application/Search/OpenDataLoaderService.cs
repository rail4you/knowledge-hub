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
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Application.Search;

public class OpenDataLoaderService : IDocumentExtractionService
{
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<OpenDataLoaderService> _logger;
    private static readonly string[] SupportedExtensions = { ".pdf", ".docx", ".pptx", ".xlsx", ".doc", ".ppt", ".xls", ".odt", ".ods", ".odp", ".rtf", ".csv" };

    public OpenDataLoaderService(
        IRepository<Resource, Guid> resourceRepository,
        IFileStorageService fileStorageService,
        ILogger<OpenDataLoaderService> logger)
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

            var fullPath = Path.Combine(_fileStorageService.RootPath, filePath);
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

    private static readonly string PdfParserScriptPath = Path.Combine(
        Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName
        ?? Directory.GetCurrentDirectory(),
        "utils", "pdf-parser", "run-parser.sh");

    public async Task<OpenDataLoaderResultDto> ParseDocumentAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            throw new UserFriendlyException($"File not found: {filePath}");
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"opendataloader_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var scriptPath = PdfParserScriptPath;
            if (!File.Exists(scriptPath))
            {
                throw new UserFriendlyException($"OpenDataLoader script not found at: {scriptPath}. Please ensure the script is deployed.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = scriptPath,
                Arguments = $"\"{filePath}\" \"{tempDir}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var pathEnv = startInfo.Environment["PATH"] ?? Environment.GetEnvironmentVariable("PATH") ?? "";
            if (!pathEnv.Contains("/opt/homebrew/bin") && File.Exists("/opt/homebrew/bin/soffice"))
            {
                startInfo.Environment["PATH"] = "/opt/homebrew/bin:" + pathEnv;
            }
            startInfo.Environment["SOFFICE_PATH"] = "/opt/homebrew/bin/soffice";

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
                _logger.LogError("OpenDataLoader parser failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
                throw new UserFriendlyException($"Document parsing failed: {error}");
            }

            var jsonFiles = Directory.GetFiles(tempDir, "*.json");
            if (jsonFiles.Length == 0)
            {
                _logger.LogWarning("No JSON output file generated in {TempDir}", tempDir);
                return new OpenDataLoaderResultDto();
            }

            var jsonContent = File.ReadAllText(jsonFiles[0], Encoding.UTF8);
            return ParseJsonOutput(jsonContent);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
            }
        }
    }

    private OpenDataLoaderResultDto ParseJsonOutput(string jsonContent)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            var result = new OpenDataLoaderResultDto();

            if (root.TryGetProperty("number of pages", out var pagesElement))
            {
                result.NumberOfPages = pagesElement.GetInt32();
            }

            if (root.TryGetProperty("kids", out var kidsElement) && kidsElement.ValueKind == JsonValueKind.Array)
            {
                var pageGroups = kidsElement.EnumerateArray()
                    .Where(k => k.TryGetProperty("type", out var type) && 
                               (type.GetString() == "paragraph" || type.GetString() == "heading" || type.GetString() == "table"))
                    .GroupBy(k => k.TryGetProperty("page number", out var pn) ? pn.GetInt32() : 0)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var group in pageGroups)
                {
                    var page = new OpenDataLoaderPageDto
                    {
                        Page = group.Key,
                        Text = string.Join("\n", group.Value
                            .Where(k => k.TryGetProperty("content", out var content))
                            .Select(k => k.GetProperty("content").GetString()))
                    };
                    result.Pages.Add(page);
                }
            }

            if (result.Pages.Count == 0)
            {
                result.Pages.Add(new OpenDataLoaderPageDto { Page = 1, Text = "" });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse OpenDataLoader JSON output");
            return new OpenDataLoaderResultDto();
        }
    }
}

public class OpenDataLoaderResultDto
{
    public int NumberOfPages { get; set; }
    public List<OpenDataLoaderPageDto> Pages { get; set; } = new();
}

public class OpenDataLoaderPageDto
{
    public int Page { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public string? Text { get; set; }
}
