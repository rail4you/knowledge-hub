using System;
using KnowledgeHub.Employment.Enums;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Employment;

public class JobPosting : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid EmployerUserId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Address { get; set; }
    public EmploymentJobType JobType { get; set; } = EmploymentJobType.FullTime;
    public string? EducationRequirement { get; set; }
    public string? SalaryRange { get; set; }
    public int RecruitmentCount { get; set; } = 1;
    public string? SkillTags { get; set; }
    public string? Benefits { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public DateTime? Deadline { get; set; }
    public EmploymentJobStatus Status { get; set; } = EmploymentJobStatus.Draft;
    public string? ReviewComment { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int ViewCount { get; set; }

    public JobPosting()
    {
    }

    public JobPosting(Guid id, Guid employerUserId, string companyName, string title, string description)
        : base(id)
    {
        EmployerUserId = employerUserId;
        CompanyName = companyName;
        Title = title;
        Description = description;
    }
}
