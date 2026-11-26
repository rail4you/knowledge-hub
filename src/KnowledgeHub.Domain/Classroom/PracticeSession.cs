using System;
using KnowledgeHub.Classroom.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Classroom;

/// <summary>
/// 课堂练习场次（教师发起 → 生成 token → 学生短链作答 → 教师实时看统计）。
/// <para>
/// 与「智学分析」的个人专项练习（<c>PracticeAppService</c>）区分：
/// 这里老师选定范围后**快照抽题**（<see cref="PracticeSessionItem"/>），
/// 场次进行中题目不再变化，全班做的是同一套题，统计才有意义。
/// </para>
/// </summary>
public class PracticeSession : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CourseId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>短链 token（唯一）。</summary>
    public string Token { get; set; } = string.Empty;

    public ClassroomPracticeStatus Status { get; set; } = ClassroomPracticeStatus.Active;

    /// <summary>限定章节（为空表示整门课程抽题）。</summary>
    public Guid? ChapterId { get; set; }

    /// <summary>限定知识点 Id 列表（JSON 数组字符串；为空表示不限）。</summary>
    public string? KnowledgeResourceIds { get; set; }

    /// <summary>抽题数量（快照后的实际题量以 Items 为准）。</summary>
    public int QuestionCount { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? EndedAt { get; set; }

    /// <summary>发起人（教师）。注意不能叫 CreatorId —— 基类 FullAuditedAggregateRoot 已有 Guid? CreatorId，
    /// 同名 Guid 属性会让 EF 模型创建直接报错。</summary>
    public Guid InitiatorId { get; set; }

    private PracticeSession() { }

    public PracticeSession(Guid id, Guid courseId, string title, string token, Guid creatorId) : base(id)
    {
        CourseId = courseId;
        Title = title;
        Token = token;
        InitiatorId = creatorId;
    }

    public void Close()
    {
        Status = ClassroomPracticeStatus.Closed;
        EndedAt = DateTime.UtcNow;
    }

    public bool IsOpen => Status == ClassroomPracticeStatus.Active;
}
