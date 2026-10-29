using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;

namespace KnowledgeHub.Application.Identity;

public class TenantUserDto : IdentityUserDto
{
    public Guid? MajorId { get; set; }
    public string? MajorName { get; set; }

    /// <summary>所属教学班 Id（未分班时为空）。</summary>
    public Guid? ClassId { get; set; }

    /// <summary>所属教学班名称（未分班时为空）。</summary>
    public string? ClassName { get; set; }
}
