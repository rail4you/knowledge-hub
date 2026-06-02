using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.RecruitmentLive;

public class RecruitmentLive : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    /// <summary>直播标题</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>直播描述</summary>
    public string? Description { get; set; }

    /// <summary>教师ID（面试官/创建者）</summary>
    public Guid TeacherId { get; set; }

    /// <summary>教师姓名（冗余）</summary>
    public string TeacherName { get; set; } = string.Empty;

    /// <summary>学生ID（候选人）</summary>
    public Guid? StudentId { get; set; }

    /// <summary>学生姓名（冗余）</summary>
    public string? StudentName { get; set; }

    /// <summary>6位房间码（唯一）</summary>
    public string RoomCode { get; set; } = string.Empty;

    /// <summary>直播状态</summary>
    public RecruitmentLiveStatus Status { get; set; } = RecruitmentLiveStatus.Waiting;

    /// <summary>计划开始时间</summary>
    public DateTime? ScheduledAt { get; set; }

    /// <summary>实际开始时间</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>结束时间</summary>
    public DateTime? EndedAt { get; set; }

    /// <summary>关联的面试安排ID（可选）</summary>
    public Guid? InterviewScheduleId { get; set; }

    protected RecruitmentLive()
    {
    }

    public RecruitmentLive(Guid id, string title, Guid teacherId, string teacherName, string roomCode)
        : base(id)
    {
        Check.NotNullOrWhiteSpace(title, nameof(title));
        Check.NotNullOrWhiteSpace(teacherName, nameof(teacherName));
        Check.NotNullOrWhiteSpace(roomCode, nameof(roomCode));

        Title = title;
        TeacherId = teacherId;
        TeacherName = teacherName;
        RoomCode = roomCode;
        Status = RecruitmentLiveStatus.Waiting;
    }

    /// <summary>分配学生</summary>
    public void AssignStudent(Guid studentId, string studentName)
    {
        Check.NotNullOrWhiteSpace(studentName, nameof(studentName));
        StudentId = studentId;
        StudentName = studentName;
    }

    /// <summary>开始直播</summary>
    public void Start()
    {
        if (Status != RecruitmentLiveStatus.Waiting)
        {
            throw new BusinessException("RecruitmentLive:CannotStart", "只有等待中的直播才能开始。");
        }
        Status = RecruitmentLiveStatus.Active;
        StartedAt = DateTime.UtcNow;
    }

    /// <summary>结束直播</summary>
    public void End()
    {
        if (Status != RecruitmentLiveStatus.Active)
        {
            throw new BusinessException("RecruitmentLive:CannotEnd", "只有进行中的直播才能结束。");
        }
        Status = RecruitmentLiveStatus.Ended;
        EndedAt = DateTime.UtcNow;
    }

    /// <summary>取消直播</summary>
    public void Cancel()
    {
        if (Status == RecruitmentLiveStatus.Active)
        {
            throw new BusinessException("RecruitmentLive:CannotCancel", "正在进行的直播不能取消，请先结束。");
        }
        if (Status == RecruitmentLiveStatus.Ended)
        {
            throw new BusinessException("RecruitmentLive:CannotCancel", "已结束的直播不能取消。");
        }
        Status = RecruitmentLiveStatus.Cancelled;
    }

    /// <summary>计算通话时长（秒）</summary>
    public double GetDurationSeconds()
    {
        if (StartedAt == null || EndedAt == null)
        {
            return 0;
        }
        return (EndedAt.Value - StartedAt.Value).TotalSeconds;
    }
}
