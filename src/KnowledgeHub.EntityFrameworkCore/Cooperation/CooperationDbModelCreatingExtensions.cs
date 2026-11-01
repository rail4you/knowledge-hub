using KnowledgeHub.Cooperation;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.Cooperation;

public static class CooperationDbModelCreatingExtensions
{
    public static void ConfigureCooperation(this ModelBuilder builder)
    {
        builder.Entity<CooperationDemand>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "CooperationDemands", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(CooperationDemand.MaxTitleLength);
            b.Property(x => x.Majors).HasMaxLength(500);
            b.Property(x => x.Industries).HasMaxLength(500);
            b.Property(x => x.Region).HasMaxLength(CooperationDemand.MaxRegionLength);
            b.Property(x => x.Background).HasMaxLength(CooperationDemand.MaxTextLength);
            b.Property(x => x.EnterpriseInput).HasMaxLength(CooperationDemand.MaxTextLength);
            b.Property(x => x.SchoolConditions).HasMaxLength(CooperationDemand.MaxTextLength);
            b.Property(x => x.DeliveryAcceptance).HasMaxLength(CooperationDemand.MaxTextLength);
            b.Property(x => x.IpTerms).HasMaxLength(CooperationDemand.MaxTextLength);
            b.Property(x => x.Cycle).HasMaxLength(128);
            b.Property(x => x.Scale).HasMaxLength(128);
            b.Property(x => x.BudgetNote).HasMaxLength(500);
            b.Property(x => x.ContactName).HasMaxLength(CooperationDemand.MaxContactNameLength);
            b.Property(x => x.ContactPhone).HasMaxLength(CooperationDemand.MaxContactPhoneLength);
            b.Property(x => x.ContactEmail).HasMaxLength(CooperationDemand.MaxContactEmailLength);
            b.Property(x => x.RejectReason).HasMaxLength(500);

            b.HasIndex(x => new { x.Status, x.Type });
            b.HasIndex(x => x.EnterpriseProfileId);
            b.HasIndex(x => x.TenantId);
            b.HasMany(x => x.Responses).WithOne().HasForeignKey(x => x.DemandId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CooperationResponse>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "CooperationResponses", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.ResponderName).HasMaxLength(256);
            b.Property(x => x.Note).HasMaxLength(CooperationResponse.MaxNoteLength);
            b.Property(x => x.DecisionNote).HasMaxLength(500);

            b.HasIndex(x => new { x.DemandId, x.SchoolTenantId }).IsUnique();
            b.HasIndex(x => x.SchoolTenantId);
        });

        builder.Entity<CooperationProject>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "CooperationProjects", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(CooperationProject.MaxNameLength);
            b.Property(x => x.AgreementRef).HasMaxLength(CooperationProject.MaxAgreementRefLength);
            b.Property(x => x.AcceptanceNote).HasMaxLength(1000);

            b.HasIndex(x => new { x.SchoolTenantId, x.Stage });
            b.HasIndex(x => x.EnterpriseProfileId);
            b.HasMany(x => x.Milestones).WithOne().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProjectMilestone>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ProjectMilestones", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(ProjectMilestone.MaxNameLength);
            b.Property(x => x.EvidenceRef).HasMaxLength(ProjectMilestone.MaxEvidenceRefLength);
            b.Property(x => x.Note).HasMaxLength(1000);

            b.HasIndex(x => x.ProjectId);
        });
    }
}
