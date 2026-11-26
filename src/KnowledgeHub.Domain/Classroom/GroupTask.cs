using System;
using KnowledgeHub.Classroom.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Classroom;

/// <summary>
/// 小组任务（教师发布 → 分组 → 每组一份提交 → 评分，组员共享分数）。
/// <para>
/// 与「课程作业」(<c>Assignments</c>) 的分工：作业是**个人**提交 + 评分细则 + 迟交扣分；
/// 小组任务是**组**维度协作产出，一个任务下若干组，每组只有一份提交。
/// </para>
/// </summary>
public class GroupTask : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CourseId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>短链 token（唯一，学生扫码进入）。</summary>
    public string Token { get; set; } = string.Empty;

    public GroupTaskStatus Status { get; set; } = GroupTaskStatus.Draft;

    public GroupMode GroupMode { get; set; } = GroupMode.RandomAuto;

    public DateTime? PublishAt { get; set; }

    /// <summary>截止时间（为空表示不设截止）。</summary>
    public DateTime? DueAt { get; set; }

    /// <summary>发起人（教师）。注意不能叫 CreatorId —— 基类 FullAuditedAggregateRoot 已有 Guid? CreatorId，
    /// 同名 Guid 属性会让 EF 模型创建直接报错。</summary>
    public Guid InitiatorId { get; set; }

    /// <summary>满分（评分上限）。</summary>
    public int MaxScore { get; set; } = 100;

    private GroupTask() { }

    public GroupTask(Guid id, Guid courseId, string title, string token, Guid creatorId) : base(id)
    {
        CourseId = courseId;
        Title = title;
        Token = token;
        InitiatorId = creatorId;
    }

    public void Publish()
    {
        Status = GroupTaskStatus.Published;
        PublishAt ??= DateTime.UtcNow;
    }

    public void Close() => Status = GroupTaskStatus.Closed;

    /// <summary>是否可提交：已发布 + 未结束 + 未过截止。</summary>
    public bool CanAcceptSubmission(DateTime now) =>
        Status == GroupTaskStatus.Published && (!DueAt.HasValue || DueAt.Value > now);
}
