using System;
using System.Collections.Generic;
using KnowledgeHub.Resources.Enums;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Resources;

public class ResourceDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public ResourceType ResourceType { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Guid? MajorId { get; set; }
    public string? MajorName { get; set; }
    /// <summary>
    /// 所属教学资源库 Id（教学资源库 = 文件资源 + 课程 + 实训的关联集合）。
    /// 非空表示该资源归属某个教学资源库；共建库中其它学校的资源通过库成员关系对本校可见。
    /// </summary>
    public Guid? LibraryId { get; set; }
    /// <summary>资源关联的全部资源库（多对多；为空表示「未分类」）。</summary>
    public List<ResourceLibraryRefDto> Libraries { get; set; } = new();
    /// <summary>资源归属的全部专业 Id（主专业排第一）；与 <see cref="MajorId"/> / <see cref="MajorName"/> 兼容并双写。</summary>
    public List<Guid> MajorIds { get; set; } = new();
    /// <summary>与 <see cref="MajorIds"/> 一一对应的专业名（主专业排第一）。</summary>
    public List<string> MajorNames { get; set; } = new();
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string FileExtension { get; set; }
    public string OriginalFileName { get; set; }
    public ResourceStatus Status { get; set; }
    public int CurrentVersion { get; set; }
    public string? Keywords { get; set; }
    /// <summary>AI 生成的资源摘要（由 DocumentSummaryBackgroundJob 写入）。</summary>
    public string? Summary { get; set; }
    public string? CopyrightInfo { get; set; }
    public bool IsDownloadable { get; set; }
    public int CollectionCount { get; set; }
    public int DownloadCount { get; set; }
    public int ViewCount { get; set; }

    /// <summary>媒体处理状态（缩略图/预览生成），前端可据此展示"处理中"。</summary>
    public ResourceMediaStatus MediaStatus { get; set; }

    /// <summary>是否为 AI 生成资源（教学图片 / 短视频），列表页用于打「AI生成」标签。</summary>
    public bool IsAiGenerated { get; set; }

    // ── 来源（课程资源 ⇄ 教学资源库资源 互转）─────────────────────
    /// <summary>来源类型：0 新建 / 1 从课程转换 / 2 从资源库转换。</summary>
    public ResourceSourceType SourceType { get; set; }

    /// <summary>来源展示文案：「新建」/「来自课程：X」/「来自资源库：Y」。</summary>
    public string? SourceName { get; set; }

    /// <summary>转换前的源资源 Id（溯源用）。</summary>
    public Guid? SourceResourceId { get; set; }

    /// <summary>是否文件缺失：FilePath 已配置，但磁盘上对应物理文件不存在。</summary>
    /// <remarks>前端据此禁用下载/预览按钮并给出提示；后端资源列表据此标记。由 AppService 填充。</remarks>
    public bool IsFileMissing { get; set; }

    /// <summary>
    /// 是否已建页面/视频索引（可用于 AI 文档问答）。
    /// true：资源有 PageContent（文档/PDF/PPT 等）或视频已完成索引；
    /// false：未建立索引或索引中。
    /// 前端据此决定是否显示「基于资源提问」入口并跳转到 AI 聊天。
    /// 仅 <see cref="GetWithVersionsAsync"/> 与 <see cref="GetListAsync"/> 等详情/列表 DTO 出口填充。
    /// </summary>
    public bool IsIndexed { get; set; }

    public Guid? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }    // CreatorId 复用基类 FullAuditedEntityDto<Guid>.CreatorId（Guid?）。
    // 此前这里曾用 `public Guid CreatorId` 隐藏基类（CS0108），导致 Mapperly 在
    // Guid? -> Guid 映射时处理脆弱、历史数据的 NULL 变成 Guid.Empty，
    // FillCreatorNamesAsync 按 Guid.Empty 过滤后直接返回，列表创建人全空。
    public string? CreatorName { get; set; }

    /// <summary>所属教学资源库名称。</summary>
    public string? LibraryName { get; set; }

    /// <summary>
    /// 是否为「共建库共享进来」的资源。
    /// true：资源归属其它学校，通过院校共建库对当前学校可见；
    /// false：资源属于当前学校（自己创建的或本租户内他人创建的）。
    /// 前端资源列表据此显示「来源」列与详情「资源库」信息块。
    /// </summary>
    public bool IsSharedIn { get; set; }

    /// <summary>资源归属学校 Id（共建库中可能不是当前学校）。</summary>
    public Guid? OwnerTenantId { get; set; }

    /// <summary>资源归属学校租户 Id（由 Resource.TenantId 映射，共建库中可能不是当前学校）。</summary>
    public Guid? TenantId { get; set; }

    /// <summary>资源归属学校名称（共建库中可能不是当前学校）。</summary>
    public string? OwnerTenantName { get; set; }

    /// <summary>资源关联的课程（多对多，可为空）。</summary>
    public List<ResourceCourseRefDto> Courses { get; set; } = new();
}

public class ResourceCourseRefDto
{
    public Guid CourseId { get; set; }
    public string? CourseName { get; set; }
}

public class ResourceVersionDto : EntityDto<Guid>
{
    public Guid ResourceId { get; set; }
    public int Version { get; set; }
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string? UpdateContent { get; set; }
    public bool IsCurrentVersion { get; set; }
    public DateTime CreationTime { get; set; }
    // 与源实体 AuditedEntity<Guid>.CreatorId（Guid?）保持一致，避免 Guid? -> Guid 映射问题。
    public Guid? CreatorId { get; set; }
    public string? CreatorName { get; set; }
}

