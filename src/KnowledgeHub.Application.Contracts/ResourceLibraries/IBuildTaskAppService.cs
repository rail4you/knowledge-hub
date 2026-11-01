using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.ResourceLibraries;
using KnowledgeHub.ResourceLibraries.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 共建任务与库模板（V2.0 P3）。
///
/// <para>
/// 权限划分（保持"两个审核角色"，库内职责走 <c>LibraryRoleAssignment</c>）：
/// </para>
/// <list type="bullet">
/// <item><description>模板管理：<c>ResourceLibraries.ManageTemplate</c>（院校管理员 / 联盟管理员）。</description></item>
/// <item><description>建任务 / 分派 / 验收：<c>ResourceLibraries.ManageTask</c> + 库级 <c>Manager</c>。</description></item>
/// <item><description>提交交付：<c>ResourceLibraries.SubmitTask</c> + 任务协作关系或库级 <c>Contributor</c>。</description></item>
/// </list>
///
/// <para>
/// 重要语义：<b>任务验收 ≠ 资源发布</b>。交付物进库仍需走 <c>ResourcePublication</c> 准入审核。
/// </para>
/// </summary>
public interface IBuildTaskAppService : IApplicationService
{
    // ── 库模板 ──

    /// <summary>已发布模板列表（建库向导用）。</summary>
    Task<List<LibraryTemplateDto>> GetTemplatesAsync();

    Task<LibraryTemplateDto> CreateTemplateAsync(CreateLibraryTemplateDto input);

    Task<LibraryTemplateDto> PublishTemplateAsync(Guid id);

    // ── 任务模板 ──

    Task<List<BuildTaskTemplateDto>> GetTaskTemplatesAsync(Guid? libraryId);

    Task<BuildTaskTemplateDto> CreateTaskTemplateAsync(CreateBuildTaskTemplateDto input);

    // ── 共建任务 ──

    Task<PagedResultDto<BuildTaskDto>> GetTasksAsync(BuildTaskListRequestDto input);

    Task<BuildTaskDto> GetTaskAsync(Guid id);

    /// <summary>建任务（可从模板实例化交付项），建后为 <c>Draft</c>。</summary>
    Task<BuildTaskDto> CreateTaskAsync(CreateBuildTaskDto input);

    /// <summary>发布任务（协作校 / 教师可见并可提交）。</summary>
    Task<BuildTaskDto> PublishTaskAsync(Guid id);

    Task<BuildTaskDto> CloseTaskAsync(Guid id);

    /// <summary>提交交付物（会同时发起库准入审核）。</summary>
    Task<BuildTaskDeliveryDto> SubmitDeliveryAsync(SubmitBuildTaskDeliveryDto input);

    /// <summary>验收 / 退回交付物（影响进度）。</summary>
    Task<BuildTaskDeliveryDto> ReviewDeliveryAsync(ReviewBuildTaskDeliveryDto input);

    /// <summary>我的共建任务（教师工作台卡片）。</summary>
    Task<List<BuildTaskDto>> GetMyTasksAsync(BuildTaskStatus? status);
}
