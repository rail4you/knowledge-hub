using System;
using System.Collections.Generic;

namespace KnowledgeHub.Studios.Dtos;

public class StudioMemberDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Role { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}

public class StudioListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public Guid? LeaderUserId { get; set; }
    public string? LeaderName { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public int PublishedCount { get; set; }
    public int PendingCount { get; set; }
    public bool IsLeader { get; set; }
    public bool IsMember { get; set; }
    public bool CanReview { get; set; }
    public DateTime CreationTime { get; set; }
}

public class StudioDetailDto : StudioListItemDto
{
    public List<StudioMemberDto> Members { get; set; } = new();
    public List<StudioMaterialDto> Materials { get; set; } = new();
}

public class StudioMaterialDto
{
    public Guid Id { get; set; }
    public Guid StudioId { get; set; }
    public Guid ResourceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string SubmittedByName { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? FileExtension { get; set; }
    public long? FileSize { get; set; }
    public string? OriginalFileName { get; set; }
    /// <summary>提交人本人且处于草稿/驳回 → 可再次提交。</summary>
    public bool CanSubmit { get; set; }
    /// <summary>领衔人且处于待审 → 可审核。</summary>
    public bool CanReview { get; set; }
    /// <summary>已发布或本人/领衔人 → 可预览。</summary>
    public bool CanPreview { get; set; }
}

public class CreateStudioDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    /// <summary>领衔人（可空，建后可再指定）。</summary>
    public Guid? LeaderUserId { get; set; }
}

public class UpdateStudioDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public Guid? LeaderUserId { get; set; }
    /// <summary>1 启用 / 2 停用。</summary>
    public int? Status { get; set; }
}

/// <summary>成员上传资料（先经由资源中心上传文件拿到 resourceId，再挂到工作室）。</summary>
public class CreateStudioMaterialDto
{
    public Guid ResourceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>true = 直接提交审核；false = 存草稿。</summary>
    public bool SubmitNow { get; set; } = true;
}

public class ReviewStudioMaterialDto
{
    /// <summary>true 通过并发布；false 驳回。</summary>
    public bool Approved { get; set; }
    public string? Comment { get; set; }
}

public class StudioMemberInputDto
{
    public Guid UserId { get; set; }
    /// <summary>0 领衔人 / 1 成员。</summary>
    public int Role { get; set; } = 1;
}
