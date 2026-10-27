using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.ResourceLibraries;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.ResourceLibraries.Dtos;

/// <summary>「其他学员」档案（PRD 5.3）。</summary>
public class ResourceLibraryLearnerDto : FullAuditedEntityDto<Guid>
{
    public Guid LibraryId { get; set; }

    public string? LibraryName { get; set; }

    public Guid UserId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public bool IsPhoneVerified { get; set; }

    public string? RealName { get; set; }

    public string? IdCardMasked { get; set; }

    public bool IsRealNameVerified { get; set; }

    public DateTime? FirstLearningAt { get; set; }

    public DateTime EnrolledAt { get; set; }

    public ResourceLibraryLearnerStatus Status { get; set; }

    public ResourceLibraryLearnerSource Source { get; set; }

    public Guid? OwnerTeacherId { get; set; }

    public string? OwnerTeacherName { get; set; }

    /// <summary>已发放的结业证书数。</summary>
    public int CertificateCount { get; set; }
}

/// <summary>登记为「其他学员」（手机号验证 + 实名认证）。</summary>
public class RegisterOtherLearnerInput
{
    /// <summary>目标资源库 Id。</summary>
    public Guid LibraryId { get; set; }

    /// <summary>手机号（需与账号绑定手机号一致）。</summary>
    public string Phone { get; set; } = string.Empty;

    /// <summary>真实姓名。</summary>
    public string RealName { get; set; } = string.Empty;

    /// <summary>身份证号（脱敏存储）。</summary>
    public string? IdCardNo { get; set; }
}

/// <summary>「其他学员」列表查询。</summary>
public class GetResourceLibraryLearnersInput : PagedAndSortedResultRequestDto
{
    /// <summary>资源库 Id（为空表示当前教师名下全部库）。</summary>
    public Guid? LibraryId { get; set; }

    /// <summary>来源过滤（其他学员 / 班级导入学员）。</summary>
    public ResourceLibraryLearnerSource? Source { get; set; }

    public string? Filter { get; set; }
}
