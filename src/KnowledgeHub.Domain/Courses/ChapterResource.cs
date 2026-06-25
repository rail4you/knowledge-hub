using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Auditing;

namespace KnowledgeHub.Courses;

/// <summary>
/// 资源（Resource 文件）与章节的关联表。
/// 教师可将已上传的资源文件关联到课程章节，学生可在章节学习页面查看/下载。
/// </summary>
public class ChapterResource : Entity<Guid>, IMultiTenant, IHasCreationTime
{
    public Guid? TenantId { get; set; }
    public DateTime CreationTime { get; set; }
    public Guid ChapterId { get; set; }
    public Guid ResourceId { get; set; }
    /// <summary>在章节内的显示名称，为空时使用 Resource.OriginalFileName</summary>
    public string? DisplayName { get; set; }
    public int SortOrder { get; set; }

    private ChapterResource() { }

    public ChapterResource(Guid id, Guid chapterId, Guid resourceId, string? displayName = null, int sortOrder = 0) : base(id)
    {
        ChapterId = chapterId;
        ResourceId = resourceId;
        DisplayName = displayName;
        SortOrder = sortOrder;
    }
}
