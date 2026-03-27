using KnowledgeHub.Alliance;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using AllianceEntity = KnowledgeHub.Alliance.Alliance;
using AllianceMemberEntity = KnowledgeHub.Alliance.AllianceMember;
using AllianceAuditEntity = KnowledgeHub.Alliance.AllianceAudit;

namespace KnowledgeHub.EntityFrameworkCore;

public static class AllianceDbModelCreatingExtensions
{
    public static void ConfigureAlliance(this ModelBuilder builder)
    {
        builder.Entity<AllianceEntity>(b =>
        {
            b.ToTable("Alliances");
            b.ConfigureByConvention();

            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasMaxLength(500);

            b.HasMany(x => x.Members)
                .WithOne()
                .HasForeignKey(x => x.AllianceId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(x => x.Audits)
                .WithOne()
                .HasForeignKey(x => x.AllianceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AllianceMemberEntity>(b =>
        {
            b.ToTable("AllianceMembers");
            b.ConfigureByConvention();

            b.Property(x => x.TenantName).HasMaxLength(200).IsRequired();

            b.HasIndex(x => new { x.AllianceId, x.MemberTenantId }).IsUnique();
            b.HasIndex(x => x.MemberTenantId);
        });

        builder.Entity<AllianceAuditEntity>(b =>
        {
            b.ToTable("AllianceAudits");
            b.ConfigureByConvention();

            b.Property(x => x.ApproverTenantName).HasMaxLength(200).IsRequired();
            b.Property(x => x.Comment).HasMaxLength(500);

            b.HasIndex(x => new { x.AllianceId, x.ResourceId });
        });
    }
}
