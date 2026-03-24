using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Courses;

public interface IChapterAppService : ICrudAppService<
    ChapterDto,
    Guid,
    PagedAndSortedResultRequestDto,
    CreateUpdateChapterDto>
{
    Task<List<ChapterDto>> GetChaptersByCourseAsync(Guid courseId);
    Task<List<ChapterDto>> GetChapterTreeAsync(Guid courseId);
    Task DeleteByCourseAsync(Guid courseId);
}
