using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Classroom;

/// <summary>
/// 课程讨论答疑主题（对齐 kg-edu 的 discussion）。
/// <para>
/// 定位：课程内的「提问—解答」沉淀，与白板/实训聊天（即时、易失）区分开。
/// 学生发帖 → 教师/助教回复 → 学生采纳答案后标记「已解决」，形成可检索的知识答疑库。
/// </para>
/// </summary>
public class Discussion : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CourseId { get; set; }

    /// <summary>所属章节（可选，便于按章筛选）。</summary>
    public Guid? ChapterId { get; set; }

    /// <summary>关联知识点（可选，便于从知识点直接跳到答疑）。</summary>
    public Guid? KnowledgeResourceId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public Guid AuthorId { get; set; }

    /// <summary>作者显示名快照（避免列表 N+1 查询用户表）。</summary>
    public string? AuthorName { get; set; }

    /// <summary>置顶（教师设置，列表优先展示）。</summary>
    public bool IsPinned { get; set; }

    /// <summary>已解决（学生采纳答案或教师标记）。</summary>
    public bool IsResolved { get; set; }

    /// <summary>回复数（冗余计数，列表展示用）。</summary>
    public int ReplyCount { get; set; }

    /// <summary>浏览数（打开详情时累加）。</summary>
    public int ViewCount { get; set; }

    /// <summary>最后回复时间（排序用；无回复时等于创建时间）。</summary>
    public DateTime? LastReplyAt { get; set; }

    private Discussion() { }

    public Discussion(Guid id, Guid courseId, string title, string content, Guid authorId) : base(id)
    {
        CourseId = courseId;
        Title = title;
        Content = content;
        AuthorId = authorId;
    }

    public void IncreaseView() => ViewCount++;

    public void AddReply(DateTime at)
    {
        ReplyCount++;
        LastReplyAt = at;
    }

    public void RemoveReply()
    {
        if (ReplyCount > 0)
        {
            ReplyCount--;
        }
    }
}
