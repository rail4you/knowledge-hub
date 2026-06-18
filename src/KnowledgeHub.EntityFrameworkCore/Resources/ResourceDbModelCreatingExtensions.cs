using KnowledgeHub;
using KnowledgeHub.Majors;
using KnowledgeHub.Resources;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

public static class ResourceDbModelCreatingExtensions
{
    public static void ConfigureResource(this ModelBuilder builder)
    {
        builder.Entity<Resource>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "Resources", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.FilePath).HasMaxLength(512);
            b.Property(x => x.FileExtension).HasMaxLength(32);
            b.Property(x => x.OriginalFileName).HasMaxLength(256);
            b.Property(x => x.Keywords).HasMaxLength(500);
            b.Property(x => x.CopyrightInfo).HasMaxLength(500);

            b.HasIndex(x => x.Name);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.CategoryId);
            b.HasIndex(x => x.CreatorId);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.MajorId);

            b.HasOne<Major>()
                .WithMany()
                .HasForeignKey(x => x.MajorId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ResourceVersion>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ResourceVersions", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.Property(x => x.FilePath).HasMaxLength(512);
            b.Property(x => x.UpdateContent).HasMaxLength(500);
            
            b.HasIndex(x => x.ResourceId);
            b.HasIndex(x => new { x.ResourceId, x.IsCurrentVersion });
        });

        builder.Entity<ResourceCategory>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ResourceCategories", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.Property(x => x.Name).IsRequired().HasMaxLength(128);
            b.Property(x => x.Code).HasMaxLength(64);
            
            b.HasIndex(x => x.ParentId);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<ResourceAudit>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ResourceAudits", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.Property(x => x.Comment).HasMaxLength(1000);
            
            b.HasIndex(x => x.ResourceId);
        });

        builder.Entity<ResourceCollection>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ResourceCollections", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.HasIndex(x => new { x.ResourceId, x.UserId }).IsUnique();
            b.HasIndex(x => x.UserId);
        });

        builder.Entity<PhysicalDeleteRequest>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "PhysicalDeleteRequests", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.Property(x => x.ResourceName).IsRequired().HasMaxLength(256);
            b.Property(x => x.Reason).HasMaxLength(1000);
            b.Property(x => x.RequesterName).HasMaxLength(128);
            b.Property(x => x.ApproverName).HasMaxLength(128);
            
            b.HasIndex(x => x.ResourceId);
            b.HasIndex(x => x.Status);
        });
    }
}
