using System;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace KnowledgeHub.Schools;

/// <summary>
/// 学校 —— 与 ABP 租户 1:1 的独立实体。
///
/// <para>
/// 租户在业务上代表一所学校，本实体承载学校的「机构档案」：徽标、简介、联系方式等，
/// 与专业库资源（<c>TenantInfo</c>：人才培养方案 / 专业教学标准 / 封面轮播）是两个正交维度：
/// 前者描述「这所学校」，后者描述「这个专业库对外展示什么」。
/// </para>
///
/// <para>
/// 刻意<b>不</b>实现 <see cref="Volo.Abp.MultiTenancy.IMultiTenant"/>：
/// 学校描述的正是租户本身，若按租户过滤，门户公开接口就无法跨租户读取全部学校。
/// 因此这里是全局实体，<see cref="TenantId"/> 仅作为指向 ABP 租户的外键（唯一索引）。
/// </para>
/// </summary>
public class School : FullAuditedAggregateRoot<Guid>
{
    public const int MaxNameLength = 256;
    public const int MaxLogoUrlLength = 1000;
    public const int MaxBannerUrlLength = 1000;
    public const int MaxIntroductionLength = 8000;
    public const int MaxWebsiteLength = 500;
    public const int MaxAddressLength = 500;
    public const int MaxContactPhoneLength = 64;
    public const int MaxContactEmailLength = 256;
    public const int MaxDomainLength = 256;
    public const int MaxSlugLength = 64;

    /// <summary>关联的 ABP 租户 Id。</summary>
    public Guid TenantId { get; private set; }

    /// <summary>学校名称（展示用）。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>学校徽标 URL（首页资源库卡片与学校简介页展示）。</summary>
    public string? LogoUrl { get; private set; }

    /// <summary>学校横幅 / 封面图 URL（学校简介页头图）。</summary>
    public string? BannerUrl { get; private set; }

    /// <summary>学校简介（支持 HTML 富文本）。</summary>
    public string? Introduction { get; private set; }

    /// <summary>学校官网地址。</summary>
    public string? Website { get; private set; }

    /// <summary>学校地址。</summary>
    public string? Address { get; private set; }

    /// <summary>联系电话。</summary>
    public string? ContactPhone { get; private set; }

    /// <summary>联系邮箱。</summary>
    public string? ContactEmail { get; private set; }

    /// <summary>
    /// 学校自有域名（完整主机名，如 <c>qidi-edu.cn</c> / <c>qidi.school.iketong.com</c>）。
    /// 访问该域名时系统自动落入本租户上下文，只显示本校内容。
    /// 存储时统一规范化为小写、去掉协议前缀与端口。
    /// </summary>
    public string? Domain { get; private set; }

    /// <summary>
    /// 子域名前缀（如 <c>qidi</c>）。配合平台基础域名
    /// （设置项 <c>KnowledgeHub.Domain.BaseDomains</c>）推导为
    /// <c>{slug}.{baseDomain}</c>，用于「每校一个子域名」的轻量接入方式。
    /// </summary>
    public string? Slug { get; private set; }

    /// <summary>是否在门户（首页 / 公开资源库）公开展示。</summary>
    public bool IsPublic { get; private set; } = true;

    /// <summary>排序值，越小越靠前。</summary>
    public int SortOrder { get; private set; }

    protected School()
    {
    }

    public School(Guid id, Guid tenantId, string name)
        : base(id)
    {
        TenantId = tenantId;
        SetName(name);
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: MaxNameLength);
    }

    public void SetLogoUrl(string? logoUrl)
    {
        LogoUrl = Normalize(logoUrl, MaxLogoUrlLength);
    }

    public void SetBannerUrl(string? bannerUrl)
    {
        BannerUrl = Normalize(bannerUrl, MaxBannerUrlLength);
    }

    public void SetIntroduction(string? introduction)
    {
        Introduction = Normalize(introduction, MaxIntroductionLength);
    }

    public void SetWebsite(string? website)
    {
        Website = Normalize(website, MaxWebsiteLength);
    }

    public void SetAddress(string? address)
    {
        Address = Normalize(address, MaxAddressLength);
    }

    public void SetContactPhone(string? phone)
    {
        ContactPhone = Normalize(phone, MaxContactPhoneLength);
    }

    public void SetContactEmail(string? email)
    {
        ContactEmail = Normalize(email, MaxContactEmailLength);
    }

    /// <summary>设置学校自有域名（自动规范化）。</summary>
    public void SetDomain(string? domain)
    {
        Domain = NormalizeDomain(domain);
    }

    /// <summary>设置子域名前缀（自动规范化）。</summary>
    public void SetSlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            Slug = null;
            return;
        }

        var value = slug.Trim().ToLowerInvariant();
        // 只允许字母 / 数字 / 中划线，避免注入到 URL 结构里
        if (value.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
        {
            throw new Volo.Abp.Validation.AbpValidationException("子域名前缀只能包含字母、数字和中划线。");
        }

        Slug = Check.Length(value, nameof(slug), maxLength: MaxSlugLength);
    }

    /// <summary>
    /// 域名规范化：去协议前缀、去路径、去端口、去末尾点、转小写。
    /// 存库与解析时使用同一套规则，保证「写入什么就能用什么访问」。
    /// </summary>
    public static string? NormalizeDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return null;
        }

        var value = domain.Trim().ToLowerInvariant();

        // 去协议前缀
        var schemeIndex = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeIndex >= 0)
        {
            value = value[(schemeIndex + 3)..];
        }

        // 只取主机部分（去掉路径 / 查询串 / 片段）
        var slashIndex = value.IndexOf('/');
        if (slashIndex >= 0)
        {
            value = value[..slashIndex];
        }

        var queryIndex = value.IndexOfAny(new[] { '?', '#' });
        if (queryIndex >= 0)
        {
            value = value[..queryIndex];
        }

        // 去端口
        var portIndex = value.IndexOf(':');
        if (portIndex >= 0)
        {
            value = value[..portIndex];
        }

        // 去末尾点（FQDN 写法）
        value = value.TrimEnd('.');

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Check.Length(value, nameof(domain), maxLength: MaxDomainLength);
    }

    public void SetIsPublic(bool isPublic)
    {
        IsPublic = isPublic;
    }

    public void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Check.Length(value.Trim(), nameof(value), maxLength);
    }
}
