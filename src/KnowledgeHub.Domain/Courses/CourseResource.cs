using System;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Auditing;

namespace KnowledgeHub.Courses;

/// <summary>
/// 资源（Resource 文件）与课程的关联表。
/// 教师可先将已上传的资源文件关联到课程，再在「章节资源」页从课程资源池中选择关联到章节，
/// 避免每次从全部资源库中挑选。一个资源可以关联到多个课程。
/// </summary>
public class CourseResource : Entity<Guid>, IMultiTenant, IHasCreationTime
{
    public Guid? TenantId { get; set; }
    public DateTime CreationTime { get; set; }
    public Guid CourseId { get; set; }
    public Guid ResourceId { get; set; }
    /// <summary>在课程内的显示名称，为空时使用 Resource.Name</summary>
    public string? DisplayName { get; set; }
    public int SortOrder { get; set; }
    /// <summary>是否推荐该课程资源：用于课程内资源推荐展示</summary>
    public bool IsRecommended { get; set; }

    private CourseResource() { }

    public CourseResource(Guid id, Guid courseId, Guid resourceId, string? displayName = null, int sortOrder = 0, bool isRecommended = false) : base(id)
    {
        CourseId = courseId;
        ResourceId = resourceId;
        DisplayName = displayName;
        SortOrder = sortOrder;
        IsRecommended = isRecommended;
    }
}
