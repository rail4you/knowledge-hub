using KnowledgeHub.Whiteboards;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.Whiteboards;

/// <summary>教研白板三张表的 EF 映射。</summary>
public static class WhiteboardDbModelCreatingExtensions
{
    public static void ConfigureWhiteboards(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<Whiteboard>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "Whiteboards", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            b.Property(x => x.Topic).HasMaxLength(500);
            b.Property(x => x.Subject).HasMaxLength(200);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.CreatorName).HasMaxLength(128);
            b.Property(x => x.ViewScale).HasColumnType("double precision");

            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.CreatorId);
            // 列表按「最近改动」排序，命中率最高的复合索引
            b.HasIndex(x => new { x.TenantId, x.LastModificationTime });
        });

        builder.Entity<WhiteboardMember>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "WhiteboardMembers", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.UserName).IsRequired().HasMaxLength(128);
            b.Property(x => x.Color).IsRequired().HasMaxLength(16);

            b.HasIndex(x => new { x.WhiteboardId, x.UserId }).IsUnique();
            b.HasIndex(x => x.UserId);
        });

        builder.Entity<WhiteboardElement>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "WhiteboardElements", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.ElementKey).IsRequired().HasMaxLength(64);
            // 元素属性整份存 JSON：字段多、随前端演进，服务端不解析
            b.Property(x => x.Json).IsRequired().HasColumnType("text");
            b.Property(x => x.LastEditorName).HasMaxLength(128);

            // 实时 upsert 按 (白板, 元素键) 定位
            b.HasIndex(x => new { x.WhiteboardId, x.ElementKey }).IsUnique();
        });
    }
}
