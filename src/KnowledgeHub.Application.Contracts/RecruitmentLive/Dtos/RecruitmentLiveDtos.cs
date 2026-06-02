using System;
using System.Collections.Generic;
using KnowledgeHub.RecruitmentLive;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.RecruitmentLive.Dtos;

public class RecruitmentLiveDto : FullAuditedEntityDto<Guid>
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string? TeacherUserName { get; set; }
    public Guid? StudentId { get; set; }
    public string? StudentName { get; set; }
    public string? StudentUserName { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public RecruitmentLiveStatus Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public double DurationSeconds { get; set; }
    public Guid? InterviewScheduleId { get; set; }
    /// <summary>当前用户是否是该直播的参与者</summary>
    public bool IsParticipant { get; set; }
}

public class CreateRecruitmentLiveDto
{
    /// <summary>直播标题（必填）</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>直播描述</summary>
    public string? Description { get; set; }
    /// <summary>分配到学生ID（可选）</summary>
    public Guid? StudentId { get; set; }
    /// <summary>计划开始时间（可选）</summary>
    public DateTime? ScheduledAt { get; set; }
}

public class UpdateRecruitmentLiveDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? StudentId { get; set; }
    public DateTime? ScheduledAt { get; set; }
}

public class UserBriefDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class PagedRecruitmentLiveRequestDto : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public RecruitmentLiveStatus? Status { get; set; }
}

public class IceServerDto
{
    public List<string> Urls { get; set; } = [];
    public string? Username { get; set; }
    public string? Credential { get; set; }
}

public class WsTokenDto
{
    public string Token { get; set; } = string.Empty;
    public string WsUrl { get; set; } = string.Empty;
}
