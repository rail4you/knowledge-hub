using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Resources.Enums;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Resources.Conversion;

/// <summary>
/// 课程资源 ⇄ 教学资源库资源 互转。
///
/// <para>
/// 平台口径：这两类是<b>独立的两套资源记录</b> ——
/// 库资源独立于课程（不绑定课程），课程里的资源就是课程资源，课程资源不直接共享。
/// 因此「互转」= <b>复制一份新记录 + 写来源标记</b>，转换后两侧各自维护、互不同步；
/// 需要最新内容时重新转一次即可。
/// </para>
///
/// <para>
/// 复制会连带复制当前版本文件（`ResourceVersion`），新资源的审核状态回到「待审核」，
/// 进库仍走资源库准入审核（`ResourcePublication`）。
/// </para>
/// </summary>
public interface IResourceConversionAppService : IApplicationService
{
    /// <summary>某门课程下的课程资源（用于「互转」页面左侧）。</summary>
    Task<PagedResultDto<CourseConvertibleResourceDto>> GetCourseResourcesAsync(CourseResourceQueryDto input);

    /// <summary>某个教学资源库下的库资源（用于「互转」页面右侧）。</summary>
    Task<PagedResultDto<LibraryConvertibleResourceDto>> GetLibraryResourcesAsync(LibraryResourceQueryDto input);

    /// <summary>课程资源 → 教学资源库资源（复制成库资源并挂到目标库，走准入审核）。</summary>
    Task<ResourceConversionResultDto> ConvertToLibraryAsync(ConvertToLibraryInput input);

    /// <summary>教学资源库资源 → 课程资源（复制成课程资源并挂到目标课程）。</summary>
    Task<ResourceConversionResultDto> ConvertToCourseAsync(ConvertToCourseInput input);

    /// <summary>
    /// 「资源互转」右栏的资源库下拉：自己参与的库 ∪ 含自己课程的库 ∪ **自己资源已共享进去的库**。
    /// 最后一个并集很关键：老师的资源被共享到某个库后，即使她没被分配进该库，也要能看到并核对那部分内容。
    /// </summary>
    Task<List<ResourceLibraryOptionDto>> GetMyLibrariesAsync();
}

/// <summary>「资源互转」右栏的资源库下拉项。</summary>
public class ResourceLibraryOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Visibility { get; set; }

    /// <summary>是否属于「本人可见/可参与」的库；false = 仅因本人资源在里面而列出（只读核对）。</summary>
    public bool IsMine { get; set; }
}

/// <summary>「互转」页面：课程资源查询。</summary>
public class CourseResourceQueryDto : PagedAndSortedResultRequestDto
{
    /// <summary>课程 Id（必填）。</summary>
    public Guid CourseId { get; set; }

    /// <summary>按名称过滤。</summary>
    public string? Filter { get; set; }
}

/// <summary>「互转」页面：库资源查询。</summary>
public class LibraryResourceQueryDto : PagedAndSortedResultRequestDto
{
    /// <summary>资源库 Id（必填）。</summary>
    public Guid LibraryId { get; set; }

    /// <summary>按名称过滤。</summary>
    public string? Filter { get; set; }
}

/// <summary>课程资源行（含来源与挂载信息）。</summary>
public class CourseConvertibleResourceDto
{
    /// <summary>资源 Id（`Resource` 主记录）。</summary>
    public Guid ResourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? FileExtension { get; set; }
    public long? FileSize { get; set; }
    public ResourceType ResourceType { get; set; }
    public ResourceStatus Status { get; set; }

    /// <summary>来源：新建 / 来自课程 / 来自资源库。</summary>
    public ResourceSourceType SourceType { get; set; }
    public string? SourceName { get; set; }

    /// <summary>创建人姓名（「从谁那转来的」追溯）。</summary>
    public string? CreatorName { get; set; }
    public DateTime CreationTime { get; set; }

    /// <summary>该资源当前已关联的资源库名称（提示用）。</summary>
    public List<string> LibraryNames { get; set; } = new();

    /// <summary>该资源已共享进去的资源库 Id 集合（用于「是否已在目标库共享过」的逐库判断）。</summary>
    public List<Guid> SharedLibraryIds { get; set; } = new();

    /// <summary>是否已共享到任意资源库（仅用于展示提示，不参与过滤/禁用判断）。</summary>
    public bool IsSharedToLibrary { get; set; }
}

/// <summary>库资源行（含来源）。</summary>
public class LibraryConvertibleResourceDto
{
    public Guid ResourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? FileExtension { get; set; }
    public long? FileSize { get; set; }
    public ResourceType ResourceType { get; set; }
    public ResourceStatus Status { get; set; }
    public ResourceSourceType SourceType { get; set; }
    public string? SourceName { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreationTime { get; set; }

    /// <summary>内容归属学校（共建库里可能是别校）。</summary>
    public string? OwnerTenantName { get; set; }
}

/// <summary>课程资源 → 资源库。</summary>
public class ConvertToLibraryInput
{
    /// <summary>要转换的课程资源 Id。</summary>
    public Guid ResourceId { get; set; }

    /// <summary>来源课程（写进来源标记，便于「来自课程：X」）。</summary>
    public Guid? SourceCourseId { get; set; }

    /// <summary>目标教学资源库。</summary>
    public Guid LibraryId { get; set; }

    /// <summary>新资源名称（留空沿用原名称）。</summary>
    public string? Name { get; set; }
}

/// <summary>资源库资源 → 课程。</summary>
public class ConvertToCourseInput
{
    /// <summary>要转换的库资源 Id。</summary>
    public Guid ResourceId { get; set; }

    /// <summary>来源资源库（写进来源标记，便于「来自资源库：X」）。</summary>
    public Guid LibraryId { get; set; }

    /// <summary>目标课程。</summary>
    public Guid CourseId { get; set; }

    /// <summary>新资源名称（留空沿用原名称）。</summary>
    public string? Name { get; set; }
}

/// <summary>转换结果。</summary>
public class ResourceConversionResultDto
{
    /// <summary>新生成的资源 Id（副本）。</summary>
    public Guid ResourceId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>提示文案：是否需要审核等。</summary>
    public string Message { get; set; } = string.Empty;
}
