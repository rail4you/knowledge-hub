using KnowledgeHub.TeachingAgents;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace KnowledgeHub.EntityFrameworkCore.TeachingAgents;

public static class TeachingAgentDbModelCreatingExtensions
{
    public static void ConfigureTeachingAgents(this ModelBuilder builder)
    {
        builder.Entity<TeachingAgent>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "TeachingAgents", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Name).IsRequired().HasMaxLength(128);
            b.Property(x => x.Description).HasMaxLength(2000);

            b.HasIndex(x => x.OwnerUserId);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.Status);
        });

        builder.Entity<TeachingAgentVersion>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "TeachingAgentVersions", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.SystemPrompt).IsRequired().HasMaxLength(8000);
            b.Property(x => x.WelcomeMessage).HasMaxLength(1000);
            b.Property(x => x.ModelId).IsRequired().HasMaxLength(128);
            b.Property(x => x.SkillsJson).IsRequired().HasMaxLength(8000);
            b.Property(x => x.VersionNote).HasMaxLength(500);

            b.HasIndex(x => new { x.TeachingAgentId, x.VersionNumber }).IsUnique();
            b.HasIndex(x => new { x.TeachingAgentId, x.IsPublished });
        });

        builder.Entity<ClassroomAgentTask>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ClassroomAgentTasks", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Title).IsRequired().HasMaxLength(128);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.TaskPrompt).IsRequired().HasMaxLength(4000);
            b.Property(x => x.TargetSnapshotJson).IsRequired().HasMaxLength(20000);

            b.HasIndex(x => x.TeachingAgentId);
            b.HasIndex(x => x.CreatorUserId);
            b.HasIndex(x => x.PublishStatus);
        });

        builder.Entity<ClassroomAgentAssignment>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "ClassroomAgentAssignments", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.SubmissionSummary).HasMaxLength(4000);
            b.Property(x => x.HelpReason).HasMaxLength(1000);

            b.HasIndex(x => new { x.ClassroomAgentTaskId, x.StudentId }).IsUnique();
            b.HasIndex(x => x.Status);
        });

        builder.Entity<AgentRun>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "AgentRuns", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.ThreadId).IsRequired().HasMaxLength(128);
            b.Property(x => x.LastError).HasMaxLength(2000);

            b.HasIndex(x => x.ClassroomAgentAssignmentId);
            b.HasIndex(x => x.ThreadId).IsUnique();
            b.HasIndex(x => x.RuntimeStatus);
        });

        builder.Entity<AgentRunMessage>(b =>
        {
            b.ToTable(KnowledgeHubConsts.DbTablePrefix + "AgentRunMessages", KnowledgeHubConsts.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Role).IsRequired().HasMaxLength(32);
            b.Property(x => x.Content).IsRequired().HasMaxLength(12000);
            b.Property(x => x.ToolCallsJson).IsRequired().HasMaxLength(8000);

            b.HasIndex(x => x.AgentRunId);
            b.HasIndex(x => x.CreationTime);
        });
    }
}
