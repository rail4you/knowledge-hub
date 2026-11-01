using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Review.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Review;

/// <summary>
/// 统一审核单查询与决策（审核中心页面用）。
///
/// <para>
/// 提交动作不在这里：由各业务服务（资源库 / 内容入库 / 共建任务 / 企业认证）
/// 通过 <c>IReviewWorkflow.SubmitAsync</c> 在各自的业务事务内发起，
/// 避免前端直接构造审核单造成「无对应业务对象」的孤儿单据。
/// </para>
/// </summary>
public interface IReviewTicketAppService : IApplicationService
{
    /// <summary>审核单列表（校审 / 联盟审各看自己阶段；可按目标类型与状态过滤）。</summary>
    Task<PagedResultDto<ReviewTicketDto>> GetListAsync(ReviewTicketListRequestDto input);

    /// <summary>审核单详情（含完整审核时间线）。</summary>
    Task<ReviewTicketDto> GetAsync(Guid id);

    /// <summary>通过 / 驳回 / 退回当前阶段。</summary>
    Task<ReviewTicketDto> DecideAsync(DecideReviewInput input);

    /// <summary>提交人撤回（仅待审状态、仅本人）。</summary>
    Task<ReviewTicketDto> WithdrawAsync(Guid id, string? reason = null);

    /// <summary>
    /// 驳回后重新提交（仅本人、仅已驳回状态）：轮次 +1，回到第一阶段。
    /// <paramref name="payloadJson"/> 为空时沿用原快照（建库被驳的典型场景：
    /// 库还在草稿，改的是库本身而不是提交内容）。
    /// </summary>
    Task<ReviewTicketDto> ResubmitAsync(Guid id, string? payloadJson = null);

    /// <summary>某业务对象的全部审核单（倒序），用于对象详情页展示审核历程。</summary>
    Task<List<ReviewTicketDto>> GetByTargetAsync(ReviewTargetType targetType, Guid targetId);

    /// <summary>当前用户的待办数量（按阶段权限统计），用于工作台红点。</summary>
    Task<int> GetMyPendingCountAsync();
}
