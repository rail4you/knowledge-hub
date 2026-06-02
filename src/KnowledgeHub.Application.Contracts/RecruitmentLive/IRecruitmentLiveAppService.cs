using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.RecruitmentLive.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.RecruitmentLive;

public interface IRecruitmentLiveAppService : IApplicationService
{
    // ── 教师端 ──
    /// <summary>获取教师创建的直播列表</summary>
    Task<PagedResultDto<RecruitmentLiveDto>> GetTeacherLivesAsync(PagedRecruitmentLiveRequestDto input);
    /// <summary>创建直播</summary>
    Task<RecruitmentLiveDto> CreateLiveAsync(CreateRecruitmentLiveDto input);
    /// <summary>编辑直播</summary>
    Task<RecruitmentLiveDto> UpdateLiveAsync(Guid id, UpdateRecruitmentLiveDto input);
    /// <summary>取消直播</summary>
    Task CancelLiveAsync(Guid id);
    /// <summary>删除直播</summary>
    Task DeleteLiveAsync(Guid id);
    /// <summary>获取进入直播间的 WebSocket 令牌（一次性，30秒过期）</summary>
    Task<WsTokenDto> GetWebSocketTokenAsync(Guid liveId);

    // ── 学生端 ──
    /// <summary>获取分配给学生的直播列表</summary>
    Task<PagedResultDto<RecruitmentLiveDto>> GetStudentLivesAsync(PagedRecruitmentLiveRequestDto input);

    // ── 通用 ──
    /// <summary>获取直播详情</summary>
    Task<RecruitmentLiveDto> GetLiveAsync(Guid id);
    /// <summary>获取租户内学生列表（供选择学生用）</summary>
    Task<List<UserBriefDto>> GetTenantStudentsAsync(string? filter);
    /// <summary>获取 ICE 服务器配置</summary>
    Task<List<IceServerDto>> GetIceServersAsync();
}
