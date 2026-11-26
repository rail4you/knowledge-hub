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
            b.HasIndex(x => x.ClassId);
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

            // 统一掌握度算法（MasteryCalculator：Beta 先验 + 半衰期）的落库字段
            b.Property(x => x.Mastery).HasDefaultValue(0d);
            b.Property(x => x.Evidence).HasDefaultValue(0d);
        });

        // 学生个性化推荐（落库 + 生命周期跟踪）
        builder.Entity<StudentRecommendation>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "StudentRecommendations", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Reason).IsRequired().HasMaxLength(500);

            // 同一学生同一门课里，同一个「类型 + 对象」只留一条
            b.HasIndex(x => new { x.StudentId, x.CourseId, x.Type, x.KnowledgeResourceId, x.ChapterId })
                .IsUnique()
                .HasFilter("\"KnowledgeResourceId\" IS NOT NULL OR \"ChapterId\" IS NOT NULL");
            b.HasIndex(x => new { x.StudentId, x.Status });
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.TenantId);
        });

        // 学生行为日志：画像 / 推荐 / 学情时间线的原始事实（只追加）
        builder.Entity<StudentActivityLog>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "StudentActivityLogs", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.ResourceType).HasMaxLength(64);
            b.Property(x => x.Metadata).HasMaxLength(2000);

            // 画像查询固定按「某学生最近的行为」：StudentId + CreationTime
            b.HasIndex(x => new { x.StudentId, x.CreationTime });
            b.HasIndex(x => x.CourseId);
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
