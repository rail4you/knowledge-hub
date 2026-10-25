using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Courses;

public interface ITeachingClassAppService : IApplicationService
{
    /// <summary>教学班列表（分页）</summary>
    Task<PagedResultDto<TeachingClassDto>> GetListAsync(GetTeachingClassesInput input);

    /// <summary>教学班详情（含成员数）</summary>
    Task<TeachingClassDto> GetAsync(Guid id);

    /// <summary>新建教学班</summary>
    Task<TeachingClassDto> CreateAsync(CreateTeachingClassDto input);

    /// <summary>编辑教学班</summary>
    Task<TeachingClassDto> UpdateAsync(Guid id, UpdateTeachingClassDto input);

    /// <summary>删除教学班（成员关系一并失效，学习记录不受影响）</summary>
    Task DeleteAsync(Guid id);

    /// <summary>批量添加班级成员（已存在的学生自动跳过，不覆盖既有选课与成绩）</summary>
    Task<List<Guid>> AddMembersAsync(Guid classId, AddTeachingClassMembersDto input);

    /// <summary>移除班级成员（不删除其学习与考核记录）</summary>
    Task RemoveMemberAsync(Guid classId, Guid studentId);

    /// <summary>班级成员列表（分页）</summary>
    Task<PagedResultDto<TeachingClassMemberDto>> GetMembersAsync(GetTeachingClassMembersInput input);
}
