using System;
using KnowledgeHub.Learning.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Learning;

/// <summary>
/// 学生行为日志（学习画像 / 推荐 / 教师端学情时间线的原始事实）。
/// <para>
/// 只追加不修改；写入必须「失败不影响主流程」（见 <c>IStudentActivityLogger</c>）。
/// </para>
/// </summary>
public class StudentActivityLog : CreationAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid StudentId { get; set; }

    public Guid? CourseId { get; set; }

    public StudentActivityAction ActionType { get; set; }

    /// <summary>资源类型（chapter / exercise / exam / assignment / video / check-in / discussion / group-task / practice）。</summary>
    public string? ResourceType { get; set; }

    public Guid? ResourceId { get; set; }

    /// <summary>扩展信息（JSON）。</summary>
    public string? Metadata { get; set; }

    private StudentActivityLog() { }

    public StudentActivityLog(Guid id, Guid studentId, StudentActivityAction actionType) : base(id)
    {
        StudentId = studentId;
        ActionType = actionType;
    }
}
