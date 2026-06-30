using System;
using KnowledgeHub.Practicums.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Practicums;

public class PracticumProject : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public Guid? CourseId { get; set; }
    public string? Major { get; set; }
    public string? ClassName { get; set; }
    public PracticumProjectStatus Status { get; set; } = PracticumProjectStatus.Draft;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal MaxScore { get; set; } = 100;
    public bool AllowResubmission { get; set; } = true;
    public string? AgentName { get; set; }
    public string? AgentPrompt { get; set; }

    public PracticumProject()
    {
    }

    public PracticumProject(Guid id, string title)
        : base(id)
    {
        Title = title;
    }
}
