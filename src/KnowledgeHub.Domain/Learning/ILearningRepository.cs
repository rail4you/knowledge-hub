using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Learning;

public interface ILearningRepository : IRepository<StudentCourse, Guid>
{
    Task<List<StudentCourse>> GetByStudentIdAsync(Guid studentId);
    Task<StudentCourse?> GetByStudentAndCourseAsync(Guid studentId, Guid courseId);
    Task<List<LearningProgress>> GetProgressByStudentAndCourseAsync(Guid studentId, Guid courseId);
    Task<LearningProgress?> GetProgressAsync(Guid studentId, Guid resourceId);
    Task<List<KnowledgeMastery>> GetMasteryByStudentAndCourseAsync(Guid studentId, Guid courseId);
}
