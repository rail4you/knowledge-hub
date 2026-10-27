using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace KnowledgeHub.Certificates;

/// <summary>
/// 平台级结业证书模板。
///
/// <para>
/// PRD 第 9 节「证书模板」：<b>由平台侧统一定义</b>（平台管理员在后台配置模板：
/// 校徽位、学员姓名、课程名称、结业时间、证书编号、防伪二维码等字段），
/// <b>院校 / 老师不可自定义</b>。因此本实体刻意<b>不</b>实现 <c>IMultiTenant</c>：
/// 它是宿主级（host）全局模板，所有院校共用。
/// </para>
///
/// <para>
/// 模板图片由管理员上传（OSS），<see cref="LayersJson"/> 保存占位符图层配置
/// （姓名 / 课程名称 / 结业时间 / 证书编号 / 防伪码等），前端发证时按图层把真实数据
/// 合成到图片上并回传图片 URL。
/// </para>
/// </summary>
public class CertificateTemplate : FullAuditedAggregateRoot<Guid>
{
    public const int MaxNameLength = 128;
    public const int MaxImageUrlLength = 1000;

    /// <summary>模板名称，如「教学资源库结业证书」。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>证书底图 URL。</summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>占位符图层配置（JSON 数组）。</summary>
    public string? LayersJson { get; set; }

    /// <summary>是否为默认模板（发证时默认选中）。</summary>
    public bool IsDefault { get; set; }

    /// <summary>是否启用。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>排序值，越小越靠前。</summary>
    public int SortOrder { get; set; }

    /// <summary>备注（说明适用场景，如「适用于校级共享库 / 校际共建库」）。</summary>
    public string? Description { get; set; }

    public CertificateTemplate()
    {
    }

    public CertificateTemplate(Guid id, string name, string imageUrl)
        : base(id)
    {
        Name = name;
        ImageUrl = imageUrl;
    }

    public void SetName(string name)
    {
        Name = Volo.Abp.Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: MaxNameLength);
    }

    public void SetImageUrl(string imageUrl)
    {
        ImageUrl = Volo.Abp.Check.NotNullOrWhiteSpace(imageUrl, nameof(imageUrl), maxLength: MaxImageUrlLength);
    }

    public void SetLayersJson(string? layersJson)
    {
        LayersJson = layersJson;
    }

    public void SetIsDefault(bool isDefault)
    {
        IsDefault = isDefault;
    }

    public void SetIsEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }

    public void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }

    public void SetDescription(string? description)
    {
        Description = description;
    }
}
