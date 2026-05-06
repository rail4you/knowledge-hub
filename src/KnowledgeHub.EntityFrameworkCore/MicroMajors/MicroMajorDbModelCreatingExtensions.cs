using KnowledgeHub;
using KnowledgeHub.MicroMajors;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

public static class MicroMajorDbModelCreatingExtensions
{
    public static void ConfigureMicroMajor(this ModelBuilder builder)
    {
        builder.Entity<MicroMajor>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "MicroMajors", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.Summary).HasMaxLength(1000);
            b.Property(x => x.Description).HasMaxLength(4000);
            b.Property(x => x.CoverImageUrl).HasMaxLength(512);
            b.Property(x => x.IndustryField).HasMaxLength(256);
            b.Property(x => x.CollaborationUnit).HasMaxLength(256);

            b.HasIndex(x => x.Title);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<MicroMajorCourse>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "MicroMajorCourses", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => x.MicroMajorId);
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => new { x.MicroMajorId, x.CourseId }).IsUnique();
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<MicroMajorEnrollment>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "MicroMajorEnrollments", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => x.MicroMajorId);
            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => new { x.MicroMajorId, x.StudentId }).IsUnique();
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<MicroMajorCertificate>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "MicroMajorCertificates", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.CertificateNo).IsRequired().HasMaxLength(64);
            b.Property(x => x.VerifyCode).IsRequired().HasMaxLength(32);

            b.HasIndex(x => x.EnrollmentId).IsUnique();
            b.HasIndex(x => x.CertificateNo).IsUnique();
            b.HasIndex(x => x.VerifyCode);
            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.TenantId);
        });
    }
}
