using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.Practicums.Enums;
using KnowledgeHub.Practicums.Simulations;
using KnowledgeHub.Practicums.WasmMirrors;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Practicums;

[Authorize]
public class PracticumSimulationAppService : KnowledgeHubAppService, IPracticumSimulationAppService
{
    private readonly IRepository<PracticumSimulation, Guid> _simulationRepository;
    private readonly IRepository<PracticumProject, Guid> _projectRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IOptions<WasmMirrorOptions> _options;
    private readonly IHostEnvironment _environment;

    public PracticumSimulationAppService(
        IRepository<PracticumSimulation, Guid> simulationRepository,
        IRepository<PracticumProject, Guid> projectRepository,
        IFileStorageService fileStorageService,
        IOptions<WasmMirrorOptions> options,
        IHostEnvironment environment)
    {
        _simulationRepository = simulationRepository;
        _projectRepository = projectRepository;
        _fileStorageService = fileStorageService;
        _options = options;
        _environment = environment;
    }

    public async Task<List<PracticumSimulationDto>> GetListByProjectAsync(Guid projectId)
    {
        var query = await _simulationRepository.GetQueryableAsync();
        query = query.Where(x => x.ProjectId == projectId);
        if (!await CanManageAsync())
        {
            query = query.Where(x => x.Status == PracticumSimulationStatus.Ready);
        }

        var items = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.CreationTime)
            .ToListAsync();
        return items.Select(MapDto).ToList();
    }

    public async Task<List<PracticumSimulationDto>> GetAllAsync()
    {
        var query = await _simulationRepository.GetQueryableAsync();
        if (!await CanManageAsync())
        {
            query = query.Where(x => x.Status == PracticumSimulationStatus.Ready);
        }

        var items = await query
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.CreationTime)
            .ToListAsync();
        return items.Select(MapDto).ToList();
    }

    [Authorize(KnowledgeHubPermissions.Practicum.Create)]
    public async Task<PracticumSimulationDto> CreateAsync(CreatePracticumSimulationDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 256)
        {
            throw new UserFriendlyException("仿真名称不能为空且不能超过 256 个字符。");
        }

        if (!await _projectRepository.AnyAsync(x => x.Id == input.ProjectId))
        {
            throw new UserFriendlyException("未找到实训项目。");
        }

        ValidateCoverUrl(input.CoverUrl);
        var zipPath = ResolveUploadedZipPath(input.UploadedFilePath);
        if (!File.Exists(zipPath))
        {
            throw new UserFriendlyException("上传的 ZIP 文件不存在或已失效。");
        }

        var slug = CreateSlug(input.Name);
        var root = ResolveMirrorRoot();
        Directory.CreateDirectory(root);
        var destination = Path.Combine(root, slug);
        var staging = Path.Combine(root, $".{slug}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);

        try
        {
            await ExtractZipSafelyAsync(zipPath, staging, root);
            var entryPath = LocateEntryPath(staging, input.EntryPath);
            var files = Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories).ToList();
            var fileCount = files.Count;
            var totalBytes = files.Sum(file => new FileInfo(file).Length);
            var hasWasm = files.Any(file =>
                string.Equals(Path.GetExtension(file), ".unityweb", StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetExtension(file), ".wasm", StringComparison.OrdinalIgnoreCase));

            Directory.Move(staging, destination);

            var entity = new PracticumSimulation(GuidGenerator.Create(), input.ProjectId, input.Name.Trim(), slug)
            {
                TenantId = CurrentTenant.Id,
                EntryPath = entryPath,
                Description = TrimToLength(input.Description, 1000),
                CoverUrl = input.CoverUrl?.Trim(),
                Status = hasWasm ? PracticumSimulationStatus.Ready : PracticumSimulationStatus.Invalid,
                FileCount = fileCount,
                TotalBytes = totalBytes,
                CoopCoepRequired = true
            };

            await _simulationRepository.InsertAsync(entity, autoSave: true);
            await _fileStorageService.DeleteAsync(input.UploadedFilePath);
            return MapDto(entity);
        }
        catch
        {
            DeleteDirectoryIfExists(staging);
            DeleteDirectoryIfExists(destination);
            throw;
        }
        finally
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
    }

    [Authorize(KnowledgeHubPermissions.Practicum.Edit)]
    public async Task<PracticumSimulationDto> UpdateAsync(Guid id, UpdatePracticumSimulationDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 256)
        {
            throw new UserFriendlyException("仿真名称不能为空且不能超过 256 个字符。");
        }

        ValidateCoverUrl(input.CoverUrl);
        var entity = await _simulationRepository.GetAsync(id);
        entity.Name = input.Name.Trim();
        entity.Description = TrimToLength(input.Description, 1000);
        entity.CoverUrl = input.CoverUrl?.Trim();
        entity.SortOrder = input.SortOrder;
        await _simulationRepository.UpdateAsync(entity, autoSave: true);
        return MapDto(entity);
    }

    [Authorize(KnowledgeHubPermissions.Practicum.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        var entity = await _simulationRepository.GetAsync(id);
        var root = ResolveMirrorRoot();
        var directory = Path.Combine(root, entity.Slug);
        DeleteDirectoryIfExists(directory);
        await _simulationRepository.DeleteAsync(entity, autoSave: true);
    }

    private async Task<bool> CanManageAsync()
    {
        return await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Practicum.Create)
            || await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Practicum.Edit)
            || await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Practicum.Review);
    }

    private PracticumSimulationDto MapDto(PracticumSimulation entity)
    {
        return new PracticumSimulationDto
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            Name = entity.Name,
            Slug = entity.Slug,
            EntryPath = entity.EntryPath,
            Description = entity.Description,
            CoverUrl = entity.CoverUrl,
            Status = entity.Status,
            FileCount = entity.FileCount,
            TotalBytes = entity.TotalBytes,
            CoopCoepRequired = entity.CoopCoepRequired,
            SortOrder = entity.SortOrder,
            PublicUrl = BuildPublicUrl(entity.Slug, entity.EntryPath),
            CreationTime = entity.CreationTime
        };
    }

    private string ResolveUploadedZipPath(string uploadedFilePath)
    {
        if (string.IsNullOrWhiteSpace(uploadedFilePath) || Path.IsPathRooted(uploadedFilePath))
        {
            throw new UserFriendlyException("上传文件路径无效。");
        }

        var relativePath = uploadedFilePath.Replace('\\', '/');
        if (relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(x => x == ".."))
        {
            throw new UserFriendlyException("上传文件路径无效。");
        }

        if (!string.Equals(Path.GetExtension(relativePath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException("仿真构建必须上传 ZIP 文件。");
        }

        var storageRoot = Path.GetFullPath(_fileStorageService.RootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(_fileStorageService.RootPath, relativePath));
        if (!fullPath.StartsWith(storageRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException("上传文件路径无效。");
        }

        return fullPath;
    }

    private string ResolveMirrorRoot()
    {
        var raw = _options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new UserFriendlyException("WASM 镜像存储目录未配置。");
        }

        var root = Path.IsPathRooted(raw)
            ? Path.GetFullPath(raw)
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, raw));
        var segments = root.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (segments.Any(x => x == ".."))
        {
            throw new UserFriendlyException("WASM 镜像存储目录配置无效。");
        }

        return root;
    }

    private static async Task ExtractZipSafelyAsync(string zipPath, string destination, string root)
    {
        var destinationRoot = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var mirrorRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!destinationRoot.StartsWith(mirrorRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException("WASM 解压目录无效。");
        }

        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            var normalizedName = entry.FullName.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                continue;
            }

            var segments = normalizedName.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (Path.IsPathRooted(normalizedName) || segments.Any(x => x == ".."))
            {
                throw new UserFriendlyException("ZIP 文件包含不安全的路径。");
            }

            var relativePath = string.Join(Path.DirectorySeparatorChar, segments);
            var fullPath = Path.GetFullPath(Path.Combine(destination, relativePath));
            if (!fullPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFriendlyException("ZIP 文件包含不安全的路径。");
            }

            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
            {
                Directory.CreateDirectory(fullPath);
                continue;
            }

            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            await using var source = entry.Open();
            await using var target = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await source.CopyToAsync(target);
        }
    }

    private static string LocateEntryPath(string root, string? requestedEntryPath)
    {
        if (!string.IsNullOrWhiteSpace(requestedEntryPath))
        {
            var requested = NormalizeRelativePath(requestedEntryPath);
            var requestedFullPath = Path.GetFullPath(Path.Combine(root, requested));
            var rootPrefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!requestedFullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(requestedFullPath)
                || !string.Equals(Path.GetExtension(requestedFullPath), ".html", StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFriendlyException("指定的入口 HTML 不存在。");
            }

            return requested;
        }

        var htmlFiles = Directory.EnumerateFiles(root, "*.html", SearchOption.AllDirectories)
            .OrderBy(file => file.Length)
            .ThenBy(file => file, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var index = htmlFiles.FirstOrDefault(file =>
            string.Equals(Path.GetFileName(file), "index.html", StringComparison.OrdinalIgnoreCase));
        if (index == null && htmlFiles.Count != 1)
        {
            throw new UserFriendlyException("ZIP 中必须包含 index.html，或只能包含一个 HTML 入口文件。");
        }

        var entry = index ?? htmlFiles[0];
        var rootPath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetRelativePath(rootPath, entry).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Trim().Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (normalized.StartsWith('/') || segments.Any(x => x == ".."))
        {
            throw new UserFriendlyException("入口路径无效。");
        }

        return string.Join(Path.DirectorySeparatorChar, segments);
    }

    private static string CreateSlug(string name)
    {
        var builder = new StringBuilder();
        var pendingSeparator = false;
        foreach (var character in name.Trim().ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(character);
                pendingSeparator = false;
            }
            else
            {
                pendingSeparator = true;
            }
        }

        var baseSlug = builder.ToString().Trim('-');
        if (baseSlug.Length == 0)
        {
            baseSlug = "simulation";
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var prefix = baseSlug[..Math.Min(baseSlug.Length, 119)].Trim('-');
        return $"{prefix}-{suffix}";
    }

    private string BuildPublicUrl(string slug, string entryPath)
    {
        var publicBasePath = string.IsNullOrWhiteSpace(_options.Value.PublicBasePath)
            ? "/wasm"
            : "/" + _options.Value.PublicBasePath.Trim('/');
        var path = string.Join('/', entryPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
        return $"{publicBasePath}/{Uri.EscapeDataString(slug)}/{path}";
    }

    private static string? TrimToLength(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed[..Math.Min(trimmed.Length, maxLength)];
    }

    private static void ValidateCoverUrl(string? coverUrl)
    {
        if (string.IsNullOrWhiteSpace(coverUrl))
        {
            return;
        }

        if (!Uri.TryCreate(coverUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new UserFriendlyException("封面地址必须是 http 或 https URL。");
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
