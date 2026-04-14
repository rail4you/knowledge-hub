using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using Microsoft.AspNetCore.Http;
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

    /// <summary>
    /// 从 Excel 文件导入章节（多级）
    /// </summary>
    /// <param name="courseId">课程ID</param>
    /// <param name="file">Excel 文件</param>
    /// <returns>导入结果</returns>
    Task<ChapterImportResultDto> ImportFromExcelAsync(Guid courseId, IFormFile file);
}
