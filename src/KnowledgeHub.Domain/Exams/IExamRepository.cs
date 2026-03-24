using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Exams;

public interface IExamRepository : IRepository<Exam, Guid>
{
    Task<List<Exam>> GetByCourseIdAsync(Guid courseId);
    Task<Exam?> GetWithExercisesAsync(Guid id);
    Task<List<Exercise>> GetExercisesByCourseAsync(Guid courseId);
    Task<List<Exercise>> GetExercisesByChapterAsync(Guid chapterId);
}
