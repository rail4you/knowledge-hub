using KnowledgeHub;
using KnowledgeHub.MenuVisibility;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.MenuVisibility;

/// <summary>
/// 用户级「侧边栏一级菜单可见性」表结构（一个用户最多一行，存被隐藏的一级菜单名）。
/// </summary>
public static class MenuVisibilityDbModelCreatingExtensions
{
    public static void ConfigureMenuVisibility(this ModelBuilder builder)
    {
        builder.Entity<UserMenuVisibility>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "UserMenuVisibilities", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.HiddenMenus)
                .IsRequired()
                .HasMaxLength(4000);

            // 一个租户内一个用户只有一行配置。
            b.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        });
    }
}
