using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Classroom;

/// <summary>某学生在某签到场次的一条签到记录（(SessionId, StudentId) 唯一）。</summary>
public class CheckInRecord : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid SessionId { get; set; }

    public Guid StudentId { get; set; }

    public string? StudentName { get; set; }

    public DateTime CheckedInAt { get; set; } = DateTime.UtcNow;

    /// <summary>定位信息（"lat,lng" 或客户端上报的位置描述）。</summary>
    public string? Location { get; set; }

    /// <summary>扩展信息（设备 / IP / 距签到点距离等，JSON）。</summary>
    public string? Metadata { get; set; }

    private CheckInRecord() { }

    public CheckInRecord(Guid id, Guid sessionId, Guid studentId) : base(id)
    {
        SessionId = sessionId;
        StudentId = studentId;
    }
}
