using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Exams.Dtos;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Exams;

public interface IExamAppService : ICrudAppService<
    ExamDto, 
    Guid, 
    PagedAndSortedResultRequestDto, 
    CreateUpdateExamDto>
{
    Task<List<ExamDto>> GetByCourseAsync(Guid courseId);
    Task<StudentExamDto> StartExamAsync(Guid examId);
    Task<StudentExamDto> SubmitExamAsync(Guid studentExamId, SubmitExamInput input);
    Task<StudentExamDto> GetStudentExamAsync(Guid studentExamId);
}

public interface IExerciseAppService : ICrudAppService<
    ExerciseDto,
    Guid,
    PagedAndSortedResultRequestDto,
    CreateUpdateExerciseDto>
{
    Task<List<ExerciseDto>> GetByCourseAsync(Guid courseId);
    Task<List<ExerciseDto>> GetByChapterAsync(Guid chapterId);
    Task<List<ExerciseDto>> GenerateByAIAsync(GenerateExerciseInput input);
    Task<GradingResultDto> GradeEssayAsync(GradeEssayInput input);

    /// <summary>
    /// 从 Excel 文件导入习题
    /// </summary>
    Task<ExerciseImportResultDto> ImportFromExcelAsync(Guid courseId, IFormFile file);
}