public class ResourceCategoryDto : FullAuditedEntityDto<Guid>
{
    public string Name { get; set; }
    public Guid? ParentId { get; set; }
    public string? ParentName { get; set; }
    public string? Code { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    /// <summary>该分类下的资源总数（包括子分类下的资源）。</summary>
    public int ResourceCount { get; set; }
    public List<ResourceCategoryDto> Children { get; set; } = new();
}

public class ResourceAuditDto : EntityDto<Guid>
{
    public Guid ResourceId { get; set; }
    public AuditType AuditType { get; set; }
    public AuditStatus Status { get; set; }
    public string? Comment { get; set; }
    public Guid AuditorId { get; set; }
    public string? AuditorName { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CreateUpdateResourceDto
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public ResourceType ResourceType { get; set; }
    public Guid? CategoryId { get; set; }
    /// <summary>主专业（兼容老字段；与 <see cref="MajorIds"/> 同时传时以后端归一化结果为准）。</summary>
    public Guid? MajorId { get; set; }
    /// <summary>资源归属的全部专业（主专业排第一）；为空表示“无专业归属”。</summary>
    public List<Guid> MajorIds { get; set; } = new();
    public string? Keywords { get; set; }
    public string? CopyrightInfo { get; set; }
    public bool IsDownloadable { get; set; } = true;
    public Guid? OrganizationId { get; set; }

    /// <summary>
    /// 所属资源库（多校共建，兼容旧单值字段）。多库场景请使用 <see cref="LibraryIds"/>；
    /// 只传本字段时后端按「单库」处理。专业必须与所选库的主建专业一致（后端强制校验）。
    /// </summary>
    public Guid? LibraryId { get; set; }

    /// <summary>
    /// 关联的资源库（多选）：一个资源可同时挂进多个库，留空表示「未分类」。
    /// 第一个库会镜像到 <c>Resource.LibraryId</c>（主资源库），用于兼容既有按库归属的页面与统计。
    /// </summary>
    public List<Guid> LibraryIds { get; set; } = new();

    /// <summary>
    /// 关联课程（可选，多选）。资源本身独立于课程存在；
    /// 关联后课程可在「课程资源」中直接使用，并在「章节资源」中落到章节。
    /// </summary>
    public List<Guid> CourseIds { get; set; } = new();

    public string? FilePath { get; set; }
    public long? FileSize { get; set; }
    public string? FileExtension { get; set; }
    public string? OriginalFileName { get; set; }
}

public class CreateUpdateResourceCategoryDto
{
    public string Name { get; set; }
    public Guid? ParentId { get; set; }
    public string? Code { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AuditResourceDto
{
    public Guid ResourceId { get; set; }
    public AuditStatus Status { get; set; }
    public string? Comment { get; set; }
}

public class UploadVersionDto
{
    public Guid ResourceId { get; set; }
    public string? UpdateContent { get; set; }
    public string? FilePath { get; set; }
    public long? FileSize { get; set; }
    public string? FileExtension { get; set; }
    public string? OriginalFileName { get; set; }
}

public class InitiateUploadDto
{
    public string FileName { get; set; }
    public long TotalSize { get; set; }
    public int ChunkSize { get; set; } = 1024 * 1024; // 1MB default
}

public class InitiateUploadResultDto
{
    public string UploadId { get; set; }
    public int ChunkSize { get; set; }
    public int TotalChunks { get; set; }
}

public class UploadChunkDto
{
    public string UploadId { get; set; }
    public string FileName { get; set; }
    public int ChunkNumber { get; set; }
    public bool IsLastChunk { get; set; }
}

public class CompleteUploadDto
{
    public string UploadId { get; set; }
    public string FileName { get; set; }
    public int TotalChunks { get; set; }
}

public class CompleteUploadResultDto
{
    public string FilePath { get; set; }
    public long FileSize { get; set; }
    public string FileExtension { get; set; }
    public string OriginalFileName { get; set; }
}

public class PhysicalDeleteRequestDto : EntityDto<Guid>
{
    public Guid ResourceId { get; set; }
    public string ResourceName { get; set; }
    public string Reason { get; set; }
    public int Status { get; set; }
    public Guid RequesterId { get; set; }
    public string RequesterName { get; set; }
    public Guid? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public DateTime? ApprovalTime { get; set; }
    public DateTime CreationTime { get; set; }
}

public class CreatePhysicalDeleteRequestDto
{
    public Guid ResourceId { get; set; }
    public string Reason { get; set; }
}

/// <summary>资源关联的资源库（轻量引用，用于列表 / 表单展示）。</summary>
public class ResourceLibraryRefDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 资源库维度侧边栏的一项：某个资源库下当前可见资源的数量；
/// <see cref="LibraryId"/> 为 null 表示「未分类」（未关联任何资源库）。
/// </summary>
public class ResourceLibraryFacetDto
{
    public Guid? LibraryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }

    /// <summary>
    /// 库的可见性（0 私有 / 1 公开 / 2 院校共建）。
    /// <see cref="LibraryId"/> 为 null（未分类）时为 null —— 前端据此区分图标。
    /// </summary>
    public int? Visibility { get; set; }
}

/// <summary>
/// 课程维度侧边栏的一项：某门课程下当前可见资源的数量；
/// <see cref="CourseId"/> 为 null 表示「未关联课程」。
/// </summary>
public class ResourceCourseFacetDto
{
    public Guid? CourseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}
