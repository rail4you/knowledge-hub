using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.DoubleHigh.Enums;
using KnowledgeHub.Learning;
using KnowledgeHub.Majors;
using KnowledgeHub.ResourceLibraries;
using KnowledgeHub.Resources;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.DoubleHigh;

/// <summary>
/// 「系统取数」指标的取值范围（上下文）。
///
/// <para>
/// 评估对象（资源库 / 专业 / 课程）就是默认上下文；手动填写对象时，
/// 老师需要为每个系统取数指标单独确认范围。范围为空 = 不限定（全校）。
/// </para>
/// </summary>
public sealed class DoubleHighScope
{
    public DoubleHighObjectType Type { get; init; }

    /// <summary>范围 id（资源库 / 专业 / 课程）。为空 = 不限定。</summary>
    public Guid? Id { get; init; }

    public string? Name { get; init; }

    /// <summary>是否未限定范围（按全校统计）。</summary>
    public bool IsUnlimited => !Id.HasValue;

    public static DoubleHighScope Unlimited => new() { Type = DoubleHighObjectType.Manual };
}

/// <summary>
/// 取值范围解析：把「评估对象 + 指标覆盖范围」翻译成具体的课程 / 资源集合。
/// </summary>
public class DoubleHighScopeResolver : ITransientDependency
{
    private readonly IRepository<ResourceLibrary, Guid> _libraryRepository;
    private readonly IRepository<ResourceLibraryCourse, Guid> _libraryCourseRepository;
    private readonly IRepository<ResourceLibraryResource, Guid> _libraryResourceRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<CourseMajor, Guid> _courseMajorRepository;
    private readonly IRepository<CourseResource, Guid> _courseResourceRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<Major, Guid> _majorRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IDataFilter _dataFilter;

    public DoubleHighScopeResolver(
        IRepository<ResourceLibrary, Guid> libraryRepository,
        IRepository<ResourceLibraryCourse, Guid> libraryCourseRepository,
        IRepository<ResourceLibraryResource, Guid> libraryResourceRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<CourseMajor, Guid> courseMajorRepository,
        IRepository<CourseResource, Guid> courseResourceRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<Major, Guid> majorRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IDataFilter dataFilter)
    {
        _libraryRepository = libraryRepository;
        _libraryCourseRepository = libraryCourseRepository;
        _libraryResourceRepository = libraryResourceRepository;
        _courseRepository = courseRepository;
        _courseMajorRepository = courseMajorRepository;
        _courseResourceRepository = courseResourceRepository;
        _resourceRepository = resourceRepository;
        _majorRepository = majorRepository;
        _studentCourseRepository = studentCourseRepository;
        _dataFilter = dataFilter;
    }

    /// <summary>
    /// 解析指标的实际取值范围：
    /// 指标自己指定的范围优先；没指定则跟随项目评估对象；都不是 = 不限定（全校）。
    /// </summary>
    public DoubleHighScope Resolve(DoubleHighProject project, DoubleHighIndicator indicator)
    {
        if (indicator.ScopeId.HasValue)
        {
            return new DoubleHighScope
            {
                Type = indicator.ScopeType ?? DoubleHighObjectType.Manual,
                Id = indicator.ScopeId,
                Name = indicator.ScopeName
            };
        }

        if (project.ObjectId.HasValue && project.ObjectType != DoubleHighObjectType.Manual)
        {
            return new DoubleHighScope
            {
                Type = project.ObjectType,
                Id = project.ObjectId,
                Name = project.ObjectName
            };
        }

        return DoubleHighScope.Unlimited;
    }

    /// <summary>范围的展示文案。</summary>
    public string Describe(DoubleHighScope scope)
    {
        if (scope.IsUnlimited)
        {
            return "全校（未限定）";
        }

        var prefix = scope.Type switch
        {
            DoubleHighObjectType.ResourceLibrary => "资源库",
            DoubleHighObjectType.Major => "专业",
            DoubleHighObjectType.Course => "课程",
            _ => "自定义"
        };

        return $"{prefix}：{scope.Name ?? "-"}";
    }

