using KnowledgeHub;
using KnowledgeHub.Competencies;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.Competencies;

/// <summary>能力体系（能力项 / 课程支撑 / 能力-知识点）表结构。</summary>
public static class CompetencyDbModelCreatingExtensions
{
    public static void ConfigureCompetency(this ModelBuilder builder)
    {
        builder.Entity<Competency>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "Competencies", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.Code).HasMaxLength(64);
            b.Property(x => x.Description).HasMaxLength(2000);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.MajorId);
            b.HasIndex(x => x.ParentId);
            // 同一专业下编码唯一（专业认证按编码引用指标点）；编码可空 → 用过滤索引避免空值互相冲突
            b.HasIndex(x => new { x.MajorId, x.Code })
                .IsUnique()
                .HasFilter("\"Code\" IS NOT NULL");
        });

        builder.Entity<CourseCompetencySupport>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "CourseCompetencySupports", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Note).HasMaxLength(500);

            b.HasIndex(x => new { x.CourseId, x.CompetencyId }).IsUnique();
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.CompetencyId);
        });

        builder.Entity<CompetencyKnowledgeLink>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "CompetencyKnowledgeLinks", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => new { x.CompetencyId, x.KnowledgeResourceId }).IsUnique();
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.KnowledgeResourceId);
        });
    }
}
