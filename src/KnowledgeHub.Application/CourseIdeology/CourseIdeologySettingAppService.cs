using System;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.CourseIdeology.Dtos;
using KnowledgeHub.Courses;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.CourseIdeology;

/// <summary>
/// 课程级「课程思政」设置：本模块与课程体系的绑定开关 + 课程思政总目标。
///
/// <para>权限刻意用课程权限（读 = <c>Courses</c>，写 = <c>Courses.Edit</c>）：
/// 这是"课程的一个属性"，不是思政模块内部的独立能力，避免老师有课程编辑权却打不开开关。</para>
/// </summary>
[Authorize(KnowledgeHubPermissions.Courses.Default)]
public class CourseIdeologySettingAppService : KnowledgeHubAppService, ICourseIdeologySettingAppService
{
    private readonly IRepository<CourseIdeologySetting, Guid> _settingRepository;
    private readonly IRepository<IdeologyCase, Guid> _caseRepository;
    private readonly IRepository<Course, Guid> _courseRepository;

    public CourseIdeologySettingAppService(
        IRepository<CourseIdeologySetting, Guid> settingRepository,
        IRepository<IdeologyCase, Guid> caseRepository,
        IRepository<Course, Guid> courseRepository)
    {
        _settingRepository = settingRepository;
        _caseRepository = caseRepository;
        _courseRepository = courseRepository;
    }

    public async Task<CourseIdeologySettingDto> GetAsync(Guid courseId)
    {
        var course = await _courseRepository.FindAsync(courseId)
                     ?? throw new UserFriendlyException("课程不存在或无权访问。");

        var query = await _settingRepository.GetQueryableAsync();
        var setting = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.CourseId == courseId));

        var caseQuery = await _caseRepository.GetQueryableAsync();
        var caseCount = await AsyncExecuter.CountAsync(caseQuery.Where(x => x.CourseId == courseId));

        return new CourseIdeologySettingDto
        {
            CourseId = courseId,
            CourseTitle = course.Title,
            // 没有配置行 = 默认启用（模块装上就能用，不需要每门课先点一次开）。
            IsEnabled = setting?.IsEnabled ?? true,
            IdeologyGoal = setting?.IdeologyGoal,
            Note = setting?.Note,
            CaseCount = caseCount,
        };
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<CourseIdeologySettingDto> SaveAsync(SaveCourseIdeologySettingDto input)
    {
        var course = await _courseRepository.FindAsync(input.CourseId)
                     ?? throw new UserFriendlyException("课程不存在或无权访问。");

        var query = await _settingRepository.GetQueryableAsync();
        var setting = await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.CourseId == input.CourseId));

        if (setting == null)
        {
            setting = new CourseIdeologySetting(GuidGenerator.Create(), input.CourseId, CurrentTenant.Id);
            setting.IsEnabled = input.IsEnabled;
            setting.IdeologyGoal = TrimToNull(input.IdeologyGoal);
            setting.Note = TrimToNull(input.Note);
            await _settingRepository.InsertAsync(setting, autoSave: true);
        }
        else
        {
            setting.IsEnabled = input.IsEnabled;
            setting.IdeologyGoal = TrimToNull(input.IdeologyGoal);
            setting.Note = TrimToNull(input.Note);
            await _settingRepository.UpdateAsync(setting, autoSave: true);
        }

        var caseQuery = await _caseRepository.GetQueryableAsync();
        var caseCount = await AsyncExecuter.CountAsync(caseQuery.Where(x => x.CourseId == input.CourseId));

        return new CourseIdeologySettingDto
        {
            CourseId = input.CourseId,
            CourseTitle = course.Title,
            IsEnabled = setting.IsEnabled,
            IdeologyGoal = setting.IdeologyGoal,
            Note = setting.Note,
            CaseCount = caseCount,
        };
    }

    private static string? TrimToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
