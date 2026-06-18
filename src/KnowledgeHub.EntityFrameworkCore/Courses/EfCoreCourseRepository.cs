using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Enums;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace KnowledgeHub.EntityFrameworkCore.Courses;

public class EfCoreCourseRepository : EfCoreRepository<KnowledgeHubDbContext, Course, Guid>, ICourseRepository
{
    public EfCoreCourseRepository(IDbContextProvider<KnowledgeHubDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<Course>> GetListAsync(
        int skipCount,
        int maxResultCount,
        string? sorting = null,
        string? filter = null,
        Guid? majorId = null,
        string? semester = null,
        int? difficulty = null,
        Guid? categoryId = null,
        Guid? teacherId = null,
        bool? publishedOnly = false)
    {
        var query = await ApplyFilterAsync(skipCount, maxResultCount, sorting, filter, majorId, semester, difficulty, categoryId, teacherId, publishedOnly ?? false);
        return await query.ToListAsync();
    }

    public async Task<long> GetCountAsync(
        string? filter = null,
        Guid? majorId = null,
        string? semester = null,
        int? difficulty = null,
        Guid? categoryId = null,
        Guid? teacherId = null,
        bool? publishedOnly = false)
    {
        var query = await ApplyFilterAsync(0, int.MaxValue, null, filter, majorId, semester, difficulty, categoryId, teacherId, publishedOnly ?? false);
        return await query.LongCountAsync();
    }

    public async Task<Course?> GetWithDetailsAsync(Guid id)
    {
        var query = await GetQueryableAsync();
        return await query
            .Include(c => c.Chapters)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<Course>> GetByTeacherIdAsync(Guid teacherId)
    {
        var query = await GetQueryableAsync();
        return await query.Where(c => c.TeacherId == teacherId).ToListAsync();
    }

    public async Task<List<Course>> GetPublishedCoursesAsync()
    {
        var query = await GetQueryableAsync();
        return await query
            .Where(c => c.Status == CourseStatus.Published)
            .ToListAsync();
    }

    protected async Task<IQueryable<Course>> ApplyFilterAsync(
        int skipCount,
        int maxResultCount,
        string? sorting,
        string? filter,
        Guid? majorId,
        string? semester,
        int? difficulty,
        Guid? categoryId,
        Guid? teacherId,
        bool publishedOnly)
    {
        var query = await GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(c => c.Title.Contains(filter!) || (c.Description != null && c.Description.Contains(filter!)));
        }

        if (majorId.HasValue)
        {
            query = query.Where(c => c.MajorId == majorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(semester))
        {
            query = query.Where(c => c.Semester == semester);
        }

        if (difficulty.HasValue)
        {
            query = query.Where(c => c.Difficulty == difficulty.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(c => c.CategoryId == categoryId.Value);
        }

        if (teacherId.HasValue)
        {
            query = query.Where(c => c.TeacherId == teacherId.Value);
        }

        if (publishedOnly == true)
        {
            query = query.Where(c => c.Status == CourseStatus.Published);
        }

        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? "CreationTime desc" : sorting!);

        return query.Skip(skipCount).Take(maxResultCount);
    }
}