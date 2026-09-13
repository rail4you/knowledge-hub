using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Volo.Abp;

namespace KnowledgeHub.Resources.FileStorage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IHostEnvironment _environment;
    private readonly string _rootPath;

    public LocalFileStorageService(IHostEnvironment environment, IOptions<FileStorageOptions> options)
    {
        _environment = environment;
        _rootPath = options.Value.ResolveRootPath(_environment.ContentRootPath);
        
        if (!Directory.Exists(_rootPath))
        {
            Directory.CreateDirectory(_rootPath);
        }
    }

    public async Task<string> SaveAsync(Stream stream, string fileName, string directory)
    {
        var path = GetFilePath(directory, fileName);
        var fullPath = EnsureUnderRoot(Path.Combine(_rootPath, path));
        var directoryPath = Path.GetDirectoryName(fullPath);
        
        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await using var outputStream = new FileStream(fullPath, FileMode.Create);
        await stream.CopyToAsync(outputStream);
        
        return path;
    }

    public async Task<string> SaveChunkAsync(Stream stream, string fileName, int chunkNumber, string uploadId)
    {
        if (!Guid.TryParse(uploadId, out _))
        {
            throw new BusinessException("上传会话标识非法。");
        }

        var safeName = SanitizeFileName(fileName);
        var chunkPath = EnsureUnderRoot(Path.Combine(_rootPath, "chunks", uploadId));
        
        if (!Directory.Exists(chunkPath))
        {
            Directory.CreateDirectory(chunkPath);
        }

        // 仅用服务端拼接的「序号_文件名」作为块名，文件名已去除路径分隔符
        var chunkFilePath = EnsureUnderRoot(Path.Combine(chunkPath, $"{chunkNumber}_{safeName}"));
        
        await using var outputStream = new FileStream(chunkFilePath, FileMode.Create);
        await stream.CopyToAsync(outputStream);
        
        return chunkFilePath;
    }

    public async Task<string> MergeChunksAsync(string uploadId, string fileName, string directory)
    {
        if (!Guid.TryParse(uploadId, out _))
        {
            throw new BusinessException("上传会话标识非法。");
        }

        var chunkPath = EnsureUnderRoot(Path.Combine(_rootPath, "chunks", uploadId));
        if (!Directory.Exists(chunkPath))
        {
            throw new BusinessException("上传分片不存在或已过期。");
        }

        var finalPath = GetFilePath(directory, fileName);
        var fullFinalPath = EnsureUnderRoot(Path.Combine(_rootPath, finalPath));
        var directoryPath = Path.GetDirectoryName(fullFinalPath);

        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var chunkFiles = Directory.GetFiles(chunkPath)
            .OrderBy(f =>
            {
                // 按 chunkNumber 数值排序（而非字符串），
                // 修复 "10_x" 排在 "2_x" 前面的 bug 导致合并后文件损坏
                var name = Path.GetFileNameWithoutExtension(f);
                var underscoreIndex = name.IndexOf('_');
                if (underscoreIndex > 0 && int.TryParse(name.AsSpan(0, underscoreIndex), out var num))
                    return num;
                return int.MaxValue;
            })
            .ToList();

        await using var outputStream = new FileStream(fullFinalPath, FileMode.Create, FileAccess.Write);
        
        foreach (var chunkFile in chunkFiles)
        {
            await using var inputStream = new FileStream(chunkFile, FileMode.Open);
            await inputStream.CopyToAsync(outputStream);
        }

        Directory.Delete(chunkPath, true);

        return finalPath;
    }

    public async Task<Stream> GetAsync(string path)
    {
        var fullPath = EnsureUnderRoot(Path.Combine(_rootPath, path));
        
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        var memoryStream = new MemoryStream();
        await using var fileStream = new FileStream(fullPath, FileMode.Open);
        await fileStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;
        
        return memoryStream;
    }

    public Task DeleteAsync(string path)
    {
        var fullPath = EnsureUnderRoot(Path.Combine(_rootPath, path));
        
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        
        return Task.CompletedTask;
    }

    public string GetFileUrl(string path)
    {
        return $"/uploads/{path.Replace("\\", "/")}";
    }

    public bool HasChunk(string uploadId, int chunkNumber)
    {
        if (!Guid.TryParse(uploadId, out _))
        {
            return false;
        }

        var chunkPath = Path.Combine(_rootPath, "chunks", uploadId);
        if (!Directory.Exists(chunkPath))
        {
            return false;
        }

        return Directory.GetFiles(chunkPath, $"{chunkNumber}_*").Length > 0;
    }

    public string RootPath => _rootPath;
    
    public string GetFilePath(string directory, string fileName)
    {
        var safeName = SanitizeFileName(fileName);
        return string.IsNullOrEmpty(directory) ? safeName : Path.Combine(directory, safeName);
    }

    private static string SanitizeFileName(string fileName)
    {
        // 去掉任何目录成分与路径分隔符，防止 ../ 或绝对路径穿越
        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        return string.IsNullOrWhiteSpace(name) || name == "." || name == ".."
            ? $"file_{Guid.NewGuid():N}"
            : name;
    }

    private string EnsureUnderRoot(string fullPath)
    {
        var root = Path.GetFullPath(_rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(fullPath);
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("非法的文件路径。");
        }

        return resolved;
    }
}
