using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Classroom;

/// <summary>
/// 小组任务提交（(GroupTaskId, GroupId) 唯一：一组一份，组内任一成员可提交/覆盖）。
/// </summary>
public class GroupTaskSubmission : FullAuditedEntity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid GroupTaskId { get; set; }

    public Guid GroupId { get; set; }

    public string? Content { get; set; }

    /// <summary>附件（指向 Resource，复用上传/预览/下载链路）。</summary>
    public Guid? AttachmentResourceId { get; set; }

    public Guid SubmittedById { get; set; }

    public string? SubmittedByName { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>分数（组员共享）。</summary>
    public int? Score { get; set; }

    public string? Feedback { get; set; }

    public DateTime? GradedAt { get; set; }

    private GroupTaskSubmission() { }

    public GroupTaskSubmission(Guid id, Guid groupTaskId, Guid groupId, Guid submittedById) : base(id)
    {
        GroupTaskId = groupTaskId;
        GroupId = groupId;
        SubmittedById = submittedById;
    }

    public void Grade(int score, string? feedback)
    {
        Score = score;
        Feedback = feedback;
        GradedAt = DateTime.UtcNow;
    }
}
