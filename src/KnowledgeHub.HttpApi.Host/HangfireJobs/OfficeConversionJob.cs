using System.Threading.Tasks;
using Hangfire;
using KnowledgeHub.Resources.Conversion;

namespace KnowledgeHub.HangfireJobs;

/// <summary>
/// Hangfire 转换任务：执行一次 Office → PDF 转换。
/// 并发数由 ConversionConcurrencyManager 按服务分组控制（默认 1，可 API 调）。
/// </summary>
public class OfficeConversionJob
{
    private readonly IOfficeConversionService _officeConversionService;

    public OfficeConversionJob(IOfficeConversionService officeConversionService)
    {
        _officeConversionService = officeConversionService;
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(10 * 60)]
    public async Task ConvertAsync(string resourceId, string sourcePath)
    {
        // 服务分组 "preview"：与 /preview-pdf 同步转换共用同一并发闸门。
        await _officeConversionService.ConvertToPdfAsync(resourceId, sourcePath, "preview");
    }
}
