using System;
using KnowledgeHub.Learning.Enums;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Learning;

public class KnowledgeMastery : Entity<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid StudentId { get; set; }
    public Guid KnowledgeResourceId { get; set; }
    
    public MasteryLevel Level { get; set; } = MasteryLevel.Unlearned;
    public int PracticeCount { get; set; }
    public int CorrectCount { get; set; }
    public DateTime LastPracticeAt { get; set; }
    
    public decimal Accuracy => PracticeCount > 0 ? (decimal)CorrectCount / PracticeCount : 0;
    
    private KnowledgeMastery() { }
    
    public KnowledgeMastery(Guid id, Guid studentId, Guid knowledgeResourceId) : base(id)
    {
        StudentId = studentId;
        KnowledgeResourceId = knowledgeResourceId;
        Level = MasteryLevel.Unlearned;
    }
    
    public void RecordPractice(bool isCorrect)
    {
        PracticeCount++;
        if (isCorrect)
        {
            CorrectCount++;
        }
        LastPracticeAt = DateTime.UtcNow;
        UpdateLevel();
    }
    
    private void UpdateLevel()
    {
        if (PracticeCount >= 3 && Accuracy >= 0.8m)
        {
            Level = MasteryLevel.Mastered;
        }
        else if (PracticeCount >= 1)
        {
            Level = MasteryLevel.Learning;
        }
    }
}
