using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Schools.Dtos;

namespace KnowledgeHub.Schools;

/// <summary>
/// 学校档案 AppService。
///
/// <para>
/// 需求「租户对应的学校是独立实体，需要有学校简介、学校徽标等配置项，在首页上显示学校徽标和名称，
/// 点击能跳转到学校简介」的对外契约。
/// </para>
/// </summary>
public interface ISchoolAppService
{
    /// <summary>获取当前租户的学校档案（管理端配置页）。</summary>
    Task<SchoolDto> GetCurrentAsync();

    /// <summary>更新当前租户的学校档案。</summary>
    Task<SchoolDto> UpdateCurrentAsync(UpdateSchoolDto input);

    /// <summary>公开：获取指定租户的学校简介（含专业 / 资源库 / 统计聚合）。</summary>
    Task<SchoolProfileDto> GetProfileAsync(Guid tenantId);

    /// <summary>公开：获取所有公开展示的学校列表（首页资源库卡片 / 注册页下拉）。</summary>
    Task<List<SchoolDto>> GetPublicListAsync();

    /// <summary>公开：获取平台唯一「社会学校」信息。</summary>
    Task<PublicSchoolDto> GetPublicSchoolAsync();

    /// <summary>平台配置：设置社会学校租户（仅 host 全局管理员）。</summary>
    Task<PublicSchoolDto> SetPublicSchoolAsync(Guid tenantId);

    /// <summary>平台配置：读取域名绑定的基础域名设置（仅 host 全局管理员）。</summary>
    Task<DomainSettingsDto> GetDomainSettingsAsync();

    /// <summary>平台配置：保存域名绑定的基础域名设置（仅 host 全局管理员）。</summary>
    Task<DomainSettingsDto> SetBaseDomainsAsync(SetBaseDomainsDto input);
}
