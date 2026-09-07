using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses.Dtos;
using KnowledgeHub.Resources;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Courses;

public class CourseResourceAppService : KnowledgeHubAppService, ICourseResourceAppService
{
    private readonly IRepository<CourseResource, Guid> _courseResourceRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;

    public CourseResourceAppService(
        IRepository<CourseResource, Guid> courseResourceRepository,
        IRepository<Resource, Guid> resourceRepository)
    {
        _courseResourceRepository = courseResourceRepository;
        _resourceRepository = resourceRepository;
    }

    public async Task<List<CourseResourceDto>> GetByCourseAsync(Guid courseId)
    {
        var courseResources = await _courseResourceRepository.GetQueryableAsync();

        // 禁用多租户过滤器，确保跨租户关联的资源也能被查询到
        using (DataFilter.Disable<IMultiTenant>())
        {
            var resources = await _resourceRepository.GetQueryableAsync();

            var joins = await (
                from cr in courseResources
                join r in resources on cr.ResourceId equals r.Id into crGroup
                from r in crGroup.DefaultIfEmpty()
                where cr.CourseId == courseId && r != null
                orderby cr.SortOrder
                select new CourseResourceDto
                {
                    Id = cr.Id,
                    CourseId = cr.CourseId,
                    ResourceId = cr.ResourceId,
                    DisplayName = cr.DisplayName,
                    SortOrder = cr.SortOrder,
                    IsRecommended = cr.IsRecommended,
                    CreationTime = cr.CreationTime,
                    ResourceName = r != null ? r.Name : null,
                    Description = r != null ? r.Description : null,
                    FilePath = r != null ? r.FilePath : null,
                    Keywords = r != null ? r.Keywords : null,
                    OriginalFileName = r != null ? r.OriginalFileName : null,
                    FileExtension = r != null ? r.FileExtension : null,
                    FileSize = r != null ? r.FileSize : null,
                    ResourceType = r != null ? r.ResourceType : Resources.Enums.ResourceType.Document,
                    IsDownloadable = r != null && r.IsDownloadable,
                }
            ).ToListAsync();

            return joins;
        }
    }

    public async Task<List<CourseResourceDto>> GetByResourceAsync(Guid resourceId)
    {
        var courseResources = await _courseResourceRepository.GetQueryableAsync();

        using (DataFilter.Disable<IMultiTenant>())
        {
            var resources = await _resourceRepository.GetQueryableAsync();

            var joins = await (
                from cr in courseResources
                join r in resources on cr.ResourceId equals r.Id into crGroup
                from r in crGroup.DefaultIfEmpty()
                where cr.ResourceId == resourceId && r != null
                orderby cr.CreationTime
                select new CourseResourceDto
                {
                    Id = cr.Id,
                    CourseId = cr.CourseId,
                    ResourceId = cr.ResourceId,
                    DisplayName = cr.DisplayName,
                    SortOrder = cr.SortOrder,
                    IsRecommended = cr.IsRecommended,
                    CreationTime = cr.CreationTime,
                    ResourceName = r != null ? r.Name : null,
                    Description = r != null ? r.Description : null,
                    FilePath = r != null ? r.FilePath : null,
                    Keywords = r != null ? r.Keywords : null,
                    OriginalFileName = r != null ? r.OriginalFileName : null,
                    FileExtension = r != null ? r.FileExtension : null,
                    FileSize = r != null ? r.FileSize : null,
                    ResourceType = r != null ? r.ResourceType : Resources.Enums.ResourceType.Document,
                    IsDownloadable = r != null && r.IsDownloadable,
                }
            ).ToListAsync();

            return joins;
        }
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<CourseResourceDto> CreateAsync(CreateCourseResourceDto input)
    {
        var resource = await _resourceRepository.GetAsync(input.ResourceId);
        if (resource == null)
        {
            throw new UserFriendlyException("指定的资源不存在");
        }

        // 防止同一资源重复关联到同一课程
        var existsQuery = await _courseResourceRepository.GetQueryableAsync();
        var exists = await existsQuery.AnyAsync(x =>
            x.CourseId == input.CourseId && x.ResourceId == input.ResourceId);
        if (exists)
        {
            throw new UserFriendlyException("该资源已关联到当前课程");
        }

        var courseResource = new CourseResource(
            GuidGenerator.Create(),
            input.CourseId,
            input.ResourceId,
            input.DisplayName,
            input.SortOrder,
            input.IsRecommended
        );

        courseResource = await _courseResourceRepository.InsertAsync(courseResource);

        return new CourseResourceDto
        {
            Id = courseResource.Id,
            CourseId = courseResource.CourseId,
            ResourceId = courseResource.ResourceId,
            DisplayName = courseResource.DisplayName,
            SortOrder = courseResource.SortOrder,
            IsRecommended = courseResource.IsRecommended,
            CreationTime = courseResource.CreationTime,
            ResourceName = resource.Name,
            Description = resource.Description,
            FilePath = resource.FilePath,
            Keywords = resource.Keywords,
            OriginalFileName = resource.OriginalFileName,
            FileExtension = resource.FileExtension,
            FileSize = resource.FileSize,
            ResourceType = resource.ResourceType,
            IsDownloadable = resource.IsDownloadable,
        };
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task<CourseResourceDto> UpdateAsync(Guid id, UpdateCourseResourceDto input)
    {
        var courseResource = await _courseResourceRepository.GetAsync(id);
        courseResource.DisplayName = input.DisplayName;
        courseResource.SortOrder = input.SortOrder;
        courseResource.IsRecommended = input.IsRecommended;
        await _courseResourceRepository.UpdateAsync(courseResource);

        Resource? resource = null;
        using (DataFilter.Disable<IMultiTenant>())
        {
            resource = await _resourceRepository.FindAsync(courseResource.ResourceId);
        }

        return new CourseResourceDto
        {
            Id = courseResource.Id,
            CourseId = courseResource.CourseId,
            ResourceId = courseResource.ResourceId,
            DisplayName = courseResource.DisplayName,
            SortOrder = courseResource.SortOrder,
            IsRecommended = courseResource.IsRecommended,
            CreationTime = courseResource.CreationTime,
            ResourceName = resource?.Name,
            Description = resource?.Description,
            FilePath = resource?.FilePath,
            Keywords = resource?.Keywords,
            OriginalFileName = resource?.OriginalFileName,
            FileExtension = resource?.FileExtension,
            FileSize = resource?.FileSize,
            ResourceType = resource?.ResourceType ?? Resources.Enums.ResourceType.Document,
            IsDownloadable = resource?.IsDownloadable ?? false,
        };
    }

    [Authorize(KnowledgeHubPermissions.Courses.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        await _courseResourceRepository.DeleteAsync(id);
    }
}
