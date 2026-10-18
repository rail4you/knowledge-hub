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

    /// <summary>主学生 ID（兼容旧一对一）</summary>
    public Guid? StudentId { get; set; }
    /// <summary>主学生姓名（兼容旧一对一）</summary>
    public string? StudentName { get; set; }
    public string? StudentUserName { get; set; }

    /// <summary>所有参与者（教师+学生）的摘要列表</summary>
    public List<ParticipantBriefDto> Participants { get; set; } = new();

    public string RoomCode { get; set; } = string.Empty;
    public RecruitmentLiveStatus Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public DateTime? ScheduledAt { get; set; }
    public DateTime? ScheduledEndAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public double DurationSeconds { get; set; }
    public Guid? InterviewScheduleId { get; set; }

    /// <summary>直播类型：招聘直播 / 课程直播</summary>
    public LiveType Type { get; set; }

    /// <summary>关联课程ID（课程直播）</summary>
    public Guid? CourseId { get; set; }

    /// <summary>关联课程标题（课程直播）</summary>
    public string? CourseTitle { get; set; }

    /// <summary>当前用户是否是该直播的参与者</summary>
    public bool IsParticipant { get; set; }
}

public class CreateRecruitmentLiveDto
{
    /// <summary>直播类型：招聘直播（默认）/ 课程直播</summary>
    public LiveType Type { get; set; } = LiveType.Recruitment;

    /// <summary>关联课程ID（Type=Course 时必填，必须是租户内已存在的课程）</summary>
    public Guid? CourseId { get; set; }

    /// <summary>直播标题（必填）</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>直播描述</summary>
    public string? Description { get; set; }
    /// <summary>分配到学生ID列表（可多选，创建多人直播）</summary>
    public List<Guid>? StudentIds { get; set; }
    /// <summary>计划开始时间（可选）</summary>
    public DateTime? ScheduledAt { get; set; }
    /// <summary>计划结束时间（可选）。时间范围内进行的直播不会过期。</summary>
    public DateTime? ScheduledEndAt { get; set; }
}

public class UpdateRecruitmentLiveDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? StudentId { get; set; }

    /// <summary>关联课程ID（课程直播允许改绑课程，需为租户内已存在课程）</summary>
    public Guid? CourseId { get; set; }

    public DateTime? ScheduledAt { get; set; }
    public DateTime? ScheduledEndAt { get; set; }
}

/// <summary>课程简要信息（直播关联课程用）</summary>
public class CourseBriefDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Semester { get; set; }
    public string StatusText { get; set; } = string.Empty;
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

    /// <summary>按直播类型过滤：不传则不过滤（招聘直播 / 课程直播共用列表接口）</summary>
    public LiveType? Type { get; set; }
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

public class RecruitmentLiveChatMessageDto
{
    public string SenderRole { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}

public class SaveChatMessageInputDto
{
    public string Content { get; set; } = string.Empty;
}

public class ParticipantBriefDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
