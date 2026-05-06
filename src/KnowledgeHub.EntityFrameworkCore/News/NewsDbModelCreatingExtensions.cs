using KnowledgeHub;
using KnowledgeHub.News;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

public static class NewsDbModelCreatingExtensions
{
    public static void ConfigureNews(this ModelBuilder builder)
    {
        builder.Entity<NewsCategory>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "NewsCategories", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(128);
            b.Property(x => x.Code).IsRequired().HasMaxLength(64);

            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => x.ParentId);
            b.HasIndex(x => x.SortOrder);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<NewsArticle>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "NewsArticles", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.Summary).HasMaxLength(1000);
            b.Property(x => x.Content).IsRequired();
            b.Property(x => x.CoverImageUrl).HasMaxLength(512);
            b.Property(x => x.Tags).HasMaxLength(500);

            b.HasIndex(x => x.CategoryId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.PublishedAt);
            b.HasIndex(x => x.AuthorId);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<NewsAudit>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "NewsAudits", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Action).IsRequired().HasMaxLength(64);
            b.Property(x => x.Comment).HasMaxLength(1000);

            b.HasIndex(x => x.ArticleId);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<NewsComment>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "NewsComments", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Content).IsRequired().HasMaxLength(2000);

            b.HasIndex(x => x.ArticleId);
            b.HasIndex(x => x.UserId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<NewsReaction>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "NewsReactions", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => new { x.ArticleId, x.UserId }).IsUnique();
            b.HasIndex(x => x.TenantId);
        });
    }
}
