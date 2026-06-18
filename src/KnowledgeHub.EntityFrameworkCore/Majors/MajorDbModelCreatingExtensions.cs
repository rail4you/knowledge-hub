using KnowledgeHub;
using KnowledgeHub.Majors;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.Majors;

public static class MajorDbModelCreatingExtensions
{
    public static void ConfigureMajor(this ModelBuilder builder)
    {
        builder.Entity<Major>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "Majors", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(Major.MaxNameLength);
            b.Property(x => x.Code).HasMaxLength(Major.MaxCodeLength);
            b.Property(x => x.Description).HasMaxLength(Major.MaxDescriptionLength);
            b.Property(x => x.TrainingObjectives).HasMaxLength(Major.MaxTrainingObjectivesLength);

            b.HasIndex(x => x.Name);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Code })
                .IsUnique()
                .HasFilter(null);
        });
    }
}
