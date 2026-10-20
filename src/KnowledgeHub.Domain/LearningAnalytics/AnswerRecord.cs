using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 统一作答记录：纸质试卷（OCR 校对后）与课堂练习写入此处；
/// 在线练习仍保存在 <c>StudentExerciseRecord</c>，计算掌握度时合并。
/// </summary>
public class AnswerRecord : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid StudentId { get; set; }

    public Guid CourseId { get; set; }

    public Guid? ChapterId { get; set; }

    public Guid ExerciseId { get; set; }

    public double Score { get; set; }

    public double FullScore { get; set; } = 1;

    public Enums.AnswerSource Source { get; set; } = Enums.AnswerSource.Online;

    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;

    /// <summary>来自纸质试卷时指向具体试卷</summary>
    public Guid? PaperScriptId { get; set; }

    private AnswerRecord() { }

    public AnswerRecord(Guid id, Guid studentId, Guid courseId, Guid exerciseId) : base(id)
    {
        StudentId = studentId;
        CourseId = courseId;
        ExerciseId = exerciseId;
    }

    /// <summary>得分率（0~1）</summary>
    public double Ratio => FullScore > 0 ? Math.Clamp(Score / FullScore, 0, 1) : 0;
}
