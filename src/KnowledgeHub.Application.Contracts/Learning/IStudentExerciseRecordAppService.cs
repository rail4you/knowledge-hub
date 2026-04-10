using System;
using System.Threading.Tasks;
using KnowledgeHub.Learning.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace KnowledgeHub.Learning;

public interface IStudentExerciseRecordAppService : IApplicationService
{
    // Student APIs
    Task<StudentExerciseRecordDto> SaveOrUpdateRecordAsync(SaveExerciseRecordInput input);
    Task<PagedResultDto<StudentExerciseRecordDto>> GetRecordsByCourseAsync(GetStudentExerciseRecordsInput input);
    Task<PagedResultDto<StudentExerciseRecordDto>> GetRecordsByChapterAsync(GetStudentExerciseRecordsInput input);
    Task MarkAnswerViewedAsync(MarkAnswerViewedInput input);
    Task SubmitSelfAssessmentAsync(SubmitSelfAssessmentInput input);
    Task<ListResultDto<ChapterProgressDto>> GetChapterProgressAsync(Guid courseId);

    // Teacher APIs
    Task<PagedResultDto<StudentLearningStatisticsDto>> GetLearningStatisticsAsync(GetLearningStatisticsInput input);
    Task<CourseLearningOverviewDto> GetCourseLearningOverviewAsync(GetCourseLearningOverviewInput input);
    Task<IRemoteStreamContent> ExportLearningStatisticsAsync(GetLearningStatisticsInput input);
}
