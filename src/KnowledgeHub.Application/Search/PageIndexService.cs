using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;
using KnowledgeHub.Application.Contracts.Search.Dtos;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Application.Search;

public class PageIndexService : IPageIndexService, ITransientDependency
{
    private readonly IRepository<ResourcePageIndex, Guid> _pageIndexRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<ResourceVersion, Guid> _versionRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PageIndexService> _logger;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".pptx", ".xlsx"
    };

    public PageIndexService(
        IRepository<ResourcePageIndex, Guid> pageIndexRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<ResourceVersion, Guid> versionRepository,
        IFileStorageService fileStorageService,
        IConfiguration configuration,
        ILogger<PageIndexService> logger)
    {
        _pageIndexRepository = pageIndexRepository;
        _resourceRepository = resourceRepository;
        _versionRepository = versionRepository;
        _fileStorageService = fileStorageService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ResourcePageIndexDto?> GeneratePageIndexAsync(Guid resourceVersionId)
    {
        var version = await _versionRepository.GetAsync(resourceVersionId);
        if (version == null)
        {
            throw new Exception($"ResourceVersion not found: {resourceVersionId}");
        }

        var resource = await _resourceRepository.GetAsync(version.ResourceId);
        if (resource == null)
        {
            throw new Exception($"Resource not found: {version.ResourceId}");
        }

        var filePath = version.FilePath ?? resource.FilePath;
        if (string.IsNullOrEmpty(filePath))
        {
            throw new Exception("No file path available for this resource version");
        }

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (!SupportedExtensions.Contains(ext))
        {
            _logger.LogInformation("Skipping PageIndex for unsupported format: {Ext}", ext);
            return null;
        }

        var fullPath = Path.Combine(_fileStorageService.RootPath, filePath);
        if (!File.Exists(fullPath))
        {
            throw new Exception($"File not found: {fullPath}");
        }

        var apiKey = _configuration["Qwen:ApiKey"]
            ?? throw new Exception("Qwen:ApiKey is not configured");

        var outputJsonPath = Path.Combine(Path.GetTempPath(), $"pageindex_{resourceVersionId:N}.json");

        try
        {
            await RunCliAsync(fullPath, apiKey, outputJsonPath);

            if (!File.Exists(outputJsonPath))
            {
                throw new Exception($"PageIndex CLI did not produce output file: {outputJsonPath}");
            }

            var json = await File.ReadAllTextAsync(outputJsonPath);
            var jsonDoc = JsonDocument.Parse(json);

            var nodeCount = 0;
            if (jsonDoc.RootElement.TryGetProperty("structure", out var structure))
            {
                nodeCount = CountNodes(structure);
            }

            var existing = await _pageIndexRepository.FindAsync(x => x.ResourceVersionId == resourceVersionId);
            if (existing != null)
            {
                existing.PageIndexJson = json;
                existing.SourceFormat = ext.TrimStart('.').ToUpperInvariant();
                existing.NodeCount = nodeCount;
                existing.ResourceId = resource.Id;
                await _pageIndexRepository.UpdateAsync(existing);
                return MapToDto(existing);
            }
            else
            {
                var entity = new ResourcePageIndex
                {
                    ResourceId = resource.Id,
                    ResourceVersionId = resourceVersionId,
                    PageIndexJson = json,
                    SourceFormat = ext.TrimStart('.').ToUpperInvariant(),
                    NodeCount = nodeCount,
                    TenantId = resource.TenantId
                };
                await _pageIndexRepository.InsertAsync(entity);
                return MapToDto(entity);
            }
        }
        finally
        {
            try { if (File.Exists(outputJsonPath)) File.Delete(outputJsonPath); }
            catch { /* ignore cleanup errors */ }
        }
    }

    public async Task<ResourcePageIndexDto?> GetPageIndexAsync(Guid resourceId)
    {
        var pageIndexList = await _pageIndexRepository.GetListAsync(x => x.ResourceId == resourceId);
        return pageIndexList
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapToDto)
            .FirstOrDefault();
    }

    public async Task<ResourcePageIndexDto?> GetPageIndexByVersionAsync(Guid resourceVersionId)
    {
        var entity = await _pageIndexRepository.FindAsync(x => x.ResourceVersionId == resourceVersionId);
        return entity != null ? MapToDto(entity) : null;
    }

    public async Task<List<PageIndexSearchResultDto>> SearchPageIndexAsync(string query, int maxResults = 10)
    {
        var results = new List<PageIndexSearchResultDto>();
        var allPageIndices = await _pageIndexRepository.GetListAsync();
        var queryLower = query.ToLowerInvariant();

        foreach (var pageIndex in allPageIndices)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(pageIndex.PageIndexJson);
                if (!jsonDoc.RootElement.TryGetProperty("structure", out var structure)) continue;

                var resource = await _resourceRepository.FindAsync(pageIndex.ResourceId);
                var resourceName = resource?.Name;

                var docDescription = jsonDoc.RootElement.TryGetProperty("doc_description", out var desc)
                    ? desc.GetString()
                    : null;

                SearchNodes(structure.EnumerateArray(), queryLower, pageIndex.ResourceId, resourceName, docDescription, results, maxResults);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse PageIndex JSON for resource {ResourceId}", pageIndex.ResourceId);
            }

            if (results.Count >= maxResults) break;
        }

        return results.Take(maxResults).ToList();
    }

    private void SearchNodes(
        JsonElement.ArrayEnumerator nodes,
        string query,
        Guid resourceId,
        string? resourceName,
        string? docDescription,
        List<PageIndexSearchResultDto> results,
        int maxResults)
    {
        foreach (var node in nodes)
        {
            if (results.Count >= maxResults) return;

            var title = node.TryGetProperty("title", out var t) ? t.GetString() : null;
            var summary = node.TryGetProperty("summary", out var s) ? s.GetString() : null;
            var nodeId = node.TryGetProperty("node_id", out var nid) ? nid.GetString() : "";
            var startIndex = node.TryGetProperty("start_index", out var si) ? si.GetInt32() : 0;
            var endIndex = node.TryGetProperty("end_index", out var ei) ? ei.GetInt32() : 0;

            var titleMatch = title?.ToLowerInvariant().Contains(query) == true;
            var summaryMatch = summary?.ToLowerInvariant().Contains(query) == true;

            if (titleMatch || summaryMatch)
            {
                results.Add(new PageIndexSearchResultDto
                {
                    ResourceId = resourceId,
                    ResourceName = resourceName,
                    NodeTitle = title,
                    NodeSummary = summary,
                    NodeId = nodeId ?? "",
                    StartIndex = startIndex,
                    EndIndex = endIndex,
                    DocDescription = docDescription
                });
            }

            if (node.TryGetProperty("children", out var children))
            {
                SearchNodes(children.EnumerateArray(), query, resourceId, resourceName, docDescription, results, maxResults);
            }
        }
    }

    private async Task RunCliAsync(string filePath, string apiKey, string outputPath)
    {
        var model = _configuration["PageIndex:Model"] ?? "qwen-plus";
        var timeoutMinutes = _configuration["PageIndex:TimeoutMinutes"] != null
            ? int.Parse(_configuration["PageIndex:TimeoutMinutes"]!)
            : 5;
        var pythonPath = _configuration["PageIndex:PythonPath"] ?? "python3";
        var cliPath = ResolveCliPath();

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = $"\"{cliPath}\" \"{filePath}\" --qwen-api-key {apiKey} --qwen-model {model} -o \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogInformation("Running PageIndex CLI: {PythonPath} {Arguments}", pythonPath, startInfo.Arguments);

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();

        var exited = process.WaitForExit((int)TimeSpan.FromMinutes(timeoutMinutes).TotalMilliseconds);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            throw new Exception($"PageIndex CLI timed out after {timeoutMinutes} minutes");
        }

        if (process.ExitCode != 0)
        {
            _logger.LogError("PageIndex CLI failed with exit code {ExitCode}. stdout: {Stdout}, stderr: {Stderr}",
                process.ExitCode, stdout, stderr);
            throw new Exception($"PageIndex CLI failed with exit code {process.ExitCode}: {stderr}");
        }

        _logger.LogInformation("PageIndex CLI completed successfully for {FilePath}", filePath);
    }

    /// <summary>
    /// Resolve the CLI script path by trying multiple candidate locations.
    /// </summary>
    private string ResolveCliPath()
    {
        var configuredPath = _configuration["PageIndex:CliPath"] ?? "src/pageindex-cli/pageindex_cli.py";

        // Candidate paths to try (in order of priority)
        var candidates = new[]
        {
            configuredPath,                                                     // As-is from config
            Path.Combine("/Users/bai/projects/KnowledgeHub", configuredPath),   // Relative to solution root
            Path.Combine(AppContext.BaseDirectory, configuredPath),              // Relative to bin output
            Path.GetFullPath(configuredPath),                                    // Relative to CWD
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                _logger.LogDebug("Resolved PageIndex CLI path: {Path}", candidate);
                return Path.GetFullPath(candidate);
            }
        }

        throw new Exception(
            $"PageIndex CLI script not found. Tried: {string.Join(", ", candidates)}");
    }

    private static int CountNodes(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            var count = 0;
            foreach (var child in element.EnumerateArray())
            {
                count += CountNodes(child);
            }
            return count;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            var count = 1;
            if (element.TryGetProperty("children", out var children))
            {
                count += CountNodes(children);
            }
            return count;
        }

        return 0;
    }

    private static ResourcePageIndexDto MapToDto(ResourcePageIndex entity)
    {
        return new ResourcePageIndexDto
        {
            Id = entity.Id,
            ResourceId = entity.ResourceId,
            ResourceVersionId = entity.ResourceVersionId,
            PageIndexJson = entity.PageIndexJson,
            SourceFormat = entity.SourceFormat,
            Model = entity.Model,
            NodeCount = entity.NodeCount
        };
    }
}
