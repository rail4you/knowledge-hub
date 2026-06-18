using KnowledgeHub;
using KnowledgeHub.TenantInfos;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.TenantInfos;

public static class TenantInfoDbModelCreatingExtensions
{
    public static void ConfigureTenantInfo(this ModelBuilder builder)
    {
        builder.Entity<TenantInfo>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "TenantInfos", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(TenantInfo.MaxNameLength);
            b.Property(x => x.Description).HasMaxLength(TenantInfo.MaxDescriptionLength);
            b.Property(x => x.CoverImages).HasMaxLength(TenantInfo.MaxCoverImagesLength);
            b.Property(x => x.TalentTrainingPlan).HasMaxLength(TenantInfo.MaxTalentTrainingPlanLength);
            b.Property(x => x.ProfessionalTeachingStandards).HasMaxLength(TenantInfo.MaxProfessionalTeachingStandardsLength);
            b.Property(x => x.SpecialProjects).HasMaxLength(TenantInfo.MaxSpecialProjectsLength);

            b.HasIndex(x => x.TenantId).IsUnique();
        });
    }
}
