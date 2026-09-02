using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.RecruitmentLive;

/// <summary>
/// 招聘直播参与者（教师 + 多个学生）。
/// 一个直播可以有 1 个教师和多个学生作为参与者。
/// </summary>
public class RecruitmentLiveParticipant : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>所属直播 ID</summary>
    public Guid LiveId { get; set; }

    /// <summary>用户 ID</summary>
    public Guid UserId { get; set; }

    /// <summary>用户名称（冗余）</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>角色：teacher / student</summary>
    public string Role { get; set; } = string.Empty;

    protected RecruitmentLiveParticipant() { }

    public RecruitmentLiveParticipant(Guid id, Guid liveId, Guid userId, string userName, string role)
        : base(id)
    {
        LiveId = liveId;
        UserId = userId;
        UserName = userName;
        Role = role;
    }
}