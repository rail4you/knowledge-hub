using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Learning.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Learning;

public interface ILearningAppService : IApplicationService
{
    Task<LearningDashboardDto> GetDashboardAsync();
    Task<List<StudentCourseListItemDto>> GetMyCoursesAsync();
    Task<LearningProgressDto> GetProgressAsync(Guid courseId);
    Task<LearningProgressDto> RecordProgressAsync(RecordProgressInput input);
    Task<List<KnowledgeMasteryDto>> GetKnowledgeMasteryAsync(Guid courseId);
}
