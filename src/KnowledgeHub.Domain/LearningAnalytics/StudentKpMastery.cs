using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>学生 × 知识点掌握度（物化结果，(StudentId, KnowledgePointId) 唯一）。</summary>
public class StudentKpMastery : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid StudentId { get; set; }

    public Guid KnowledgePointId { get; set; }

    /// <summary>掌握度 0~1</summary>
    public double Mastery { get; set; }

    /// <summary>有效作答量，过小表示「数据不足」</summary>
    public double Evidence { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    private StudentKpMastery() { }

    public StudentKpMastery(Guid id, Guid studentId, Guid knowledgePointId) : base(id)
    {
        StudentId = studentId;
        KnowledgePointId = knowledgePointId;
    }
}
