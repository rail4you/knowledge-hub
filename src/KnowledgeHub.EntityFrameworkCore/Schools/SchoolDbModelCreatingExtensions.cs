using KnowledgeHub;
using KnowledgeHub.Schools;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.Schools;

public static class SchoolDbModelCreatingExtensions
{
    public static void ConfigureSchool(this ModelBuilder builder)
    {
        builder.Entity<School>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "Schools", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.Name).IsRequired().HasMaxLength(School.MaxNameLength);
            b.Property(x => x.LogoUrl).HasMaxLength(School.MaxLogoUrlLength);
            b.Property(x => x.BannerUrl).HasMaxLength(School.MaxBannerUrlLength);
            b.Property(x => x.Introduction).HasMaxLength(School.MaxIntroductionLength);
            b.Property(x => x.Website).HasMaxLength(School.MaxWebsiteLength);
            b.Property(x => x.Address).HasMaxLength(School.MaxAddressLength);
            b.Property(x => x.ContactPhone).HasMaxLength(School.MaxContactPhoneLength);
            b.Property(x => x.ContactEmail).HasMaxLength(School.MaxContactEmailLength);

            // 与 ABP 租户 1:1
            b.HasIndex(x => x.TenantId).IsUnique();
            b.HasIndex(x => x.IsPublic);
        });
    }
}
