using KnowledgeHub;
using KnowledgeHub.KnowledgeGraph;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

public static class KnowledgeGraphDbModelCreatingExtensions
{
    public static void ConfigureKnowledgeGraph(this ModelBuilder builder)
    {
        builder.Entity<KnowledgeNode>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "KnowledgeNodes", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.Metadata).HasMaxLength(2000);
            
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.KnowledgeResourceId);
            b.HasIndex(x => x.TenantId);
        });
        
        builder.Entity<KnowledgeRelation>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "KnowledgeRelations", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.Property(x => x.Description).HasMaxLength(500);
            
            b.HasIndex(x => x.SourceNodeId);
            b.HasIndex(x => x.TargetNodeId);
            b.HasIndex(x => x.Type);
            b.HasIndex(x => x.TenantId);
        });
    }
}
