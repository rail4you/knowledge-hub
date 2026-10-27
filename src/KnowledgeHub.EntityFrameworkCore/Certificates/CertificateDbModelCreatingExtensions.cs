using KnowledgeHub;
using KnowledgeHub.Certificates;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.Certificates;

public static class CertificateDbModelCreatingExtensions
{
    /// <summary>
    /// 结业证书相关表（PRD 第 9 节）。
    ///
    /// <para>
    /// 证书模板刻意<b>不</b>带 TenantId：模板由平台侧统一定义，所有院校共用（院校 / 老师不可自定义）。
    /// </para>
    /// </summary>
    public static void ConfigureCertificates(this ModelBuilder builder)
    {
        builder.Entity<CertificateTemplate>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "CertificateTemplates", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(CertificateTemplate.MaxNameLength);
            b.Property(x => x.ImageUrl).IsRequired().HasMaxLength(CertificateTemplate.MaxImageUrlLength);
            b.Property(x => x.Description).HasMaxLength(500);

            b.HasIndex(x => x.IsDefault);
            b.HasIndex(x => x.IsEnabled);
        });

        builder.Entity<CertificateApplication>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "CertificateApplications", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.ApplicantName).IsRequired().HasMaxLength(CertificateApplication.MaxApplicantNameLength);
            b.Property(x => x.CourseTitle).IsRequired().HasMaxLength(CertificateApplication.MaxCourseTitleLength);
            b.Property(x => x.LibraryName).HasMaxLength(256);
            b.Property(x => x.SchoolName).HasMaxLength(256);
            b.Property(x => x.PhoneMasked).HasMaxLength(32);
            b.Property(x => x.ReviewerName).HasMaxLength(128);
            b.Property(x => x.ReviewRemark).HasMaxLength(CertificateApplication.MaxRemarkLength);

            b.HasIndex(x => x.LibraryId);
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.TenantId);
            // 同一学员同一课程只允许一条待审核 / 已通过的申请
            b.HasIndex(x => new { x.CourseId, x.StudentId, x.Status });
        });

        builder.Entity<Certificate>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "Certificates", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.CertificateNo).IsRequired().HasMaxLength(Certificate.MaxCertificateNoLength);
            b.Property(x => x.VerifyCode).IsRequired().HasMaxLength(Certificate.MaxVerifyCodeLength);
            b.Property(x => x.StudentName).IsRequired().HasMaxLength(Certificate.MaxStudentNameLength);
            b.Property(x => x.CourseTitle).IsRequired().HasMaxLength(Certificate.MaxCourseTitleLength);
            b.Property(x => x.SchoolName).HasMaxLength(Certificate.MaxSchoolNameLength);
            b.Property(x => x.LibraryName).HasMaxLength(Certificate.MaxLibraryNameLength);
            b.Property(x => x.ImageUrl).HasMaxLength(Certificate.MaxImageUrlLength);

            b.HasIndex(x => x.CertificateNo).IsUnique();
            b.HasIndex(x => x.VerifyCode).IsUnique();
            b.HasIndex(x => x.StudentId);
            b.HasIndex(x => x.LibraryId);
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.LearnerId);
        });
    }
}
