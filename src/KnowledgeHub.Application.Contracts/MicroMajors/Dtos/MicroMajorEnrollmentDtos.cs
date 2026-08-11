using System;
using System.Collections.Generic;
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
    public string? CertificateImageUrl { get; set; }
}

public class MyMicroMajorDto : MicroMajorDto
{
    public Guid EnrollmentId { get; set; }
    public MicroMajorEnrollmentStatus EnrollmentStatus { get; set; }
    public decimal Progress { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CertificateIssuedAt { get; set; }
    public string? CertificateImageUrl { get; set; }
    public List<MicroMajorCourseDto> Courses { get; set; } = new();
}

public class GetMicroMajorEnrollmentsInput : PagedAndSortedResultRequestDto
{
    public Guid? MicroMajorId { get; set; }
    public Guid? StudentId { get; set; }
    public MicroMajorEnrollmentStatus? Status { get; set; }
}
