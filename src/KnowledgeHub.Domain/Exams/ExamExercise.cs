using System;
using Volo.Abp.Domain.Entities;

namespace KnowledgeHub.Exams;

public class ExamExercise : Entity<Guid>
{
    public Guid ExamId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public int Score { get; set; }
    public int SortOrder { get; set; }
    
    private ExamExercise() { }
    
    public ExamExercise(Guid id, Guid examId, Guid exerciseId, int score, int sortOrder) : base(id)
    {
        ExamId = examId;
        ExerciseId = exerciseId;
        Score = score;
        SortOrder = sortOrder;
    }
}
