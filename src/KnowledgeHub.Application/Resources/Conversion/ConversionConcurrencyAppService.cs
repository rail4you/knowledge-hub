using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace KnowledgeHub.Resources.Conversion;

/// <summary>
/// 转换并发配置：查看/调整每类转换服务的并发数。
/// 需要资源管理权限（管理员/资源负责人）。
/// </summary>
[Authorize(KnowledgeHubPermissions.Resources.Default)]
public class ConversionConcurrencyAppService : KnowledgeHubAppService, IConversionConcurrencyAppService
{
    private readonly ConversionConcurrencyManager _concurrencyManager;

    public ConversionConcurrencyAppService(ConversionConcurrencyManager concurrencyManager)
    {
        _concurrencyManager = concurrencyManager;
    }

    public Task<List<ConversionConcurrencyDto>> GetListAsync()
    {
        var result = _concurrencyManager.GetAllConfigurations()
            .Select(kv => new ConversionConcurrencyDto
            {
                ServiceName = kv.Key,
                MaxConcurrent = kv.Value
            })
            .OrderBy(x => x.ServiceName)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<ConversionConcurrencyDto> SetMaxConcurrentAsync(string serviceName, int maxConcurrent)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new Volo.Abp.UserFriendlyException("服务名不能为空");
        }

        if (maxConcurrent < 1)
        {
            throw new Volo.Abp.UserFriendlyException("并发数必须 ≥ 1");
        }

        _concurrencyManager.SetMaxConcurrent(serviceName, maxConcurrent);

        return Task.FromResult(new ConversionConcurrencyDto
        {
            ServiceName = serviceName,
            MaxConcurrent = _concurrencyManager.GetGate(serviceName).MaxConcurrent
        });
    }
}
