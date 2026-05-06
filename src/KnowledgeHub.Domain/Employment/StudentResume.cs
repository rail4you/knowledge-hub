using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Employment;

public class StudentResume : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }
    public Guid StudentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? SchoolName { get; set; }
    public string? Major { get; set; }
    public string? Grade { get; set; }
    public string? ClassName { get; set; }
    public string? StudentNumber { get; set; }
    public string? Summary { get; set; }
    public string? Skills { get; set; }
    public string? EducationExperience { get; set; }
    public string? InternshipExperience { get; set; }
    public string? ProjectExperience { get; set; }
    public string? CertificateText { get; set; }
    public string? AttachmentUrl { get; set; }
    public bool IsDefault { get; set; }
    public int VersionNo { get; set; } = 1;
    public DateTime? LastUsedAt { get; set; }

    public StudentResume()
    {
    }

    public StudentResume(Guid id, Guid studentId, string title, string fullName)
        : base(id)
    {
        StudentId = studentId;
        Title = title;
        FullName = fullName;
    }
}
