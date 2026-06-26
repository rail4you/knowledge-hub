using KnowledgeHub;
using KnowledgeHub.Exams;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

public static class ExamDbModelCreatingExtensions
{
    public static void ConfigureExam(this ModelBuilder builder)
    {
        builder.Entity<Exam>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "Exams", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(1000);
            
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.ChapterId);
            b.HasIndex(x => x.TenantId);
            
            b.HasMany(x => x.ExamExercises).WithOne().HasForeignKey(x => x.ExamId);
        });
        
        builder.Entity<Exercise>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "Exercises", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.QuestionContent).IsRequired().HasMaxLength(4000);
            b.Property(x => x.Answer).IsRequired().HasMaxLength(2000);
            b.Property(x => x.AnswerExplanation).HasMaxLength(2000);
            b.Property(x => x.Options).HasMaxLength(2000);
            
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.ChapterId);
            b.HasIndex(x => x.KnowledgeResourceId);
            b.HasIndex(x => x.TenantId);
        });
        
        builder.Entity<ChapterExercise>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ChapterExercises", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => x.ChapterId);
            b.HasIndex(x => x.ExerciseId);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.ChapterId, x.ExerciseId });
        });

        builder.Entity<ExamExercise>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ExamExercises", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.HasIndex(x => x.ExamId);
            b.HasIndex(x => x.ExerciseId);
        });
        
        builder.Entity<StudentExam>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "StudentExams", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.ExamId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.TenantId);
            
            b.HasMany(x => x.Answers).WithOne().HasForeignKey(x => x.StudentExamId);
        });
        
        builder.Entity<StudentAnswer>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "StudentAnswers", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.Property(x => x.Content).HasMaxLength(4000);
            b.Property(x => x.Feedback).HasMaxLength(1000);
            
            b.HasIndex(x => x.StudentExamId);
            b.HasIndex(x => x.ExerciseId);
        });
    }
}
