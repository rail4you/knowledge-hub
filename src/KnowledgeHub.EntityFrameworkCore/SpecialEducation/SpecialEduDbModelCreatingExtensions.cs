using KnowledgeHub.SpecialEducation;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.SpecialEducation;

public static class SpecialEduDbModelCreatingExtensions
{
    public static void ConfigureSpecialEdu(this ModelBuilder builder)
    {
        builder.Entity<SpecialTeachingDesign>(b =>
        {
            b.ToTable("SpecialTeachingDesigns");
            b.ConfigureByConvention();
            b.Property(x => x.Title).HasMaxLength(300).IsRequired();
            b.Property(x => x.Subject).HasMaxLength(100);
            b.Property(x => x.Grade).HasMaxLength(100);
            b.Property(x => x.ReviewComment).HasMaxLength(1000);
            b.Property(x => x.RawJson).HasColumnType("text");
            b.Property(x => x.SourceInputJson).HasColumnType("text");
            b.Property(x => x.SectionsJson).HasColumnType("text");
            b.Property(x => x.StandardBasis).HasColumnType("text");
            b.HasIndex(x => new { x.TenantId, x.Category });
            b.HasIndex(x => x.CourseId);
        });

        builder.Entity<IepPlan>(b =>
        {
            b.ToTable("IepPlans");
            b.ConfigureByConvention();
            b.Property(x => x.StudentName).HasMaxLength(100);
            b.Property(x => x.RawJson).HasColumnType("text");
            b.Property(x => x.SourceInputJson).HasColumnType("text");
            b.Property(x => x.ProfileJson).HasColumnType("text");
            b.Property(x => x.LegalBasis).HasColumnType("text");
            b.HasIndex(x => new { x.TenantId, x.StudentUserId });
            b.HasIndex(x => x.ParentVersionId);
        });

        builder.Entity<SpecialEduResource>(b =>
        {
            b.ToTable("SpecialEduResources");
            b.ConfigureByConvention();
            b.Property(x => x.Title).HasMaxLength(300).IsRequired();
            b.Property(x => x.Modality).HasMaxLength(50).IsRequired();
            b.Property(x => x.ContentJson).HasColumnType("text");
            b.Property(x => x.PairsJson).HasColumnType("text");
            b.Property(x => x.RawJson).HasColumnType("text");
            b.HasIndex(x => new { x.TenantId, x.Category, x.Modality });
            b.HasIndex(x => x.TeachingDesignId);
            b.HasIndex(x => x.IepPlanId);
        });

        builder.Entity<SpecialEduContentVersion>(b =>
        {
            b.ToTable("SpecialEduContentVersions");
            b.ConfigureByConvention();
            b.Property(x => x.Title).HasMaxLength(300);
            b.Property(x => x.SnapshotJson).HasColumnType("text");
            b.HasIndex(x => new { x.TenantId, x.ContentType, x.EntityId, x.VersionNumber }).IsUnique();
        });
    }
}
