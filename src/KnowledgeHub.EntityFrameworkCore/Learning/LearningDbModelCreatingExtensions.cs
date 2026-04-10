using KnowledgeHub;
using KnowledgeHub.Learning;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

public static class LearningDbModelCreatingExtensions
{
    public static void ConfigureLearning(this ModelBuilder builder)
    {
        builder.Entity<StudentCourse>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "StudentCourses", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.HasIndex(x => new { x.TenantId, x.StudentId, x.CourseId }).IsUnique();
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.TenantId);
        });
        
        builder.Entity<LearningProgress>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "LearningProgress", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.Property(x => x.LastPosition).HasMaxLength(500);
            
            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => new { x.StudentId, x.ResourceId });
            b.HasIndex(x => x.TenantId);
        });
        
        builder.Entity<KnowledgeMastery>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "KnowledgeMastery", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.KnowledgeResourceId);
            b.HasIndex(x => new { x.StudentId, x.KnowledgeResourceId }).IsUnique();
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<StudentExerciseRecord>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "StudentExerciseRecords", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => new { x.TenantId, x.StudentId, x.ExerciseId }).IsUnique();
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.ChapterId);
            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.CompletedAt);
        });
    }
}
