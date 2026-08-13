using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Resources.Conversion;

/// <summary>
/// 转换并发配置接口：查看/调整每类转换服务的并发数（默认 1 = 严格串行）。
/// 部署后可随时放开限制，无需重启。
/// </summary>
public interface IConversionConcurrencyAppService : IApplicationService
{
    /// <summary>获取所有转换服务的并发配置。</summary>
    Task<List<ConversionConcurrencyDto>> GetListAsync();

    /// <summary>调整某类服务的并发数（≥1）。</summary>
    Task<ConversionConcurrencyDto> SetMaxConcurrentAsync(string serviceName, int maxConcurrent);
}

public class ConversionConcurrencyDto
{
    /// <summary>服务名（如 preview / reprocess）。</summary>
    public string ServiceName { get; set; } = "";

    /// <summary>当前并发数。</summary>
    public int MaxConcurrent { get; set; }
}
