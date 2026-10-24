using System;
using System.Threading.Tasks;
using KnowledgeHub.Schools;
using Microsoft.AspNetCore.Http;
using Volo.Abp.DependencyInjection;

namespace KnowledgeHub.Schools;

/// <summary>
/// 当前请求是否落在「学校专属域名」上。
///
/// <para><b>为什么不能用 <c>ITenantResolveResultAccessor.Result.AppliedResolvers</c> 判断：</b>
/// ABP 的 <c>TenantResolver</c> 在遍历解析链时，会把<b>每一个尝试过的</b>解析器名字
/// 都记进 <c>AppliedResolvers</c>（无论它是否真的解析出了租户）。学校域名解析器
/// 注册在链首，所以只要请求走过解析链，<c>AppliedResolvers</c> 里就一定含
/// <c>SchoolDomain</c> —— 该判断恒为真。</para>
///
/// <para>后果：平台域名下只要浏览器残留 <c>__tenant</c> cookie（登录页选机构时写入），
/// 就会被误判成「学校域名」—— 登录页错误地锁定机构、门户被错误收窄到单个租户、
/// 跨校令牌守卫也会误报。</para>
///
/// <para><b>正确做法</b>：看当前请求的 Host 到底能不能解析出某所学校
/// —— 与域名解析器 <see cref="ISchoolDomainResolver"/> 用同一套规则，
/// 不依赖 ABP 的解析链记账。</para>
/// </summary>
public interface ISchoolDomainContext
{
    /// <summary>当前请求的 Host 是否命中某所学校的专属域名（学校自有域名或 <c>{slug}.{基础域名}</c>）。</summary>
    Task<bool> IsCurrentHostSchoolDomainAsync();

    /// <summary>当前请求的 Host 命中的学校租户 Id；未命中为 null。</summary>
    Task<Guid?> GetCurrentHostTenantIdAsync();
}

/// <inheritdoc cref="ISchoolDomainContext"/>
public class SchoolDomainContext : ISchoolDomainContext, ITransientDependency
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISchoolDomainResolver _schoolDomainResolver;

    public SchoolDomainContext(
        IHttpContextAccessor httpContextAccessor,
        ISchoolDomainResolver schoolDomainResolver)
    {
        _httpContextAccessor = httpContextAccessor;
        _schoolDomainResolver = schoolDomainResolver;
    }

    public async Task<Guid?> GetCurrentHostTenantIdAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return null;
        }

        // 只取主机名（不含端口）：本地开发 qidi.localhost:4200 也能正确解析
        var host = School.NormalizeDomain(httpContext.Request.Host.Host);
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        return await _schoolDomainResolver.ResolveTenantIdAsync(host);
    }

    public async Task<bool> IsCurrentHostSchoolDomainAsync()
    {
        return await GetCurrentHostTenantIdAsync() != null;
    }
}
