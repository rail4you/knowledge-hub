using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.ResourceLibraries.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 教师端「教学资源库建设」AppService。
///
/// <para>
/// 需求「教师用户的教学资源库建设」：教师看到与自己相关的教学资源库
/// （被分配的私有库 + 本校公开库 + 参与共建的库），并把文件资源 / 课程 / 实训
/// 共享进库里。
/// </para>
/// </summary>
public interface ITeachingResourceLibraryAppService : IApplicationService
{
    /// <summary>获取「我的教学资源库」列表。</summary>
    Task<List<TeachingLibraryDto>> GetMyLibrariesAsync(TeachingLibraryListRequestDto input);

    /// <summary>获取教学资源库详情。</summary>
    Task<TeachingLibraryDetailDto> GetLibraryAsync(Guid id);

    /// <summary>获取教学资源库内的内容（文件资源 / 课程 / 实训）。</summary>
    Task<PagedResultDto<TeachingLibraryContentDto>> GetContentsAsync(Guid id, TeachingLibraryContentListRequestDto input);

    /// <summary>把已有内容共享进教学资源库。</summary>
    Task<TeachingLibraryContentDto> AttachAsync(Guid id, AttachLibraryContentDto input);

    /// <summary>把内容移出教学资源库。</summary>
    Task DetachAsync(Guid id, LibraryContentType contentType, Guid contentId);

    /// <summary>在教学资源库内新建文件资源。</summary>
    Task<TeachingLibraryContentDto> CreateResourceAsync(Guid id, CreateTeachingLibraryResourceDto input);

    /// <summary>在教学资源库内新建课程。</summary>
    Task<TeachingLibraryContentDto> CreateCourseAsync(Guid id, CreateTeachingLibraryCourseDto input);

    /// <summary>在教学资源库内新建实训。</summary>
    Task<TeachingLibraryContentDto> CreatePracticumAsync(Guid id, CreateTeachingLibraryPracticumDto input);
}
