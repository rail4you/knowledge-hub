using KnowledgeHub;
using KnowledgeHub.Assignments;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.Assignments;

/// <summary>课程作业（作业 / 评分细则 / 作业文件 / 学生提交 / 得分明细）表结构。</summary>
public static class AssignmentDbModelCreatingExtensions
{
    public static void ConfigureAssignment(this ModelBuilder builder)
    {
        builder.Entity<CourseAssignment>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "CourseAssignments", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(4000);
            b.Property(x => x.AllowedFileTypes).IsRequired().HasMaxLength(512);

            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.CourseId, x.Deadline });

            b.HasMany(x => x.Criteria).WithOne().HasForeignKey(x => x.AssignmentId);
            b.HasMany(x => x.Files).WithOne().HasForeignKey(x => x.AssignmentId);
            b.HasMany(x => x.Submissions).WithOne().HasForeignKey(x => x.AssignmentId);
        });

        builder.Entity<AssignmentCriterion>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "AssignmentCriteria", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(128);
            b.Property(x => x.Description).HasMaxLength(500);

            b.HasIndex(x => x.AssignmentId);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<AssignmentFile>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "AssignmentFiles", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.FileName).IsRequired().HasMaxLength(256);
            b.Property(x => x.FilePath).IsRequired().HasMaxLength(512);
            b.Property(x => x.ContentType).HasMaxLength(128);

            b.HasIndex(x => x.AssignmentId);
            b.HasIndex(x => x.SubmissionId);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<AssignmentSubmission>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "AssignmentSubmissions", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.StudentName).IsRequired().HasMaxLength(128);
            b.Property(x => x.StudentNumber).HasMaxLength(64);
            b.Property(x => x.Note).HasMaxLength(1000);
            b.Property(x => x.Comment).HasMaxLength(2000);
            b.Property(x => x.ReviewerName).HasMaxLength(128);

            b.HasIndex(x => x.AssignmentId);
            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.AssignmentId, x.StudentId });

            b.HasMany(x => x.Files).WithOne().HasForeignKey(x => x.SubmissionId);
            b.HasMany(x => x.Scores).WithOne().HasForeignKey(x => x.SubmissionId);
        });

        builder.Entity<SubmissionScore>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "SubmissionScores", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.CriterionName).IsRequired().HasMaxLength(128);
            b.Property(x => x.Comment).HasMaxLength(500);

            b.HasIndex(x => x.SubmissionId);
            b.HasIndex(x => x.TenantId);
        });
    }
}
