using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Exams.Dtos;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Exams;

public interface IExamAppService : IApplicationService
{
    // ── 教师端：试卷管理 ──────────────────────────────────────
    Task<PagedResultDto<ExamListItemDto>> GetListAsync(ExamListQueryDto input);

    Task<ExamDetailDto> GetAsync(Guid id);

    /// <summary>手工组卷（指定题目与每题分值）。</summary>
    Task<ExamDetailDto> CreateAsync(CreateExamDto input);

    /// <summary>自动组卷：按题型 + 每题分 + 数量，或按目标总分自动配平。</summary>
    Task<ExamDetailDto> GenerateAsync(GenerateExamInput input);

    Task<ExamDetailDto> UpdateAsync(Guid id, CreateExamDto input);

    Task DeleteAsync(Guid id);

    // ── 教师端：判卷 ──────────────────────────────────────────
    Task<PagedResultDto<ExamSubmissionItemDto>> GetSubmissionsAsync(ExamSubmissionQueryDto input);

    Task<StudentExamResultDto> GetSubmissionAsync(Guid studentExamId);

    /// <summary>人工判卷（逐题给分 + 评语），并重算总分。</summary>
    Task<StudentExamResultDto> GradeAsync(Guid studentExamId, GradeSubmissionInput input);

    /// <summary>自动判卷：客观题按标准答案判分，主观题走 AI 辅助判分。</summary>
    Task<StudentExamResultDto> AutoGradeAsync(Guid studentExamId);

    // ── 学生端 ────────────────────────────────────────────────
    Task<List<StudentExamListItemDto>> GetMyExamsAsync(Guid courseId);

    /// <summary>开始（或继续）答题，返回试卷题目（不含标准答案）。</summary>
    Task<StudentExamPaperDto> StartAsync(Guid examId);

    Task<StudentExamResultDto> SubmitAsync(Guid studentExamId, SubmitExamInput input);

    Task<StudentExamResultDto> GetMyResultAsync(Guid studentExamId);
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

    /// <summary>
    /// 批量删除习题
    /// </summary>
    Task BatchRemoveAsync(List<Guid> ids);

    /// <summary>
    /// AI 分析习题，生成题目解析和答案
    /// </summary>
    Task<AiAnalyzeExerciseResultDto> AiAnalyzeAsync(AiAnalyzeExerciseInput input);
}
