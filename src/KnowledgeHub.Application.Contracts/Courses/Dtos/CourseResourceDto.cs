using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;
using KnowledgeHub.Resources.Enums;

namespace KnowledgeHub.Courses.Dtos;

public class CourseResourceDto : EntityDto<Guid>
{
    public Guid CourseId { get; set; }
    public Guid ResourceId { get; set; }
    public string? DisplayName { get; set; }
    public int SortOrder { get; set; }
    /// <summary>是否推荐该课程资源</summary>
    public bool IsRecommended { get; set; }
    public DateTime CreationTime { get; set; }

    // Resource 字段
    public string? ResourceName { get; set; }
    public string? Description { get; set; }
    public string? FilePath { get; set; }
    public string? Keywords { get; set; }
    public string? OriginalFileName { get; set; }
    public string? FileExtension { get; set; }
    public long? FileSize { get; set; }
    public ResourceType ResourceType { get; set; }
    public bool IsDownloadable { get; set; }

    /// <summary>资源审核状态（课程内可用性判定：SchoolApproved / LeagueApproved 才可用于教学）。</summary>
    public KnowledgeHub.Resources.Enums.ResourceStatus Status { get; set; }

    /// <summary>是否为 AI 生成资源（教学图片 / 短视频）。</summary>
    public bool IsAiGenerated { get; set; }

    /// <summary>资源本身的创建时间（区别于关联记录时间）。</summary>
    public DateTime? ResourceCreationTime { get; set; }

    /// <summary>资源创建者 Id（用于判定「创建者可删除自己的未过审资源」）。</summary>
    public Guid? ResourceCreatorId { get; set; }

    /// <summary>
    /// 当前用户是否可直接删除该资源：仅创建者本人、且资源尚未审核通过（草稿/待审核/已驳回）。
    /// 已审核通过的资源仍需走审核流程，避免绕过审核直接删除已发布内容。
    /// </summary>
    public bool CanDelete { get; set; }
}

public class CreateCourseResourceDto
{
    [Required]
    public Guid CourseId { get; set; }

    [Required]
    public Guid ResourceId { get; set; }

    /// <summary>可选，覆盖默认显示名</summary>
    [StringLength(256)]
    public string? DisplayName { get; set; }

    public int SortOrder { get; set; }

    /// <summary>是否推荐该课程资源</summary>
    public bool IsRecommended { get; set; }
}

public class UpdateCourseResourceDto
{
    [StringLength(256)]
    public string? DisplayName { get; set; }

    public int SortOrder { get; set; }

    /// <summary>是否推荐该课程资源</summary>
    public bool IsRecommended { get; set; }
}
