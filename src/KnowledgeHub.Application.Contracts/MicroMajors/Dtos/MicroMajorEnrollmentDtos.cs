using System;
using KnowledgeHub.MicroMajors.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.MicroMajors.Dtos;

public class MicroMajorEnrollmentDto : FullAuditedEntityDto<Guid>
{
    public Guid MicroMajorId { get; set; }
    public string? MicroMajorTitle { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public MicroMajorEnrollmentStatus Status { get; set; }
    public decimal Progress { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CertificateIssuedAt { get; set; }
}

public class GetMicroMajorEnrollmentsInput : PagedAndSortedResultRequestDto
{
    public Guid? MicroMajorId { get; set; }
    public Guid? StudentId { get; set; }
    public MicroMajorEnrollmentStatus? Status { get; set; }
}
