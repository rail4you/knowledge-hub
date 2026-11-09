using KnowledgeHub.Studios;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.Studios;

/// <summary>名师工作室三张表的 EF 映射。</summary>
public static class StudioDbModelCreatingExtensions
{
    public static void ConfigureStudios(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<Studio>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "Studios", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.CoverUrl).HasMaxLength(512);
            b.Property(x => x.LeaderName).HasMaxLength(128);

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.LeaderUserId);
        });

        builder.Entity<StudioMember>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "StudioMembers", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.UserName).IsRequired().HasMaxLength(128);
            b.HasIndex(x => new { x.StudioId, x.UserId }).IsUnique();
            b.HasIndex(x => x.UserId);
        });

        builder.Entity<StudioMaterial>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "StudioMaterials", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(300);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.SubmittedByName).HasMaxLength(128);
            b.Property(x => x.ReviewedByName).HasMaxLength(128);
            b.Property(x => x.ReviewComment).HasMaxLength(1000);

            // 同一份资源不能在同一工作室重复挂载
            b.HasIndex(x => new { x.StudioId, x.ResourceId }).IsUnique();
            b.HasIndex(x => new { x.StudioId, x.Status });
        });
    }
}
