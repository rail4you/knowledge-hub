using System;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Courses;

/// <summary>
/// 课程案例库：案例支持手动录入、Excel 导入、AI 生成三种来源。
/// AI 生成走 AI 任务通道（AiTaskType.CaseAnalysis），完成后由后台任务自动落库。
/// </summary>
public interface ICourseCaseAppService : IApplicationService
{
    Task<PagedResultDto<CourseCaseDto>> GetListAsync(CourseCaseQueryDto input);

    Task<CourseCaseDto> GetAsync(Guid id);

    Task<CourseCaseDto> CreateAsync(CreateCourseCaseDto input);

    Task<CourseCaseDto> UpdateAsync(Guid id, UpdateCourseCaseDto input);

    Task DeleteAsync(Guid id);

    /// <summary>Excel 批量导入（Source = Import）。</summary>
    Task<CourseCaseImportResultDto> ImportFromExcelAsync(Guid courseId, IFormFile file);
}
