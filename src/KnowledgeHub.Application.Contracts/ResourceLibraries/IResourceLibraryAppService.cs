using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.ResourceLibraries.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 教学资源库管理端 AppService（院校管理员 / 租户管理员）。
///
/// <para>
/// 需求「院校管理员可以新建、删除资源库」「院校管理员可以分配租户内的教师到不同的私有资源库」
/// 「如果资源库是院校共建的，需要设置主学校，和关联学校」。
/// </para>
///
/// <para>
/// 教学资源库 = 租户（学校）下多个库；每个库是一系列<b>文件资源、课程、实训</b>的关联集合
/// （不含微专业、就业、咨询、双高等数据）。
/// </para>
/// </summary>
public interface IResourceLibraryAppService : IApplicationService
{
    /// <summary>获取资源库列表（当前租户可见：主建的 + 参与共建的）。</summary>
    Task<List<ResourceLibraryDto>> GetListAsync(ResourceLibraryListRequestDto input);

    /// <summary>获取单个资源库详情（含共建学校成员）。</summary>
    Task<ResourceLibraryDto> GetAsync(Guid id);

    /// <summary>创建资源库（当前租户自动成为主学校）。</summary>
    Task<ResourceLibraryDto> CreateAsync(CreateResourceLibraryDto input);

    /// <summary>更新资源库（仅主学校可操作）。</summary>
    Task<ResourceLibraryDto> UpdateAsync(Guid id, UpdateResourceLibraryDto input);

    /// <summary>删除资源库（仅主学校可操作；库内内容解除归属）。</summary>
    Task DeleteAsync(Guid id);

    /// <summary>添加关联学校（仅主学校、仅院校共建库）。</summary>
    Task<ResourceLibraryMemberDto> AddMemberAsync(Guid id, AddResourceLibraryMemberDto input);

    /// <summary>移除关联学校（仅主学校、仅院校共建库）。</summary>
    Task RemoveMemberAsync(Guid id, Guid memberTenantId);

    /// <summary>获取资源库已分配的教师列表。</summary>
    Task<List<ResourceLibraryTeacherDto>> GetTeachersAsync(Guid id);

    /// <summary>把本校教师分配到资源库。</summary>
    Task<ResourceLibraryTeacherDto> AssignTeacherAsync(Guid id, AssignResourceLibraryTeacherDto input);

    /// <summary>取消教师的资源库分配。</summary>
    Task RemoveTeacherAsync(Guid id, Guid userId);

    /// <summary>获取本校可分配的教师列表。</summary>
    Task<List<ResourceLibraryTeacherDto>> GetAssignableTeachersAsync(ResourceLibraryTeacherListRequestDto input);
}
