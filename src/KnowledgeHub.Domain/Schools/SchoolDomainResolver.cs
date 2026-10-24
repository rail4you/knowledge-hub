using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Schools;
using KnowledgeHub.Settings;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;

namespace KnowledgeHub.Schools;

/// <summary>域名解析相关配置。</summary>
public interface ISchoolDomainOptions
{
    /// <summary>
    /// 平台基础域名列表（小写、不含端口），如 <c>school.iketong.com</c>。
    /// 学校子域名形如 <c>{slug}.{baseDomain}</c>。
    /// </summary>
    IReadOnlyList<string> GetBaseDomains();
}

/// <summary>
/// 域名配置实现。
///
/// <para>
/// 基础域名是<b>部署级</b>配置（与 <c>OpenIddict:Applications</c> 同类）。
/// 读取走 <see cref="ISettingProvider"/>：ABP 的设置值提供器链里含
/// <c>GlobalSettingValueProvider</c>（ProviderName = "G"），
/// 因此无论当前是租户上下文还是 host 上下文，都取得到写在全局的这份值 ——
/// 这与项目里已有的品牌配置（<c>KnowledgeHub.Branding.*</c>）用的是同一套读法。
/// </para>
/// </summary>
public class SchoolDomainOptions : ISchoolDomainOptions, ITransientDependency
{
    private readonly ISettingProvider _settingProvider;

    public SchoolDomainOptions(ISettingProvider settingProvider)
    {
        _settingProvider = settingProvider;
    }

    public IReadOnlyList<string> GetBaseDomains()
    {
        var raw = _settingProvider
            .GetOrNullAsync(KnowledgeHubSettings.DomainBaseDomains)
            .GetAwaiter()
            .GetResult();

        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        return raw
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => School.NormalizeDomain(x))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

/// <summary>域名 → 租户 解析服务（与 HTTP 无关的纯查询逻辑）。</summary>
public interface ISchoolDomainResolver
{
    /// <summary>解析器名称（出现在 ABP 租户解析日志的 AppliedResolvers 中）。</summary>
    const string ResolverName = "SchoolDomain";

    /// <summary>按主机名解析租户 Id；未命中返回 null。</summary>
    Task<Guid?> ResolveTenantIdAsync(string host);

    /// <summary>清空某主机名的解析缓存。</summary>
    Task InvalidateAsync(string host);
}

/// <summary>
/// 域名 → 租户 解析实现。
///
/// <para>匹配顺序：</para>
/// <list type="number">
/// <item><description>精确匹配 <see cref="School.Domain"/>（学校自有域名，如 <c>qidi-edu.cn</c>）。</description></item>
/// <item><description>子域名前缀匹配 <see cref="School.Slug"/>：主机名形如 <c>{slug}.{基础域名}</c>。</description></item>
/// </list>
///
/// <para>结果按主机名缓存（10 分钟），学校域名变更时由调用方主动失效。</para>
/// </summary>
public class SchoolDomainResolver : ISchoolDomainResolver, ITransientDependency
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    /// <summary>缓存键前缀（用原生 IDistributedCache，避免 ABP 按当前租户加前缀导致键不一致）</summary>
    private const string CacheKeyPrefix = "kh:school-domain:";

    private readonly IRepository<School, Guid> _schoolRepository;
    private readonly IDistributedCache _cache;
    private readonly ISchoolDomainOptions _options;
    private readonly IConfiguration _configuration;

    public SchoolDomainResolver(
        IRepository<School, Guid> schoolRepository,
        IDistributedCache cache,
        ISchoolDomainOptions options,
        IConfiguration configuration)
    {
        _schoolRepository = schoolRepository;
        _cache = cache;
        _options = options;
        _configuration = configuration;
    }

    public async Task<Guid?> ResolveTenantIdAsync(string host)
    {
        var normalized = School.NormalizeDomain(host);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var cacheKey = CacheKeyPrefix + normalized;
        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached != null)
        {
            // 空串表示「已缓存的无命中」，避免对未配置域名的访问反复打库
            return string.IsNullOrEmpty(cached) ? null : Guid.Parse(cached);
        }

        var tenantId = await LookupAsync(normalized);
        await _cache.SetStringAsync(
            cacheKey,
            tenantId?.ToString() ?? string.Empty,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheDuration });

        return tenantId;
    }

    public async Task InvalidateAsync(string host)
    {
        var normalized = School.NormalizeDomain(host);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            await _cache.RemoveAsync(CacheKeyPrefix + normalized);
        }
    }

    private async Task<Guid?> LookupAsync(string host)
    {
        // 平台域名（AuthServer:Authority 的主机名）永远不解析为学校：
        // 防止有人把某个学校的 Slug 设成平台域名的前缀而抢占平台入口。
        if (IsPlatformHost(host))
        {
            return null;
        }

        // ① 学校自有域名（精确匹配）
        var byDomain = await _schoolRepository.FindAsync(s => s.Domain == host);
        if (byDomain != null)
        {
            return byDomain.TenantId;
        }

        // ② 子域名前缀
        foreach (var baseDomain in _options.GetBaseDomains())
        {
            var suffix = "." + baseDomain;
            if (!host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var slug = host[..^suffix.Length];
            if (string.IsNullOrWhiteSpace(slug) || slug.Contains('.'))
            {
                // 多级子域名（a.b.base）不按 slug 处理，避免误命中
                continue;
            }

            var bySlug = await _schoolRepository.FindAsync(s => s.Slug == slug);
            if (bySlug != null)
            {
                return bySlug.TenantId;
            }
        }

        return null;
    }

    /// <summary>主机名是否就是平台自身的域名（AuthServer:Authority 的主机名）。</summary>
    private bool IsPlatformHost(string host)
    {
        var authority = _configuration["AuthServer:Authority"];
        if (string.IsNullOrWhiteSpace(authority))
        {
            return false;
        }

        var authorityHost = School.NormalizeDomain(authority);
        return !string.IsNullOrWhiteSpace(authorityHost)
               && string.Equals(authorityHost, host, StringComparison.OrdinalIgnoreCase);
    }
}
