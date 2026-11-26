using System;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

/// <summary>
/// 课程教学信息（教学目标 / 毕业要求 / 知识逻辑结构 …）。
/// <para>
/// 读：登录用户即可（学生端课程详情要展示）；
/// 写：<c>KnowledgeHub.Courses.Edit</c>（与课程编辑同一把权限，不另设）。
/// </para>
/// </summary>
public class CourseInfoAppService : KnowledgeHubAppService, ICourseInfoAppService
{
    private readonly IRepository<CourseInfo, Guid> _courseInfoRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IDataFilter _dataFilter;

    public CourseInfoAppService(
        IRepository<CourseInfo, Guid> courseInfoRepository,
        IRepository<Course, Guid> courseRepository,
        IDataFilter dataFilter)
    {
        _courseInfoRepository = courseInfoRepository;
        _courseRepository = courseRepository;
        _dataFilter = dataFilter;
    }

    public async Task<CourseInfoDto> GetByCourseAsync(Guid courseId)
    {
        CourseInfo? info;
        // 共建库共享进来的他校课程：其教学信息属于主建学校租户，
        // 参与校的学生也要能看到，因此读路径关掉租户过滤器（与 CourseAppService 口径一致）。
        using (_dataFilter.Disable<IMultiTenant>())
        {
            info = await _courseInfoRepository.FirstOrDefaultAsync(x => x.CourseId == courseId);
        }

        if (info == null)
        {
            return new CourseInfoDto { CourseId = courseId, HasContent = false };
        }

        return MapToDto(info);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<CourseInfoDto> SaveAsync(SaveCourseInfoDto input)
    {
        Course? course;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            course = await _courseRepository.FindAsync(input.CourseId);
        }

        if (course == null)
        {
            throw new Volo.Abp.UserFriendlyException("课程不存在。");
        }

        CourseInfo? info;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            info = await _courseInfoRepository.FirstOrDefaultAsync(x => x.CourseId == input.CourseId);
        }

        if (info == null)
        {
            info = new CourseInfo(GuidGenerator.Create(), input.CourseId)
            {
                // 教学信息跟随课程归属租户，不跟随操作者 —— 否则他校老师编辑共建课程会写出一条别校数据
                TenantId = course.TenantId
            };
        }

        info.Background = input.Background;
        info.Objectives = input.Objectives;
        info.Target = input.Target;
        info.Highlights = input.Highlights;
        info.Introduction = input.Introduction;
        info.Structure = input.Structure;
        info.GraduationRequirements = input.GraduationRequirements;

        if (info.CreationTime == default)
        {
            await _courseInfoRepository.InsertAsync(info);
        }
        else
        {
            await _courseInfoRepository.UpdateAsync(info);
        }

        return MapToDto(info);
    }

    private static CourseInfoDto MapToDto(CourseInfo info)
    {
        return new CourseInfoDto
        {
            CourseId = info.CourseId,
            Background = info.Background,
            Objectives = info.Objectives,
            Target = info.Target,
            Highlights = info.Highlights,
            Introduction = info.Introduction,
            Structure = info.Structure,
            GraduationRequirements = info.GraduationRequirements,
            HasContent = !string.IsNullOrWhiteSpace(info.Background)
                         || !string.IsNullOrWhiteSpace(info.Objectives)
                         || !string.IsNullOrWhiteSpace(info.Target)
                         || !string.IsNullOrWhiteSpace(info.Highlights)
                         || !string.IsNullOrWhiteSpace(info.Introduction)
                         || !string.IsNullOrWhiteSpace(info.Structure)
                         || !string.IsNullOrWhiteSpace(info.GraduationRequirements)
        };
    }
}
