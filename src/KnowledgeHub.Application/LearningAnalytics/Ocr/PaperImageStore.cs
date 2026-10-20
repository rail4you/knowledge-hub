using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.LearningAnalytics.Ocr;

/// <summary>
/// 试卷图片本地存储（OCR 需要重复读取，且后台任务执行时请求已结束）。
/// 根目录：`LearningAnalytics:UploadRoot`，默认 `{ContentRoot}/App_Data/paper-uploads`（已加入 .gitignore）。
/// </summary>
public class PaperImageStore : ITransientDependency
{
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public PaperImageStore(IHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _configuration = configuration;
    }

    public string RootPath
    {
        get
        {
            var configured = _configuration["LearningAnalytics:UploadRoot"];
            return string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(_environment.ContentRootPath, "App_Data", "paper-uploads")
                : Path.GetFullPath(configured);
        }
    }

    public async Task<string> SaveAsync(Guid batchId, string fileName, byte[] content, CancellationToken cancellationToken = default)
    {
        if (content == null || content.Length == 0)
        {
            throw new UserFriendlyException("试卷图片内容为空");
        }

        var safeName = $"{Guid.NewGuid():N}{Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant()}";
        var relative = Path.Combine(batchId.ToString("N"), safeName);
        var fullPath = EnsureUnderRoot(Path.Combine(RootPath, relative));

        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);
        return relative.Replace('\\', '/');
    }

    public async Task<byte[]> ReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = EnsureUnderRoot(Path.Combine(RootPath, relativePath));
        if (!File.Exists(fullPath))
        {
            throw new UserFriendlyException("试卷图片文件不存在，可能已被清理，请重新上传");
        }

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    public void DeleteQuietly(string relativePath)
    {
        try
        {
            var fullPath = EnsureUnderRoot(Path.Combine(RootPath, relativePath));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch
        {
            // 清理失败不影响主流程
        }
    }

    private string EnsureUnderRoot(string fullPath)
    {
        var normalizedRoot = Path.GetFullPath(RootPath) + Path.DirectorySeparatorChar;
        var normalized = Path.GetFullPath(fullPath);
        if (!normalized.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFriendlyException("非法的试卷图片路径");
        }

        return normalized;
    }
}
