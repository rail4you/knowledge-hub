using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace KnowledgeHub.Resources.FileStorage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly IHostEnvironment _environment;
    private readonly string _rootPath;

    public LocalFileStorageService(IHostEnvironment environment)
    {
        _environment = environment;
        _rootPath = Path.Combine(_environment.ContentRootPath, "uploads");
        
        if (!Directory.Exists(_rootPath))
        {
            Directory.CreateDirectory(_rootPath);
        }
    }

    public async Task<string> SaveAsync(Stream stream, string fileName, string directory)
    {
        var path = GetFilePath(directory, fileName);
        var fullPath = Path.Combine(_rootPath, path);
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
        var chunkPath = Path.Combine(_rootPath, "chunks", uploadId);
        
        if (!Directory.Exists(chunkPath))
        {
            Directory.CreateDirectory(chunkPath);
        }

        var chunkFilePath = Path.Combine(chunkPath, $"{chunkNumber}_{fileName}");
        
        await using var outputStream = new FileStream(chunkFilePath, FileMode.Create);
        await stream.CopyToAsync(outputStream);
        
        return chunkFilePath;
    }

    public async Task<string> MergeChunksAsync(string uploadId, string fileName, string directory)
    {
        var chunkPath = Path.Combine(_rootPath, "chunks", uploadId);
        var finalPath = GetFilePath(directory, fileName);
        var fullFinalPath = Path.Combine(_rootPath, finalPath);
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
        var fullPath = Path.Combine(_rootPath, path);
        
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
        var fullPath = Path.Combine(_rootPath, path);
        
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
        var chunkPath = Path.Combine(_rootPath, "chunks", uploadId, $"{chunkNumber}_*");
        return Directory.GetFiles(Path.GetDirectoryName(chunkPath)!, Path.GetFileName(chunkPath)).Length > 0;
    }

    public string RootPath => _rootPath;
    
    public string GetFilePath(string directory, string fileName)
    {
        return string.IsNullOrEmpty(directory) ? fileName : Path.Combine(directory, fileName);
    }
}
