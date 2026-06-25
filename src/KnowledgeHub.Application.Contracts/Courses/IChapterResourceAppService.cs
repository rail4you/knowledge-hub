using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Courses;

public interface IChapterResourceAppService : IApplicationService
{
    /// <summary>获取某章节关联的资源列表</summary>
    Task<List<ChapterResourceDto>> GetByChapterAsync(Guid chapterId);

    /// <summary>将资源文件关联到章节</summary>
    Task<ChapterResourceDto> CreateAsync(CreateChapterResourceDto input);

    /// <summary>取消资源与章节的关联</summary>
    Task DeleteAsync(Guid id);
}
