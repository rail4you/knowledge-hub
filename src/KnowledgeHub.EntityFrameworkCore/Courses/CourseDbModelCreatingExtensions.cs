using KnowledgeHub;
using KnowledgeHub.Courses;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

public static class CourseDbModelCreatingExtensions
{
    public static void ConfigureCourse(this ModelBuilder builder)
    {
        builder.Entity<Course>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "Courses", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.CoverImageUrl).HasMaxLength(512);
            b.Property(x => x.Major).HasMaxLength(128);
            b.Property(x => x.Semester).HasMaxLength(64);
            
            b.HasIndex(x => x.Title);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.Major);
            b.HasIndex(x => x.TeacherId);
            b.HasIndex(x => x.TenantId);
            
            b.HasMany(x => x.Chapters).WithOne().HasForeignKey(x => x.CourseId);
        });
        
        builder.Entity<Chapter>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "Chapters", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.Property(x => x.Title).IsRequired().HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(1000);
            
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.SortOrder);
            
            b.HasMany(x => x.KnowledgeResources).WithOne().HasForeignKey(x => x.ChapterId);
        });
        
        builder.Entity<KnowledgeResource>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "KnowledgeResources", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();
            
            b.Property(x => x.Name).IsRequired().HasMaxLength(256);
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.Content).HasMaxLength(4000);
            b.Property(x => x.ImportanceLevel).HasMaxLength(32);
            b.Property(x => x.Tags).HasMaxLength(500);
            
            b.HasIndex(x => x.CourseId);
            b.HasIndex(x => x.ChapterId);
            b.HasIndex(x => x.ParentId);
            
            b.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId);
        });
    }
}
