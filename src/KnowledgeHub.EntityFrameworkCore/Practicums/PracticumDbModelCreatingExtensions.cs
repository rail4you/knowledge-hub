using KnowledgeHub;
using KnowledgeHub.Practicums;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

public static class PracticumDbModelCreatingExtensions
{
    public static void ConfigurePracticum(this ModelBuilder builder)
    {
        builder.Entity<PracticumProject>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PracticumProjects", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.Summary).HasMaxLength(1000);
            b.Property(x => x.Description).HasMaxLength(4000);
            b.Property(x => x.CoverImageUrl).HasMaxLength(512);
            b.Property(x => x.Major).HasMaxLength(256);
            b.Property(x => x.ClassName).HasMaxLength(256);

            b.HasIndex(x => x.Title);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.TenantId);

            b.Property(x => x.AgentName).HasMaxLength(100);
            b.Property(x => x.AgentPrompt).HasMaxLength(4000);
        });

        builder.Entity<PracticumTask>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PracticumTasks", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.Requirement).HasMaxLength(2000);

            b.HasIndex(x => x.ProjectId);
            b.HasIndex(x => new { x.ProjectId, x.SortOrder });
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<PracticumMaterial>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PracticumMaterials", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.ResourceUrl).IsRequired().HasMaxLength(1000);

            b.HasIndex(x => x.ProjectId);
            b.HasIndex(x => x.TaskId);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<PracticumEnrollment>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PracticumEnrollments", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.FinalComment).HasMaxLength(2000);

            b.HasIndex(x => x.ProjectId);
            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => new { x.ProjectId, x.StudentId }).IsUnique();
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<PracticumSubmission>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PracticumSubmissions", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Content).HasMaxLength(4000);
            b.Property(x => x.AttachmentUrls).HasMaxLength(2000);
            b.Property(x => x.LinkUrl).HasMaxLength(1000);
            b.Property(x => x.ScreenshotUrls).HasMaxLength(2000);
            b.Property(x => x.TeacherFeedback).HasMaxLength(2000);

            b.HasIndex(x => x.ProjectId);
            b.HasIndex(x => x.TaskId);
            b.HasIndex(x => x.EnrollmentId);
            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => new { x.EnrollmentId, x.TaskId, x.VersionNo }).IsUnique();
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<PracticumGuidanceRecord>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PracticumGuidanceRecords", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Content).IsRequired().HasMaxLength(2000);

            b.HasIndex(x => x.ProjectId);
            b.HasIndex(x => x.EnrollmentId);
            b.HasIndex(x => x.TaskId);
            b.HasIndex(x => x.TeacherId);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<PracticumAssessment>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PracticumAssessments", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.GradeLevel).HasMaxLength(64);
            b.Property(x => x.Comment).HasMaxLength(2000);
            b.Property(x => x.RubricJson).HasMaxLength(4000);

            b.HasIndex(x => x.ProjectId);
            b.HasIndex(x => x.EnrollmentId);
            b.HasIndex(x => x.SubmissionId);
            b.HasIndex(x => x.TeacherId);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<PracticumChatMessage>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PracticumChatMessages", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.SenderName).IsRequired().HasMaxLength(128);
            b.Property(x => x.Content).HasMaxLength(4000);
            b.Property(x => x.AttachmentUrl).HasMaxLength(1000);
            b.Property(x => x.AttachmentName).HasMaxLength(256);

            b.HasIndex(x => x.ProjectId);
            b.HasIndex(x => x.SenderId);
            b.HasIndex(x => x.SenderType);
            b.HasIndex(x => x.CreationTime);
            b.HasIndex(x => x.TenantId);
        });
    }
}
