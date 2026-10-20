using KnowledgeHub;
using KnowledgeHub.LearningAnalytics;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

public static class LearningAnalyticsDbModelCreatingExtensions
{
    public static void ConfigureLearningAnalytics(this ModelBuilder builder)
    {
        builder.Entity<KnowledgePoint>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "KnowledgePoints", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(1024);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.CourseId, x.Name });
            b.HasIndex(x => x.ChapterId);
            b.HasIndex(x => x.CanonicalId);
        });

        builder.Entity<ExerciseKnowledgePoint>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ExerciseKnowledgePoints", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.ExerciseId, x.KnowledgePointId }).IsUnique();
            b.HasIndex(x => x.KnowledgePointId);
        });

        builder.Entity<AnswerRecord>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "AnswerRecords", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.StudentId, x.CourseId });
            b.HasIndex(x => x.ExerciseId);
            b.HasIndex(x => x.PaperScriptId);
        });

        builder.Entity<StudentKpMastery>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "StudentKpMasteries", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.StudentId, x.KnowledgePointId }).IsUnique();
            b.HasIndex(x => x.KnowledgePointId);
        });

        builder.Entity<KpPrerequisite>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "KpPrerequisites", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Condition).HasMaxLength(128);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.FromKpId);
            b.HasIndex(x => x.ToKpId);
            b.HasIndex(x => new { x.FromKpId, x.ToKpId, x.Status });
        });

        builder.Entity<CourseLink>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "CourseLinks", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.PredecessorCourseId, x.SuccessorCourseId }).IsUnique();
        });

        builder.Entity<MasteryThreshold>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "MasteryThresholds", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.CourseId, x.KnowledgePointId });
        });

        builder.Entity<PaperBatch>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PaperBatches", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.CourseName).HasMaxLength(256);
            b.Property(x => x.SourceFileNames).HasMaxLength(2048);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.Status);
        });

        builder.Entity<PaperScript>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PaperScripts", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.StudentName).HasMaxLength(128);
            b.Property(x => x.StudentNumber).HasMaxLength(64);
            b.Property(x => x.OcrProvider).HasMaxLength(32);
            b.Property(x => x.OcrModel).HasMaxLength(64);
            b.Property(x => x.SourceFileName).HasMaxLength(512);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.BatchId);
            b.HasIndex(x => x.StudentId);
        });

        builder.Entity<PaperItemScore>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PaperItemScores", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.QuestionNo).HasMaxLength(32);
            b.Property(x => x.RawText).HasMaxLength(1024);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ScriptId);
            b.HasIndex(x => x.BatchId);
            b.HasIndex(x => x.ExerciseId);
        });

        builder.Entity<PaperUploadImage>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PaperUploadImages", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.FileName).IsRequired().HasMaxLength(512);
            b.Property(x => x.ContentType).HasMaxLength(128);
            b.Property(x => x.StoredPath).IsRequired().HasMaxLength(1024);
            b.Property(x => x.OcrProvider).HasMaxLength(32);
            b.Property(x => x.OcrModel).HasMaxLength(64);
            b.Property(x => x.ErrorMessage).HasMaxLength(2048);
            b.Property(x => x.MergeGroup).HasMaxLength(512);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.BatchId, x.Status });
            b.HasIndex(x => new { x.BatchId, x.MergeGroup });
            b.HasIndex(x => x.ScriptId);
        });

        builder.Entity<AnalysisReport>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "AnalysisReports", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).HasMaxLength(256);
            b.Property(x => x.Summary).HasMaxLength(4096);
            b.Property(x => x.FactsHash).HasMaxLength(64);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.StudentId, x.CourseId });
            b.HasIndex(x => x.PaperBatchId);
            b.HasIndex(x => x.FactsHash);
        });

        builder.Entity<StudentNotification>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "StudentNotifications", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.Content).HasMaxLength(2048);
            b.Property(x => x.Source).HasMaxLength(128);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.StudentId, x.IsRead });
            b.HasIndex(x => x.CreationTime);
        });

        builder.Entity<ReviewPackage>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ReviewPackages", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.UpcomingCourseTitle).HasMaxLength(256);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.StudentId, x.UpcomingCourseId });
        });

        builder.Entity<ReviewPackageItem>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ReviewPackageItems", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.PackageId);
            b.HasIndex(x => x.KnowledgePointId);
        });

        builder.Entity<AnalysisReportFile>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "AnalysisReportFiles", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.FileName).IsRequired().HasMaxLength(512);
            b.Property(x => x.ContentType).HasMaxLength(128);
            b.Property(x => x.StoredPath).IsRequired().HasMaxLength(1024);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.BatchId, x.Type });
            b.HasIndex(x => x.CourseId);
        });

        builder.Entity<TeachingInsight>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "TeachingInsights", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.ChapterTitle).HasMaxLength(256);
            b.Property(x => x.Advice).HasMaxLength(2048);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.CourseId, x.PaperBatchId });
        });
    }
}
