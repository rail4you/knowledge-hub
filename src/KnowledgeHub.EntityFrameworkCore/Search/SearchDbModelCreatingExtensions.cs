using Microsoft.EntityFrameworkCore;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Resources;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore;

public static class SearchDbModelCreatingExtensions
{
    public static void ConfigureSearch(this ModelBuilder builder)
    {
        builder.Entity<DocumentIndex>(b =>
        {
            b.ToTable("KhDocumentIndices");
            b.ConfigureByConvention();
            
            b.Property(x => x.PageContent).HasColumnType("text");
            b.Property(x => x.PageTitle).HasMaxLength(512);
            b.Property(x => x.EmbeddingVector).HasColumnType("text");
            
            b.HasOne(x => x.Resource)
                .WithMany(x => x.DocumentIndices)
                .HasForeignKey(x => x.ResourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SearchQuery>(b =>
        {
            b.ToTable("KhSearchQueries");
            b.ConfigureByConvention();
            
            b.Property(x => x.QueryText).HasMaxLength(500);
            b.Property(x => x.Filters).HasMaxLength(1000);
        });

        builder.Entity<ResourceViewLog>(b =>
        {
            b.ToTable("KhResourceViewLogs");
            b.ConfigureByConvention();
            
            b.HasOne(x => x.Resource)
                .WithMany(x => x.ViewLogs)
                .HasForeignKey(x => x.ResourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SearchStatistics>(b =>
        {
            b.ToTable("KhSearchStatistics");
            b.ConfigureByConvention();
            
            b.HasIndex(x => new { x.Date, x.TenantId }).IsUnique();
            
            b.Property(x => x.TopSearchTerm).HasMaxLength(500);
        });

        builder.Entity<ResourceExposure>(b =>
        {
            b.ToTable("KhResourceExposures");
            b.ConfigureByConvention();
            
            b.HasIndex(x => x.ResourceId).IsUnique();
            
            b.HasOne(x => x.Resource)
                .WithOne(x => x.Exposure)
                .HasForeignKey<ResourceExposure>(x => x.ResourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DocumentIndexingJob>(b =>
        {
            b.ToTable("KhDocumentIndexingJobs");
            b.ConfigureByConvention();
            
            b.HasIndex(x => x.ResourceId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.CreationTime);
            b.HasIndex(x => x.NextRetryAt);
            
            b.Property(x => x.ErrorMessage).HasMaxLength(2000);
        });

        builder.Entity<VideoIndexingJob>(b =>
        {
            b.ToTable("KhVideoIndexingJobs");
            b.ConfigureByConvention();
            
            b.HasIndex(x => x.ResourceId);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.CreationTime);
            b.HasIndex(x => x.NextRetryAt);
            
            b.Property(x => x.ErrorMessage).HasMaxLength(2000);
        });

        builder.Entity<PageContent>(b =>
        {
            b.ToTable("KhPageContents");
            b.ConfigureByConvention();

            b.HasIndex(x => new { x.ResourceId, x.PageNumber }).IsUnique();

            b.Property(x => x.Content).HasColumnType("text");
            b.Property(x => x.TextItemsJson).HasColumnType("jsonb");
        });

        builder.Entity<ResourceReview>(b =>
        {
            b.ToTable("KhResourceReviews");
            b.ConfigureByConvention();

            b.HasIndex(x => new { x.ResourceId, x.UserId }).IsUnique();

            b.Property(x => x.Content).HasMaxLength(2000);

            b.HasOne(x => x.Resource)
                .WithMany(x => x.Reviews)
                .HasForeignKey(x => x.ResourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
