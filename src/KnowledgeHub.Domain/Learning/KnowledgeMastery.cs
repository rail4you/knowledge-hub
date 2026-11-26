using System;
using KnowledgeHub.Learning.Enums;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Learning;

/// <summary>
/// 学生 × 课程知识点掌握度（(StudentId, KnowledgeResourceId) 唯一）。
/// <para>
/// 口径统一改造后：<see cref="Mastery"/> / <see cref="Evidence"/> 由
/// <c>CourseMasteryRecalculationService</c> 用 <c>MasteryCalculator</c>
/// （Beta 先验 + 120 天半衰期）统一写入，是**唯一可信来源**。
/// </para>
/// <para>
/// <see cref="Level"/> / <see cref="PracticeCount"/> / <see cref="CorrectCount"/> 保留为
/// 展示用冗余与历史兼容，不再单独决定掌握等级（旧实现是「练习≥3 次且正确率≥80% → 已掌握」，
/// 会因为一道题定生死，已废弃）。
/// </para>
/// </summary>
public class KnowledgeMastery : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid StudentId { get; set; }
    public Guid KnowledgeResourceId { get; set; }

    public MasteryLevel Level { get; set; } = MasteryLevel.Unlearned;
    public int PracticeCount { get; set; }
    public int CorrectCount { get; set; }
    public DateTime LastPracticeAt { get; set; }

    /// <summary>掌握度 0~1（Beta 后验均值，已做时间衰减）。</summary>
    public double Mastery { get; set; }

    /// <summary>有效证据量（越大越可信；小于 1 表示数据不足，不判薄弱）。</summary>
    public double Evidence { get; set; }

    /// <summary>最近一次重算时间。</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public decimal Accuracy => PracticeCount > 0 ? (decimal)CorrectCount / PracticeCount : 0;

    private KnowledgeMastery() { }

    public KnowledgeMastery(Guid id, Guid studentId, Guid knowledgeResourceId) : base(id)
    {
        StudentId = studentId;
        KnowledgeResourceId = knowledgeResourceId;
        Level = MasteryLevel.Unlearned;
    }

    /// <summary>
    /// 记录一次练习（仅统计口径，不再自算等级）。
    /// 掌握度请走 <c>CourseMasteryRecalculationService</c>，保证与试卷侧同一套算法。
    /// </summary>
    public void RecordPractice(bool isCorrect)
    {
        PracticeCount++;
        if (isCorrect)
        {
            CorrectCount++;
        }
        LastPracticeAt = DateTime.UtcNow;
    }

    /// <summary>写入统一算法算出的掌握度，并按判定线同步展示用等级。</summary>
    public void SetMastery(double mastery, double evidence, double? okLine = null, double? weakLine = null)
    {
        Mastery = mastery;
        Evidence = evidence;
        UpdatedAt = DateTime.UtcNow;
        Level = DeriveLevel(mastery, evidence, okLine, weakLine);
    }

    /// <summary>
    /// 展示等级映射（与 <c>MasteryCalculator</c> 判定线保持一致，默认 0.60 / 0.75）：
    /// 无证据 → 未学习；低于判定线 → 学习中；达标 → 已掌握。
    /// </summary>
    public static MasteryLevel DeriveLevel(double mastery, double evidence, double? okLine = null, double? weakLine = null)
    {
        if (evidence <= 0)
        {
            return MasteryLevel.Unlearned;
        }

        var ok = okLine ?? 0.75;
        return mastery >= ok ? MasteryLevel.Mastered : MasteryLevel.Learning;
    }
}