    /// <summary>
    /// 范围内课程 id 集合；null = 不限定（按租户全部）。
    /// 注意：范围本身就是数据的定义，必须关闭租户过滤器 —— 否则宿主管理员（TenantId = null）
    /// 查不到租户数据，资源库里的共建学校课程也会被过滤掉。
    /// </summary>
    public async Task<List<Guid>?> GetCourseIdsAsync(DoubleHighScope scope)
    {
        if (scope.IsUnlimited)
        {
            return null;
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await LoadCourseIdsAsync(scope);
        }
    }

    private async Task<List<Guid>> LoadCourseIdsAsync(DoubleHighScope scope)
    {
        switch (scope.Type)
        {
            case DoubleHighObjectType.ResourceLibrary:
            {
                var links = await _libraryCourseRepository.GetListAsync(x => x.LibraryId == scope.Id!.Value);
                return links.Select(x => x.CourseId).Distinct().ToList();
            }

            case DoubleHighObjectType.Major:
            {
                // 主专业字段 + 课程—专业关联表，两边都要算上。
                var primary = await _courseRepository.GetListAsync(x => x.MajorId == scope.Id!.Value);
                var linked = await _courseMajorRepository.GetListAsync(x => x.MajorId == scope.Id!.Value);
                return primary.Select(x => x.Id)
                    .Concat(linked.Select(x => x.CourseId))
                    .Distinct()
                    .ToList();
            }

            case DoubleHighObjectType.Course:
                return new List<Guid> { scope.Id!.Value };

            default:
                return null;
        }
    }

    /// <summary>范围内的资源 id 集合；null = 不限定（按租户全部）。</summary>
    public async Task<List<Guid>?> GetResourceIdsAsync(DoubleHighScope scope)
    {
        if (scope.IsUnlimited)
        {
            return null;
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await LoadResourceIdsAsync(scope);
        }
    }

    private async Task<List<Guid>> LoadResourceIdsAsync(DoubleHighScope scope)
    {
        switch (scope.Type)
        {
            case DoubleHighObjectType.ResourceLibrary:
            {
                var links = await _libraryResourceRepository.GetListAsync(x => x.LibraryId == scope.Id!.Value);
                return links.Select(x => x.ResourceId).Distinct().ToList();
            }

            case DoubleHighObjectType.Major:
            {
                var resources = await _resourceRepository.GetListAsync(x => x.MajorId == scope.Id!.Value);
                return resources.Select(x => x.Id).Distinct().ToList();
            }

            case DoubleHighObjectType.Course:
            {
                var links = await _courseResourceRepository.GetListAsync(x => x.CourseId == scope.Id!.Value);
                return links.Select(x => x.ResourceId).Distinct().ToList();
            }

            default:
                return null;
        }
    }

    /// <summary>范围内的学生 id 集合；null = 不限定（按租户全部选课记录）。</summary>
    public async Task<List<Guid>?> GetStudentIdsAsync(DoubleHighScope scope)
    {
        var courseIds = await GetCourseIdsAsync(scope);
        if (courseIds == null)
        {
            return null;
        }

        // 资源库可能包含共建学校的课程，跨租户读取时必须关闭租户过滤器；
        // 注意：查询要在 Disable 作用域内真正执行，不能把 IQueryable 带出去再求值。
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _studentCourseRepository.GetQueryableAsync();
            return await query.Where(x => courseIds.Contains(x.CourseId))
                .Select(x => x.StudentId)
                .Distinct()
                .ToListAsync();
        }
    }

    /// <summary>按 id 取对象名称（用于保存名称快照）。</summary>
    public async Task<string?> ResolveNameAsync(DoubleHighObjectType type, Guid id)
    {
        switch (type)
        {
            case DoubleHighObjectType.ResourceLibrary:
            {
                using (_dataFilter.Disable<IMultiTenant>())
                {
                    var library = await _libraryRepository.FirstOrDefaultAsync(x => x.Id == id);
                    return library?.Name;
                }
            }

            case DoubleHighObjectType.Major:
            {
                // 宿主管理员（TenantId = null）也要能解析租户数据，统一关掉租户过滤器。
                using (_dataFilter.Disable<IMultiTenant>())
                {
                    var major = await _majorRepository.FirstOrDefaultAsync(x => x.Id == id);
                    return major?.Name;
                }
            }

            case DoubleHighObjectType.Course:
            {
                using (_dataFilter.Disable<IMultiTenant>())
                {
                    var course = await _courseRepository.FirstOrDefaultAsync(x => x.Id == id);
                    return course?.Title;
                }
            }

            default:
                return null;
        }
    }
}
