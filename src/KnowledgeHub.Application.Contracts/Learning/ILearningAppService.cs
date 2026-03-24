using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Learning.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Learning;

public interface ILearningAppService : IApplicationService
{
    Task<LearningDashboardDto> GetDashboardAsync();
    Task<List<StudentCourseDto>> GetMyCoursesAsync();
    Task<LearningProgressDto> GetProgressAsync(Guid courseId);
    Task<LearningProgressDto> RecordProgressAsync(RecordProgressInput input);
    Task<List<KnowledgeMasteryDto>> GetKnowledgeMasteryAsync(Guid courseId);
}
