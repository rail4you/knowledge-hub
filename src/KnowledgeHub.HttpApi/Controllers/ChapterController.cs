using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Dtos;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace KnowledgeHub.HttpApi.Controllers;

[Area("app")]
[Route("api/app/chapter")]
public class ChapterController : AbpController, IChapterAppService
{
    private readonly IChapterAppService _chapterService;

    public ChapterController(IChapterAppService chapterService)
    {
        _chapterService = chapterService;
    }

    [HttpPost]
    public Task<ChapterDto> CreateAsync([FromBody] CreateUpdateChapterDto input)
    {
        return _chapterService.CreateAsync(input);
    }

    [HttpDelete]
    [Route("{id:Guid}")]
    public Task DeleteAsync(Guid id)
    {
        return _chapterService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("{id:Guid}")]
    public Task<ChapterDto> GetAsync(Guid id)
    {
        return _chapterService.GetAsync(id);
    }

    [HttpGet]
    [Route("by-course/{courseId:Guid}")]
    public Task<List<ChapterDto>> GetChaptersByCourseAsync(Guid courseId)
    {
        return _chapterService.GetChaptersByCourseAsync(courseId);
    }

    [HttpGet]
    [Route("chapter-tree/{courseId:Guid}")]
    public Task<List<ChapterDto>> GetChapterTreeAsync(Guid courseId)
    {
        return _chapterService.GetChapterTreeAsync(courseId);
    }

    [HttpGet]
    public Task<PagedResultDto<ChapterDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        return _chapterService.GetListAsync(input);
    }

    [HttpPut]
    [Route("{id:Guid}")]
    public Task<ChapterDto> UpdateAsync(Guid id, [FromBody] CreateUpdateChapterDto input)
    {
        return _chapterService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("by-course/{courseId:Guid}")]
    public Task DeleteByCourseAsync(Guid courseId)
    {
        return _chapterService.DeleteByCourseAsync(courseId);
    }
}