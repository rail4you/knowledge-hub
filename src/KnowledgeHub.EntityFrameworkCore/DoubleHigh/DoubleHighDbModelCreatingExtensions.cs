using KnowledgeHub;
using KnowledgeHub.DoubleHigh;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

public static class DoubleHighDbModelCreatingExtensions
{
    public static void ConfigureDoubleHigh(this ModelBuilder builder)
    {
        builder.Entity<DoubleHighProject>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "DoubleHighProjects", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.BatchCode).IsRequired().HasMaxLength(64);
            b.Property(x => x.Description).HasMaxLength(4000);

            b.HasIndex(x => x.BatchCode);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<DoubleHighIndicator>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "DoubleHighIndicators", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.CategoryName).IsRequired().HasMaxLength(128);
            b.Property(x => x.IndicatorCode).IsRequired().HasMaxLength(64);
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.Unit).HasMaxLength(64);

            b.HasIndex(x => x.ProjectId);
            b.HasIndex(x => x.ParentId);
            b.HasIndex(x => new { x.ProjectId, x.IndicatorCode }).IsUnique();
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<DoubleHighIndicatorValue>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "DoubleHighIndicatorValues", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Note).HasMaxLength(1000);

            b.HasIndex(x => x.ProjectId);
            b.HasIndex(x => x.IndicatorId);
            b.HasIndex(x => x.CollectedAt);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<DoubleHighEvidence>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "DoubleHighEvidences", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.AttachmentUrl).HasMaxLength(1000);
            b.Property(x => x.ExternalLink).HasMaxLength(1000);

            b.HasIndex(x => x.ProjectId);
            b.HasIndex(x => x.IndicatorId);
            b.HasIndex(x => x.ResourceId);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<DoubleHighReport>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "DoubleHighReports", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.ReportName).IsRequired().HasMaxLength(256);
            b.Property(x => x.SummaryJson).HasMaxLength(4000);

            b.HasIndex(x => x.ProjectId);
            b.HasIndex(x => x.GeneratedById);
            b.HasIndex(x => x.GeneratedAt);
            b.HasIndex(x => x.TenantId);
        });
    }
}
