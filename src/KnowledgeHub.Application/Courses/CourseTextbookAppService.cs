using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

/// <summary>
/// 课程教材（一门课多本：主教材 / 参考书 / 实验指导书）。
/// 附件用 <see cref="Resource"/> 承载，不新建文件存储。
/// </summary>
public class CourseTextbookAppService : KnowledgeHubAppService, ICourseTextbookAppService
{
    private readonly IRepository<CourseTextbook, Guid> _textbookRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IDataFilter _dataFilter;

    public CourseTextbookAppService(
        IRepository<CourseTextbook, Guid> textbookRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<Resource, Guid> resourceRepository,
        IDataFilter dataFilter)
    {
        _textbookRepository = textbookRepository;
        _courseRepository = courseRepository;
        _resourceRepository = resourceRepository;
        _dataFilter = dataFilter;
    }

    public async Task<List<CourseTextbookDto>> GetByCourseAsync(Guid courseId)
    {
        List<CourseTextbook> items;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            items = await _textbookRepository.GetListAsync(x => x.CourseId == courseId);
        }

        var ordered = items
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .ToList();

        var dtos = ordered.Select(MapToDto).ToList();
        await FillAttachmentNamesAsync(dtos);
        return dtos;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<CourseTextbookDto> CreateAsync(SaveCourseTextbookDto input)
    {
        var course = await FindCourseAsync(input.CourseId);

        var entity = new CourseTextbook(GuidGenerator.Create(), input.CourseId, input.Title.Trim())
        {
            TenantId = course.TenantId
        };
        Apply(entity, input);

        if (entity.IsPrimary)
        {
            await ClearPrimaryAsync(input.CourseId, null);
        }

        await _textbookRepository.InsertAsync(entity);
        var dto = MapToDto(entity);
        await FillAttachmentNamesAsync(new List<CourseTextbookDto> { dto });
        return dto;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<CourseTextbookDto> UpdateAsync(Guid id, SaveCourseTextbookDto input)
    {
        var entity = await _textbookRepository.GetAsync(id);
        if (entity.CourseId != input.CourseId)
        {
            throw new UserFriendlyException("不能把教材移动到其它课程。");
        }

        entity.Title = input.Title.Trim();
        Apply(entity, input);

        if (entity.IsPrimary)
        {
            await ClearPrimaryAsync(input.CourseId, id);
        }

        await _textbookRepository.UpdateAsync(entity);
        var dto = MapToDto(entity);
        await FillAttachmentNamesAsync(new List<CourseTextbookDto> { dto });
        return dto;
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        // 只删教材行，不动附件资源（资源可能被课程资源页 / 其它课程引用）
        await _textbookRepository.DeleteAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<CourseTextbookDto> SetPrimaryAsync(Guid id)
    {
        var entity = await _textbookRepository.GetAsync(id);
        await ClearPrimaryAsync(entity.CourseId, id);
        entity.IsPrimary = true;
        await _textbookRepository.UpdateAsync(entity);
        var dto = MapToDto(entity);
        await FillAttachmentNamesAsync(new List<CourseTextbookDto> { dto });
        return dto;
    }

    private async Task<Course> FindCourseAsync(Guid courseId)
    {
        Course? course;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            course = await _courseRepository.FindAsync(courseId);
        }

        return course ?? throw new UserFriendlyException("课程不存在。");
    }

    /// <summary>取消同课程其它教材的主教材标记。</summary>
    private async Task ClearPrimaryAsync(Guid courseId, Guid? exceptId)
    {
        List<CourseTextbook> others;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            others = await _textbookRepository.GetListAsync(x =>
                x.CourseId == courseId && x.IsPrimary && x.Id != exceptId);
        }

        foreach (var other in others)
        {
            other.IsPrimary = false;
            await _textbookRepository.UpdateAsync(other);
        }
    }

    private static void Apply(CourseTextbook entity, SaveCourseTextbookDto input)
    {
        entity.Author = input.Author;
        entity.Publisher = input.Publisher;
        entity.Edition = input.Edition;
        entity.Isbn = input.Isbn;
        entity.CoverUrl = input.CoverUrl;
        entity.AttachmentResourceId = input.AttachmentResourceId;
        entity.IsPrimary = input.IsPrimary;
        entity.SortOrder = input.SortOrder;
    }

    /// <summary>批量补附件原始文件名（学生端直接显示可下载名称，避免再查一次）。</summary>
    private async Task FillAttachmentNamesAsync(List<CourseTextbookDto> dtos)
    {
        var resourceIds = dtos
            .Where(x => x.AttachmentResourceId.HasValue)
            .Select(x => x.AttachmentResourceId!.Value)
            .Distinct()
            .ToList();

        if (resourceIds.Count == 0)
        {
            return;
        }

        List<Resource> resources;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            resources = await _resourceRepository.GetListAsync(x => resourceIds.Contains(x.Id));
        }

        var nameMap = resources.ToDictionary(x => x.Id, x => x.OriginalFileName);
        foreach (var dto in dtos)
        {
            if (dto.AttachmentResourceId.HasValue &&
                nameMap.TryGetValue(dto.AttachmentResourceId.Value, out var name))
            {
                dto.AttachmentFileName = name;
            }
        }
    }

    private static CourseTextbookDto MapToDto(CourseTextbook entity)
    {
        return new CourseTextbookDto
        {
            Id = entity.Id,
            CourseId = entity.CourseId,
            Title = entity.Title,
            Author = entity.Author,
            Publisher = entity.Publisher,
            Edition = entity.Edition,
            Isbn = entity.Isbn,
            CoverUrl = entity.CoverUrl,
            AttachmentResourceId = entity.AttachmentResourceId,
            IsPrimary = entity.IsPrimary,
            SortOrder = entity.SortOrder,
            CreationTime = entity.CreationTime,
            CreatorId = entity.CreatorId,
            LastModificationTime = entity.LastModificationTime,
            LastModifierId = entity.LastModifierId
        };
    }
}
