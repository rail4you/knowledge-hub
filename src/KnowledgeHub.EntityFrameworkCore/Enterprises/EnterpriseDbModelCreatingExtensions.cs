using KnowledgeHub.Enterprises;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.Enterprises;

public static class EnterpriseDbModelCreatingExtensions
{
    public static void ConfigureEnterprises(this ModelBuilder builder)
    {
        builder.Entity<EnterpriseProfile>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "EnterpriseProfiles", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(EnterpriseProfile.MaxNameLength);
            b.Property(x => x.UnifiedSocialCreditCode).HasMaxLength(EnterpriseProfile.MaxCreditCodeLength);
            b.Property(x => x.Industry).HasMaxLength(EnterpriseProfile.MaxIndustryLength);
            b.Property(x => x.ContactName).HasMaxLength(EnterpriseProfile.MaxContactNameLength);
            b.Property(x => x.ContactPhone).HasMaxLength(EnterpriseProfile.MaxContactPhoneLength);
            b.Property(x => x.ContactEmail).HasMaxLength(EnterpriseProfile.MaxContactEmailLength);
            b.Property(x => x.Summary).HasMaxLength(EnterpriseProfile.MaxSummaryLength);
            b.Property(x => x.LogoUrl).HasMaxLength(EnterpriseProfile.MaxLogoUrlLength);
            b.Property(x => x.RejectReason).HasMaxLength(500);

            b.HasIndex(x => new { x.TenantId, x.CertificationStatus });
            b.HasIndex(x => x.OwnerUserId);
            b.HasIndex(x => x.ValidTo);
        });

        builder.Entity<EnterpriseMember>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "EnterpriseMembers", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.UserName).HasMaxLength(256);
            b.HasIndex(x => new { x.EnterpriseProfileId, x.UserId }).IsUnique();
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.TenantId);
        });
    }
}
