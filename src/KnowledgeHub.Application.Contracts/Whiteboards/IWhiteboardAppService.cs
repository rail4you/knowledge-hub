using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Whiteboards.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Whiteboards;

/// <summary>
/// 教研白板。
///
/// <para>实时协同不在这里：元素增量与光标通过 SignalR Hub
/// （<c>/signalr/whiteboards</c>）收发；本服务负责元信息、成员与「进入房间时的全量快照」。</para>
/// </summary>
public interface IWhiteboardAppService : IApplicationService
{
    /// <summary>我可见的白板（我创建 + 我被邀请）。</summary>
    Task<List<WhiteboardListItemDto>> GetListAsync();

    /// <summary>白板详情：成员 + 全部画布元素（进入房间用）。</summary>
    Task<WhiteboardDetailDto> GetAsync(Guid id);

    Task<WhiteboardDetailDto> CreateAsync(CreateWhiteboardDto input);

    Task<WhiteboardDetailDto> UpdateAsync(Guid id, UpdateWhiteboardDto input);

    Task DeleteAsync(Guid id);

    /// <summary>可邀请的教师（同租户其他教师，含是否已加入标记）。</summary>
    Task<List<InvitableTeacherDto>> GetInvitableTeachersAsync(Guid id);

    /// <summary>邀请共创教师。</summary>
    Task<List<WhiteboardMemberDto>> InviteAsync(Guid id, InviteWhiteboardMembersDto input);

    /// <summary>移除成员（仅创建者）。</summary>
    Task RemoveMemberAsync(Guid id, Guid userId);

    /// <summary>清空画布元素（仅创建者）。</summary>
    Task ClearElementsAsync(Guid id);
}
