using System;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.ResourceLibraries;
using KnowledgeHub.ResourceLibraries.Dtos;
using KnowledgeHub.Review;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.Reviews;

/// <summary>
/// 教学资源库的审核落地处理器（<see cref="ReviewTargetType.Library"/>）。
///
/// <para>终审通过 / 驳回时按审核单的动作类型分别处理：</para>
/// <list type="bullet">
/// <item><description><b>Create</b>：通过 → <c>Active</c>（自动进入已发布）；驳回 → 回到 <c>Draft</c>。</description></item>
/// <item><description><b>Update</b>：通过 → 按快照应用变更并恢复到变更前状态；驳回 → 恢复变更前状态（放弃变更）。</description></item>
/// <item><description><b>Delete</b>：通过 → <c>Archived</c> + 解绑内容（不硬删）；驳回 → 恢复变更前状态。</description></item>
/// </list>
///
/// <para>
/// 所有写操作都在审核决策的同一工作单元内执行：改了库却没写审核结论、或反之，
/// 都会整笔回滚。
/// </para>
/// </summary>
public class LibraryReviewTargetHandler : IReviewTargetHandler, IReviewStageHandler, ITransientDependency
{
    private readonly IResourceLibraryRepository _libraryRepository;
    private readonly IResourceLibraryChangeApplier _changeApplier;
    private readonly IDataFilter _dataFilter;
    private readonly ILogger<LibraryReviewTargetHandler> _logger;

    public LibraryReviewTargetHandler(
        IResourceLibraryRepository libraryRepository,
        IResourceLibraryChangeApplier changeApplier,
        IDataFilter dataFilter,
        ILogger<LibraryReviewTargetHandler> logger)
    {
        _libraryRepository = libraryRepository;
        _changeApplier = changeApplier;
        _dataFilter = dataFilter;
        _logger = logger;
    }

    public ReviewTargetType TargetType => ReviewTargetType.Library;

    public async Task OnApprovedAsync(ReviewTicket ticket)
    {
        var library = await FindLibraryAsync(ticket.TargetId);
        if (library == null)
        {
            _logger.LogWarning("[LibraryReviewTargetHandler] 审核单 {TicketId} 对应的资源库 {LibraryId} 不存在，跳过落地。",
                ticket.Id, ticket.TargetId);
            return;
        }

        switch (ticket.ActionType)
        {
            case ReviewActionType.Create:
                // 建库通过 → 正式发布（联盟终审已在最后阶段完成，此处即是“全部通过”）
                library.SetStatus(ResourceLibraryStatus.Active);
                await _libraryRepository.UpdateAsync(library, autoSave: true);
                break;

            case ReviewActionType.Update:
            {
                var payload = DeserializeUpdatePayload(ticket.PayloadJson);
                if (payload?.Input != null)
                {
                    await _changeApplier.ApplyUpdateAsync(library, payload.Input, library.OwnerTenantId);
                }

                library.SetStatus(payload?.PreviousStatus ?? ResourceLibraryStatus.Active);
                await _libraryRepository.UpdateAsync(library, autoSave: true);
                break;
            }

            case ReviewActionType.Delete:
            {
                var previousStatus = DeserializePreviousStatus(ticket.PayloadJson);
                await _changeApplier.ApplyArchiveAsync(library, library.OwnerTenantId);
                _logger.LogInformation(
                    "[LibraryReviewTargetHandler] 资源库 {LibraryId} 删除审核通过 → 已归档（原状态 {Previous}）。",
                    library.Id, previousStatus);
                break;
            }

            default:
                _logger.LogWarning("[LibraryReviewTargetHandler] 未处理的审核动作 {ActionType}", ticket.ActionType);
                break;
        }
    }

    /// <summary>校审通过但还需联盟终审 → 库置「待联盟终审」（对参建校仍不可见）。</summary>
    public async Task OnStageAdvancedAsync(ReviewTicket ticket)
    {
        if (ticket.ActionType != ReviewActionType.Create && ticket.ActionType != ReviewActionType.Admit)
        {
            return;
        }

        var library = await FindLibraryAsync(ticket.TargetId);
        if (library == null)
        {
            return;
        }

        library.SetStatus(ResourceLibraryStatus.PendingLeagueReview);
        await _libraryRepository.UpdateAsync(library, autoSave: true);
    }

    public async Task OnRejectedAsync(ReviewTicket ticket)
    {
        var library = await FindLibraryAsync(ticket.TargetId);
        if (library == null)
        {
            return;
        }

        switch (ticket.ActionType)
        {
            case ReviewActionType.Create:
                // 建库被驳回：退回草稿，提交人可修改后重新提交
                library.SetStatus(ResourceLibraryStatus.Draft);
                break;

            case ReviewActionType.Update:
            case ReviewActionType.Delete:
                // 变更 / 删除被驳回：恢复变更前状态（变更本身从未应用，无需回滚业务字段）
                library.SetStatus(DeserializePreviousStatus(ticket.PayloadJson));
                break;

            default:
                return;
        }

        await _libraryRepository.UpdateAsync(library, autoSave: true);
    }

    public async Task<string?> ResolveTargetNameAsync(ReviewTicket ticket)
    {
        var library = await FindLibraryAsync(ticket.TargetId);
        return library?.Name;
    }

    /// <summary>跨租户取库：联盟终审可能审别校的库。</summary>
    /// <remarks>
    /// 必须 <c>async</c> + <c>await</c>：若写成 <c>using (Disable) { return repo.FindAsync(id); }</c>，
    /// <c>using</c> 会在 Task 真正执行前就释放过滤器，查询又回到租户过滤下，
    /// 结果就是「跨校永远查不到」。这与架构文档里记录的 <c>Members</c> 延迟加载坑同源。
    /// </remarks>
    private async Task<ResourceLibrary?> FindLibraryAsync(Guid id)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await _libraryRepository.FindAsync(id);
        }
    }

    /// <summary>Update 快照结构：<c>{ "input": {...UpdateResourceLibraryDto}, "previousStatus": 1 }</c></summary>
    private sealed class UpdatePayload
    {
        public UpdateResourceLibraryDto? Input { get; set; }
        public ResourceLibraryStatus? PreviousStatus { get; set; }
    }

    private UpdatePayload? DeserializeUpdatePayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<UpdatePayload>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[LibraryReviewTargetHandler] 解析变更快照失败：{Json}", json);
            return null;
        }
    }

    private ResourceLibraryStatus DeserializePreviousStatus(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ResourceLibraryStatus.Active;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("previousStatus", out var element))
            {
                if (element.ValueKind == JsonValueKind.Number && element.TryGetByte(out var raw))
                {
                    return (ResourceLibraryStatus)raw;
                }

                if (element.ValueKind == JsonValueKind.String &&
                    Enum.TryParse<ResourceLibraryStatus>(element.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[LibraryReviewTargetHandler] 解析原状态失败：{Json}", json);
        }

        return ResourceLibraryStatus.Active;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // 枚举用数字存：与前端 DTO 一致，且不依赖枚举名（改名不会破坏历史快照）
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };
}
