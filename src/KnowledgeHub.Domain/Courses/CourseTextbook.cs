using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

/// <summary>
/// 课程教材（一门课可挂多本：主教材 / 参考书 / 实验指导书）。
/// <para>
/// 附件不另建存储：<see cref="AttachmentResourceId"/> 指向已有 <c>Resource</c>，
/// 教材电子版因此天然复用资源的下载 / 预览 / 权限链路。
/// </para>
/// </summary>
public class CourseTextbook : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid CourseId { get; set; }

    /// <summary>书名。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>作者 / 主编。</summary>
    public string? Author { get; set; }

    /// <summary>出版社。</summary>
    public string? Publisher { get; set; }

    /// <summary>版次（如「第 3 版」）。</summary>
    public string? Edition { get; set; }

    /// <summary>ISBN。</summary>
    public string? Isbn { get; set; }

    /// <summary>封面图地址（外链或已上传图片）。</summary>
    public string? CoverUrl { get; set; }

    /// <summary>教材附件（电子版 / 目录 / 配套资料），指向 Resource。</summary>
    public Guid? AttachmentResourceId { get; set; }

    /// <summary>是否为该课程主教材（学生端优先展示）。</summary>
    public bool IsPrimary { get; set; }

    public int SortOrder { get; set; }

    private CourseTextbook() { }

    public CourseTextbook(Guid id, Guid courseId, string title) : base(id)
    {
        CourseId = courseId;
        Title = title;
    }
}
