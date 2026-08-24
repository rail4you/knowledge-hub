using KnowledgeHub;
using KnowledgeHub.Accounts;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.Accounts;

public static class AccountValidityDbModelCreatingExtensions
{
    public static void ConfigureAccountValidity(this ModelBuilder builder)
    {
        builder.Entity<AccountValidity>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "AccountValidities", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.UserName).IsRequired().HasMaxLength(AccountValidity.MaxUserNameLength);
            b.Property(x => x.DisplayName).HasMaxLength(AccountValidity.MaxDisplayNameLength);
            b.Property(x => x.RoleName).IsRequired().HasMaxLength(AccountValidity.MaxRoleNameLength);
            b.Property(x => x.RevokedPermissionsJson).HasMaxLength(4000);

            b.HasIndex(x => x.UserId).IsUnique();
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ValidUntil);
        });
    }
}
