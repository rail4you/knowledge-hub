using KnowledgeHub;
using KnowledgeHub.Employment;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

public static class EmploymentDbModelCreatingExtensions
{
    public static void ConfigureEmployment(this ModelBuilder builder)
    {
        builder.Entity<JobPosting>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "JobPostings", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.CompanyName).IsRequired().HasMaxLength(200);
            b.Property(x => x.Industry).HasMaxLength(100);
            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            b.Property(x => x.Summary).HasMaxLength(1000);
            b.Property(x => x.Description).IsRequired().HasMaxLength(8000);
            b.Property(x => x.Location).HasMaxLength(200);
            b.Property(x => x.Address).HasMaxLength(500);
            b.Property(x => x.EducationRequirement).HasMaxLength(500);
            b.Property(x => x.SalaryRange).HasMaxLength(100);
            b.Property(x => x.SkillTags).HasMaxLength(1000);
            b.Property(x => x.Benefits).HasMaxLength(2000);
            b.Property(x => x.ContactName).HasMaxLength(100);
            b.Property(x => x.ContactPhone).HasMaxLength(50);
            b.Property(x => x.ContactEmail).HasMaxLength(200);
            b.Property(x => x.ReviewComment).HasMaxLength(1000);

            b.HasIndex(x => x.EmployerUserId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.JobType);
            b.HasIndex(x => x.PublishedAt);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<StudentResume>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "StudentResumes", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            b.Property(x => x.FullName).IsRequired().HasMaxLength(100);
            b.Property(x => x.PhoneNumber).HasMaxLength(50);
            b.Property(x => x.Email).HasMaxLength(200);
            b.Property(x => x.SchoolName).HasMaxLength(200);
            b.Property(x => x.Major).HasMaxLength(100);
            b.Property(x => x.Grade).HasMaxLength(50);
            b.Property(x => x.ClassName).HasMaxLength(100);
            b.Property(x => x.StudentNumber).HasMaxLength(50);
            b.Property(x => x.Summary).HasMaxLength(2000);
            b.Property(x => x.Skills).HasMaxLength(2000);
            b.Property(x => x.EducationExperience).HasMaxLength(4000);
            b.Property(x => x.InternshipExperience).HasMaxLength(4000);
            b.Property(x => x.ProjectExperience).HasMaxLength(4000);
            b.Property(x => x.CertificateText).HasMaxLength(2000);
            b.Property(x => x.AttachmentUrl).HasMaxLength(1000);

            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.IsDefault);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<JobApplication>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "JobApplications", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.CoverLetter).HasMaxLength(2000);
            b.Property(x => x.EmployerRemark).HasMaxLength(1000);

            b.HasIndex(x => x.JobPostingId);
            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.ResumeId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.AppliedAt);
            b.HasIndex(x => new { x.JobPostingId, x.StudentId }).IsUnique();
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<InterviewSchedule>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "InterviewSchedules", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.InterviewerName).IsRequired().HasMaxLength(100);
            b.Property(x => x.InterviewerPhone).HasMaxLength(50);
            b.Property(x => x.Location).HasMaxLength(500);
            b.Property(x => x.MeetingUrl).HasMaxLength(1000);
            b.Property(x => x.Note).HasMaxLength(1000);
            b.Property(x => x.Summary).HasMaxLength(4000);
            b.Property(x => x.ResultComment).HasMaxLength(1000);

            b.HasIndex(x => x.ApplicationId);
            b.HasIndex(x => x.JobPostingId);
            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.ScheduledAt);
            b.HasIndex(x => x.Result);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<EmploymentGuidanceRecord>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "EmploymentGuidanceRecords", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            // AI 生成的就业指导内容是 raw JSON（评估/路径/技能差距/行动计划/下一步），
            // 单条报告可能远超 4000 字符，留 16000 兜底。
            b.Property(x => x.Content).IsRequired().HasMaxLength(16000);
            b.Property(x => x.CareerGoal).HasMaxLength(1000);

            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.ApplicationId);
            b.HasIndex(x => x.TeacherId);
            b.HasIndex(x => x.GuidedAt);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<EmploymentOutcome>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "EmploymentOutcomes", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.EmployerName).IsRequired().HasMaxLength(200);
            b.Property(x => x.JobTitle).IsRequired().HasMaxLength(200);
            b.Property(x => x.EmploymentType).HasMaxLength(100);
            b.Property(x => x.Region).HasMaxLength(200);
            b.Property(x => x.SalaryRange).HasMaxLength(100);
            b.Property(x => x.Remark).HasMaxLength(1000);

            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.ApplicationId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.IsPrimary);
            b.HasIndex(x => x.ConfirmedAt);
            b.HasIndex(x => x.TenantId);
        });
    }
}
