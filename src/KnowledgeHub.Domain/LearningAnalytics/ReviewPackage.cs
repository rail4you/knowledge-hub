using System;
using System.Collections.Generic;
using KnowledgeHub.LearningAnalytics.Enums;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>开课前衔接复习包。</summary>
public class ReviewPackage : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid StudentId { get; set; }

    /// <summary>即将学习的课程</summary>
    public Guid UpcomingCourseId { get; set; }

    public string UpcomingCourseTitle { get; set; } = string.Empty;

    public PathStrategy Strategy { get; set; } = PathStrategy.Mixed;

    public ReviewPackageStatus Status { get; set; } = ReviewPackageStatus.Draft;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PushedAt { get; set; }

    public ICollection<ReviewPackageItem> Items { get; set; } = new List<ReviewPackageItem>();

    private ReviewPackage() { }

    public ReviewPackage(Guid id, Guid studentId, Guid upcomingCourseId) : base(id)
    {
        StudentId = studentId;
        UpcomingCourseId = upcomingCourseId;
    }
}

/// <summary>复习包条目（一个前置知识点）。</summary>
public class ReviewPackageItem : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid PackageId { get; set; }

    public Guid KnowledgePointId { get; set; }

    /// <summary>学习顺序（拓扑排序后）</summary>
    public int SortOrder { get; set; }

    /// <summary>生成时的掌握度快照</summary>
    public double Mastery { get; set; }

    /// <summary>该前置点支撑了后续课程的几个知识点（影响面）</summary>
    public int Impacts { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime? CompletedAt { get; set; }

    private ReviewPackageItem() { }

    public ReviewPackageItem(Guid id, Guid packageId, Guid knowledgePointId) : base(id)
    {
        PackageId = packageId;
        KnowledgePointId = knowledgePointId;
    }
}
