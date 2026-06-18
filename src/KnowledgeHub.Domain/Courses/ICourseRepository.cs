using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Courses;

public interface ICourseRepository : IRepository<Course, Guid>
{
    Task<List<Course>> GetListAsync(
        int skipCount,
        int maxResultCount,
        string? sorting = null,
        string? filter = null,
        Guid? majorId = null,
        string? semester = null,
        int? difficulty = null,
        Guid? categoryId = null,
        Guid? teacherId = null,
        bool? publishedOnly = false);

    Task<long> GetCountAsync(
        string? filter = null,
        Guid? majorId = null,
        string? semester = null,
        int? difficulty = null,
        Guid? categoryId = null,
        Guid? teacherId = null,
        bool? publishedOnly = false);
    
    Task<Course?> GetWithDetailsAsync(Guid id);
    
    Task<List<Course>> GetByTeacherIdAsync(Guid teacherId);
    
    Task<List<Course>> GetPublishedCoursesAsync();
}
