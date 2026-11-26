using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

/// <summary>
/// 视频时间轴知识点标记：第 <see cref="PointSec"/> 秒处是哪个知识点。
/// <para>用途：学生点标记直接 seek 到该处；标记旁「练一练」直达该知识点的习题。</para>
/// </summary>
public class VideoKnowledgeMark : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CourseVideoId { get; set; }

    /// <summary>打点位置（秒，0 基）。</summary>
    public int PointSec { get; set; }

    /// <summary>标记文案（如「什么是背包问题」）。</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>关联知识点（可空）。</summary>
    public Guid? KnowledgeResourceId { get; set; }

    /// <summary>备注（这一段讲了什么，进入 AI 摘要 / 复习提示）。</summary>
    public string? Note { get; set; }

    private VideoKnowledgeMark() { }

    public VideoKnowledgeMark(Guid id, Guid courseVideoId, int pointSec, string label) : base(id)
    {
        CourseVideoId = courseVideoId;
        PointSec = pointSec;
        Label = label;
    }
}
