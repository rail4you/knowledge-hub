using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Features;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using KnowledgeHub.ResourceLibraries.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Features;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 共建任务 AppService（V2.0 P3）。
///
/// <para>与审核内核的关系：交付物提交时自动发起「库准入审核」（<see cref="IResourcePublicationService"/>），
/// 因为"交了东西给任务"不等于"东西可以进库给别人看"。任务验收只表示交付项达成。</para>
/// </summary>
[IgnoreAntiforgeryToken]
public class BuildTaskAppService : KnowledgeHubAppService, IBuildTaskAppService
{
    private readonly IRepository<ResourceLibraryTemplate, Guid> _templateRepository;
    private readonly IRepository<BuildTaskTemplate, Guid> _taskTemplateRepository;
    private readonly IRepository<BuildTaskTemplateItem, Guid> _taskTemplateItemRepository;
    private readonly IRepository<BuildTask, Guid> _taskRepository;
    private readonly IRepository<BuildTaskItem, Guid> _taskItemRepository;
    private readonly IRepository<BuildTaskCollaborator, Guid> _collaboratorRepository;
    private readonly IRepository<BuildTaskDelivery, Guid> _deliveryRepository;
    private readonly IRepository<LibraryRoleAssignment, Guid> _roleRepository;
    private readonly IRepository<ResourceLibrary, Guid> _libraryRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<KnowledgeHub.Courses.Course, Guid> _courseRepository;
    private readonly IRepository<KnowledgeHub.Practicums.PracticumProject, Guid> _practicumRepository;
    private readonly IResourcePublicationService _publicationService;
    private readonly IFeatureChecker _featureChecker;
    private readonly IDataFilter _dataFilter;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public BuildTaskAppService(
        IRepository<ResourceLibraryTemplate, Guid> templateRepository,
        IRepository<BuildTaskTemplate, Guid> taskTemplateRepository,
        IRepository<BuildTaskTemplateItem, Guid> taskTemplateItemRepository,
        IRepository<BuildTask, Guid> taskRepository,
        IRepository<BuildTaskItem, Guid> taskItemRepository,
        IRepository<BuildTaskCollaborator, Guid> collaboratorRepository,
        IRepository<BuildTaskDelivery, Guid> deliveryRepository,
        IRepository<LibraryRoleAssignment, Guid> roleRepository,
        IRepository<ResourceLibrary, Guid> libraryRepository,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<KnowledgeHub.Courses.Course, Guid> courseRepository,
        IRepository<KnowledgeHub.Practicums.PracticumProject, Guid> practicumRepository,
        IResourcePublicationService publicationService,
        IFeatureChecker featureChecker,
        IDataFilter dataFilter,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _templateRepository = templateRepository;
        _taskTemplateRepository = taskTemplateRepository;
        _taskTemplateItemRepository = taskTemplateItemRepository;
        _taskRepository = taskRepository;
        _taskItemRepository = taskItemRepository;
        _collaboratorRepository = collaboratorRepository;
        _deliveryRepository = deliveryRepository;
        _roleRepository = roleRepository;
        _libraryRepository = libraryRepository;
        _resourceRepository = resourceRepository;
        _courseRepository = courseRepository;
        _practicumRepository = practicumRepository;
        _publicationService = publicationService;
        _featureChecker = featureChecker;
        _dataFilter = dataFilter;
        _asyncExecuter = asyncExecuter;
    }

    // ───────────────────────── 库模板 ─────────────────────────

    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public async Task<List<LibraryTemplateDto>> GetTemplatesAsync()
    {
        await EnsureFeatureAsync();
        var query = await _templateRepository.GetQueryableAsync();
        var list = await _asyncExecuter.ToListAsync(
            query.Where(x => x.Status == LibraryTemplateStatus.Published)
                 .OrderBy(x => x.Code).ThenByDescending(x => x.Version));

        return list.Select(MapTemplate).ToList();
    }

    [Authorize(KnowledgeHubPermissions.ResourceLibraries.ManageTemplate)]
    public async Task<LibraryTemplateDto> CreateTemplateAsync(CreateLibraryTemplateDto input)
    {
        await EnsureFeatureAsync();

        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name))
        {
            throw new UserFriendlyException("模板编码与名称不能为空。");
        }

        var query = await _templateRepository.GetQueryableAsync();
        var existingVersions = await _asyncExecuter.ToListAsync(
            query.Where(x => x.Code == input.Code.Trim()).Select(x => x.Version));
        var nextVersion = existingVersions.Count == 0 ? 1 : existingVersions.Max() + 1;

        var template = new ResourceLibraryTemplate
        {
            Code = input.Code.Trim(),
            Name = input.Name.Trim(),
            Version = nextVersion,
            Description = input.Description,
            ConfigJson = string.IsNullOrWhiteSpace(input.ConfigJson) ? "{}" : input.ConfigJson
        };

        await _templateRepository.InsertAsync(template, autoSave: true);
        return MapTemplate(template);
    }

    [Authorize(KnowledgeHubPermissions.ResourceLibraries.ManageTemplate)]
    public async Task<LibraryTemplateDto> PublishTemplateAsync(Guid id)
    {
        await EnsureFeatureAsync();
        var template = await _templateRepository.GetAsync(id);
        template.Publish();
        await _templateRepository.UpdateAsync(template, autoSave: true);
        return MapTemplate(template);
    }

    // ───────────────────────── 任务模板 ─────────────────────────

    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public async Task<List<BuildTaskTemplateDto>> GetTaskTemplatesAsync(Guid? libraryId)
    {
        await EnsureFeatureAsync();

        // 前端未选资源库时会传空 Guid（路由参数不能为空）→ 视为「不限库」，
        // 返回平台级模板 + 所有库级模板，避免模板页空白。
        if (libraryId == Guid.Empty)
        {
            libraryId = null;
        }
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _taskTemplateRepository.WithDetailsAsync(x => x.Items);
            query = query.Where(x => x.IsActive
                                     && (libraryId == null || x.LibraryId == null || x.LibraryId == libraryId.Value));

            var list = await _asyncExecuter.ToListAsync(query.OrderBy(x => x.Name));
            return list.Select(MapTaskTemplate).ToList();
        }
    }

    [Authorize(KnowledgeHubPermissions.ResourceLibraries.ManageTemplate)]
    public async Task<BuildTaskTemplateDto> CreateTaskTemplateAsync(CreateBuildTaskTemplateDto input)
    {
        await EnsureFeatureAsync();

        if (input.Items == null || input.Items.Count == 0)
        {
            throw new UserFriendlyException("任务模板至少要有一个交付项。");
        }

        var template = new BuildTaskTemplate
        {
            LibraryId = input.LibraryId,
            Code = string.IsNullOrWhiteSpace(input.Code) ? Guid.NewGuid().ToString("N")[..8] : input.Code.Trim(),
            Name = input.Name.Trim(),
            CourseId = input.CourseId,
            IsActive = true,
            TenantId = CurrentTenant.Id
        };

        foreach (var item in input.Items)
        {
            template.Items.Add(new BuildTaskTemplateItem(GuidGenerator.Create())
            {
                TemplateId = template.Id,
                ItemName = item.ItemName.Trim(),
                ContentType = item.ContentType,
                RequiredCount = item.RequiredCount <= 0 ? 1 : item.RequiredCount,
                Acceptance = item.Acceptance,
                SortOrder = item.SortOrder
            });
        }

        await _taskTemplateRepository.InsertAsync(template, autoSave: true);
        return MapTaskTemplate(template);
    }

    // ───────────────────────── 共建任务 ─────────────────────────

    [Authorize(KnowledgeHubPermissions.ResourceLibraries.ManageTask)]
    public async Task<BuildTaskDto> CreateTaskAsync(CreateBuildTaskDto input)
    {
        await EnsureFeatureAsync();

        var userId = CurrentUser.Id;
        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("请填写任务标题。");
        }

        var library = await GetLibraryAsync(input.LibraryId);
        await EnsureLibraryManagerAsync(library, tenantId, userId);

        var task = new BuildTask
        {
            LibraryId = library.Id,
            CourseId = input.CourseId,
            TemplateId = input.TemplateId,
            Title = input.Title.Trim(),
            OwnerSchoolId = input.OwnerSchoolId ?? tenantId,
            OwnerUserId = input.OwnerUserId,
            ReviewerUserId = input.ReviewerUserId,
            DueAt = input.DueAt,
            Requirements = input.Requirements,
            Status = BuildTaskStatus.Draft,
            TenantId = tenantId
        };

        // 交付项：优先用模板实例化，其次用显式入参
        var templateItems = new List<BuildTaskTemplateItem>();
        if (input.TemplateId.HasValue)
        {
            var templateQuery = await _taskTemplateItemRepository.GetQueryableAsync();
            templateItems = await _asyncExecuter.ToListAsync(
                templateQuery.Where(x => x.TemplateId == input.TemplateId.Value).OrderBy(x => x.SortOrder));
        }

        if (templateItems.Count > 0)
        {
            foreach (var item in templateItems)
            {
                task.Items.Add(new BuildTaskItem(GuidGenerator.Create())
                {
                    TaskId = task.Id,
                    ItemName = item.ItemName,
                    ContentType = item.ContentType,
                    RequiredCount = item.RequiredCount,
                    Acceptance = item.Acceptance,
                    SortOrder = item.SortOrder
                });
            }
        }
        else
        {
            if (input.Items.Count == 0)
            {
                throw new UserFriendlyException("请选择任务模板，或至少填写一个交付项。");
            }

            foreach (var item in input.Items)
            {
                task.Items.Add(new BuildTaskItem(GuidGenerator.Create())
                {
                    TaskId = task.Id,
                    ItemName = item.ItemName.Trim(),
                    ContentType = item.ContentType,
                    RequiredCount = item.RequiredCount <= 0 ? 1 : item.RequiredCount,
                    Acceptance = item.Acceptance,
                    SortOrder = item.SortOrder
                });
            }
        }

        // 主责校作为 Owner 协作方，协作校按入参添加
        task.Collaborators.Add(new BuildTaskCollaborator(GuidGenerator.Create())
        {
            TaskId = task.Id,
            SchoolId = task.OwnerSchoolId,
            Capability = BuildTaskCapability.Owner,
            TenantId = tenantId
        });

        foreach (var schoolId in input.CollaboratorSchoolIds.Where(x => x != Guid.Empty && x != task.OwnerSchoolId).Distinct())
        {
            task.Collaborators.Add(new BuildTaskCollaborator(GuidGenerator.Create())
            {
                TaskId = task.Id,
                SchoolId = schoolId,
                Capability = BuildTaskCapability.Contribute,
                TenantId = tenantId
            });
        }

        await _taskRepository.InsertAsync(task, autoSave: true);
        return await ToTaskDtoAsync(task, tenantId, userId);
    }

    [Authorize(KnowledgeHubPermissions.ResourceLibraries.ManageTask)]
    public async Task<BuildTaskDto> PublishTaskAsync(Guid id)
    {
        var task = await GetTaskWithDetailsAsync(id);
        task.Status = BuildTaskStatus.Published;
        await _taskRepository.UpdateAsync(task, autoSave: true);
        return await ToTaskDtoAsync(task, CurrentTenant.Id, CurrentUser.Id);
    }

    [Authorize(KnowledgeHubPermissions.ResourceLibraries.ManageTask)]
    public async Task<BuildTaskDto> CloseTaskAsync(Guid id)
    {
        var task = await GetTaskWithDetailsAsync(id);
        task.Status = BuildTaskStatus.Closed;
        await _taskRepository.UpdateAsync(task, autoSave: true);
        return await ToTaskDtoAsync(task, CurrentTenant.Id, CurrentUser.Id);
    }

    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public async Task<PagedResultDto<BuildTaskDto>> GetTasksAsync(BuildTaskListRequestDto input)
    {
        await EnsureFeatureAsync();
        var tenantId = CurrentTenant.Id;
        var userId = CurrentUser.Id;

        // 共建任务归属「库主责学校」，协作校教师在自身租户上下文里默认看不到；
        // 这里关闭租户过滤器，再用「我的学校是否主责/协作」收敛范围（E2E 发现的真实缺陷）。
        List<BuildTask> tasks;
        int total;

        // 关键：整个查询（含协作关系匹配）都必须在关闭租户过滤器的作用域内。
        // 否则协作记录（归属主责学校租户）在协作校上下文里查不到，
        // 「我的共建任务」恒为空（E2E 发现的真实缺陷）。
        using var _crossTenant = _dataFilter.Disable<IMultiTenant>();

        // 关键：必须同时加载 Collaborators —— ToTaskDtoAsync 用它判断
        // 「当前所在学校是主责校 / 协作校」，缺了它 CanSubmit 恒为 false，
        // 前端「我的共建任务」就永远不出现「提交」按钮（V2 前端验收 P0-2）。
        var query = await _taskRepository.WithDetailsAsync(x => x.Items, x => x.Collaborators);

        if (input.LibraryId.HasValue)
        {
            query = query.Where(x => x.LibraryId == input.LibraryId.Value);
        }

        if (input.Status.HasValue)
        {
            query = query.Where(x => x.Status == input.Status.Value);
        }

        if (input.OnlyMine && userId.HasValue)
        {
            var mySchools = GetMySchoolIds();
            var myParticipatedTaskIds = await GetMyParticipatedTaskIdsAsync(userId.Value);

            // 我只参与 / 我所在学校是主责或协作的任务
            query = query.Where(x => myParticipatedTaskIds.Contains(x.Id)
                                     || mySchools.Contains(x.OwnerSchoolId)
                                     || x.OwnerUserId == userId.Value);
        }
        else
        {
            // 非「我的任务」：限本租户主责，或本租户作为协作校参与的任务
            var mySchools = GetMySchoolIds();
            var participatedIds = userId.HasValue
                ? await GetMyParticipatedTaskIdsAsync(userId.Value)
                : new List<Guid>();
            query = query.Where(x => mySchools.Contains(x.OwnerSchoolId)
                                     || participatedIds.Contains(x.Id));
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter.Trim();
            query = query.Where(x => x.Title.Contains(f));
        }

        total = await _asyncExecuter.CountAsync(query);

        query = query.OrderByDescending(x => x.CreationTime)
                     .Skip(input.SkipCount)
                     .Take(input.MaxResultCount);

        tasks = await _asyncExecuter.ToListAsync(query);
        var dtos = new List<BuildTaskDto>(tasks.Count);
        foreach (var task in tasks)
        {
            dtos.Add(await ToTaskDtoAsync(task, tenantId, userId));
        }

        return new PagedResultDto<BuildTaskDto>(total, dtos);
    }

    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public async Task<BuildTaskDto> GetTaskAsync(Guid id)
    {
        var task = await GetTaskWithDetailsAsync(id);
        return await ToTaskDtoAsync(task, CurrentTenant.Id, CurrentUser.Id);
    }

    [Authorize(KnowledgeHubPermissions.ResourceLibraries.Default)]
    public async Task<List<BuildTaskDto>> GetMyTasksAsync(BuildTaskStatus? status)
    {
        var result = await GetTasksAsync(new BuildTaskListRequestDto
        {
            OnlyMine = true,
            Status = status,
            MaxResultCount = 100
        });

        return result.Items.ToList();
    }

    [Authorize(KnowledgeHubPermissions.ResourceLibraries.SubmitTask)]
    public async Task<BuildTaskDeliveryDto> SubmitDeliveryAsync(SubmitBuildTaskDeliveryDto input)
    {
        await EnsureFeatureAsync();
        var userId = CurrentUser.Id ?? throw new UserFriendlyException("提交需要登录。");
        var tenantId = CurrentTenant.Id;

        var task = await GetTaskWithDetailsAsync(input.TaskId);
        if (task.Status is BuildTaskStatus.Draft or BuildTaskStatus.Closed or BuildTaskStatus.Accepted)
        {
            throw new UserFriendlyException("当前任务状态不接受新的交付物。");
        }

        // 共建任务可能用在「别校主建」的库上：功能开关以库归属学校为准，
        // 否则协作校没单独开通该开关时无法提交（E2E 发现的真实缺陷）。
        await EnsureFeatureForLibraryAsync(task.LibraryId);

        var item = task.Items.FirstOrDefault(x => x.Id == input.TaskItemId)
                   ?? throw new UserFriendlyException("交付项不存在。");

        if (input.ContentId == Guid.Empty)
        {
            throw new UserFriendlyException("请选择要交付的内容。");
        }

        // 协作校验：任务协作校 / 主责负责人 / 库级 Contributor
        var canSubmit = await CanSubmitAsync(task, tenantId, userId);
        if (!canSubmit)
        {
            throw new UserFriendlyException("你不在该任务的协作范围内，不能提交交付物。");
        }

        // 交付物进库仍需准入审核（任务验收 ≠ 资源发布）
        var publication = await _publicationService.BeginAdmissionAsync(
            task.LibraryId, input.ContentType, input.ContentId, null);

        var delivery = new BuildTaskDelivery(GuidGenerator.Create())
        {
            TaskId = task.Id,
            TaskItemId = item.Id,
            ContentType = input.ContentType,
            ContentId = input.ContentId,
            PublicationId = publication.Id,
            SubmittedBy = userId,
            SubmitterSchoolId = tenantId,
            Status = BuildTaskDeliveryStatus.Submitted,
            Comment = input.Comment,
            // 关键：交付记录归属「任务/库主责学校」，不是提交方学校。
            // 否则主责校在自身租户上下文里查不到这条记录，验收与进度计算全部失效（E2E 发现）。
            TenantId = task.TenantId ?? task.OwnerSchoolId
        };

        await _deliveryRepository.InsertAsync(delivery, autoSave: true);

        task.Status = BuildTaskStatus.InProgress;
        await _taskRepository.UpdateAsync(task, autoSave: true);

        return await ToDeliveryDtoAsync(delivery, item.ItemName);
    }

    [Authorize(KnowledgeHubPermissions.ResourceLibraries.ManageTask)]
    public async Task<BuildTaskDeliveryDto> ReviewDeliveryAsync(ReviewBuildTaskDeliveryDto input)
    {
        var delivery = await _deliveryRepository.GetAsync(input.DeliveryId);
        var task = await GetTaskWithDetailsAsync(delivery.TaskId);

        if (delivery.Status != BuildTaskDeliveryStatus.Submitted)
        {
            throw new UserFriendlyException("该交付物已处理过。");
        }

        var item = task.Items.FirstOrDefault(x => x.Id == delivery.TaskItemId)
                   ?? throw new UserFriendlyException("交付项不存在。");

        delivery.ReviewerUserId = CurrentUser.Id;
        delivery.ReviewedAt = DateTime.UtcNow;
        delivery.Comment = input.Comment;

        if (input.Accept)
        {
            delivery.Status = BuildTaskDeliveryStatus.Accepted;
            item.AcceptedCount += 1;
        }
        else
        {
            delivery.Status = BuildTaskDeliveryStatus.Returned;
        }

        await _deliveryRepository.UpdateAsync(delivery, autoSave: true);
        await _taskItemRepository.UpdateAsync(item, autoSave: true);

        await RefreshTaskStatusAsync(task);

        return await ToDeliveryDtoAsync(delivery, item.ItemName);
    }

    // ───────────────────────── 内部方法 ─────────────────────────

    /// <summary>当前用户是否院校管理员（与其它 AppService 一致的角色判定）。</summary>
    private bool IsTenantAdmin()
    {
        var roles = CurrentUser.Roles;
        return roles != null && (roles.Contains("SchoolAdmin") || roles.Contains("admin"));
    }

    private async Task EnsureFeatureAsync()
    {
        if (!await _featureChecker.IsEnabledAsync(KnowledgeHubFeatures.BuildTask))
        {
            throw new UserFriendlyException("共建任务功能未对本校开通（请联系平台管理员开启）。");
        }
    }

    /// <summary>按「库归属学校」校验功能开关（跨校协作场景用）。</summary>
    private async Task EnsureFeatureForLibraryAsync(Guid libraryId)
    {
        var library = await GetLibraryAsync(libraryId);
        using (CurrentTenant.Change(library.OwnerTenantId))
        {
            if (!await _featureChecker.IsEnabledAsync(KnowledgeHubFeatures.BuildTask))
            {
                throw new UserFriendlyException("共建任务功能未对本库开通（请联系平台管理员开启）。");
            }
        }
    }

    private async Task<ResourceLibrary> GetLibraryAsync(Guid libraryId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await _libraryRepository.FindAsync(libraryId)
                   ?? throw new UserFriendlyException("教学资源库不存在。");
        }
    }

    private async Task EnsureLibraryManagerAsync(ResourceLibrary library, Guid? tenantId, Guid? userId)
    {
        // 主建校管理员或库级 Manager 才能建任务
        if (tenantId.HasValue && library.OwnerTenantId == tenantId.Value && IsTenantAdmin())
        {
            return;
        }

        if (userId.HasValue)
        {
            var roleQuery = await _roleRepository.GetQueryableAsync();
            var isManager = await _asyncExecuter.AnyAsync(
                roleQuery.Where(x => x.LibraryId == library.Id
                                     && x.UserId == userId.Value
                                     && x.Role == LibraryRole.Manager));
            if (isManager)
            {
                return;
            }
        }

        throw new UserFriendlyException("只有主建校管理员或库管理员可以创建共建任务。");
    }

    private async Task<BuildTask> GetTaskWithDetailsAsync(Guid id)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _taskRepository.WithDetailsAsync(x => x.Items, x => x.Collaborators);
            return await _asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == id))
                   ?? throw new UserFriendlyException("共建任务不存在。");
        }
    }

    private async Task<bool> CanSubmitAsync(BuildTask task, Guid? tenantId, Guid? userId)
    {
        if (userId.HasValue && task.OwnerUserId == userId.Value)
        {
            return true;
        }

        if (tenantId.HasValue && task.Collaborators.Any(x => x.SchoolId == tenantId.Value))
        {
            return true;
        }

        if (userId.HasValue)
        {
            var roleQuery = await _roleRepository.GetQueryableAsync();
            return await _asyncExecuter.AnyAsync(
                roleQuery.Where(x => x.LibraryId == task.LibraryId
                                     && x.UserId == userId.Value
                                     && x.Role == LibraryRole.Contributor));
        }

        return false;
    }

    /// <summary>任务状态随交付进度推进（验收 ≠ 发布）。</summary>
    private async Task RefreshTaskStatusAsync(BuildTask task)
    {
        if (task.Status == BuildTaskStatus.Closed)
        {
            return;
        }

        var deliveryQuery = await _deliveryRepository.GetQueryableAsync();
        var deliveries = await _asyncExecuter.ToListAsync(
            deliveryQuery.Where(x => x.TaskId == task.Id));

        var allAccepted = task.Items.Count > 0 && task.Items.All(item =>
            deliveries.Count(d => d.TaskItemId == item.Id && d.Status == BuildTaskDeliveryStatus.Accepted)
            >= item.RequiredCount);

        BuildTaskStatus next;
        if (allAccepted)
        {
            next = BuildTaskStatus.Accepted;
        }
        else if (deliveries.Any(x => x.Status == BuildTaskDeliveryStatus.Returned))
        {
            next = BuildTaskStatus.Returned;
        }
        else if (deliveries.Any())
        {
            next = BuildTaskStatus.InProgress;
        }
        else
        {
            next = task.Status == BuildTaskStatus.Published ? BuildTaskStatus.Published : task.Status;
        }

        if (next != task.Status)
        {
            task.Status = next;
        }

        await _taskRepository.UpdateAsync(task, autoSave: true);
    }

    private List<Guid> GetMySchoolIds()
    {
        // 一个用户只有一个租户身份（多身份切换由 ABP 会话完成），这里返回当前租户
        return CurrentTenant.Id.HasValue
            ? new List<Guid> { CurrentTenant.Id.Value }
            : new List<Guid>();
    }

    /// <summary>
    /// 我参与的任务 Id：**用户级**（被点名）与**学校级**（我的学校是被指派的协作校）都算。
    ///
    /// <para>
    /// 此前只按 UserId 匹配，导致协作校教师（协作关系记在学校维度上，UserId 为空）
    /// 在「我的共建任务」里看不到任何任务 —— E2E 发现的真实缺陷。
    /// </para>
    /// </summary>
    private async Task<List<Guid>> GetMyParticipatedTaskIdsAsync(Guid userId)
    {
        var tenantId = CurrentTenant.Id;
        var collaboratorQuery = await _collaboratorRepository.GetQueryableAsync();

        var ids = await _asyncExecuter.ToListAsync(
            collaboratorQuery.Where(x => x.UserId == userId
                                         || (tenantId.HasValue && x.SchoolId == tenantId.Value))
                             .Select(x => x.TaskId));

        return ids.Distinct().ToList();
    }

    // ───────────────────────── 映射 ─────────────────────────

    private static LibraryTemplateDto MapTemplate(ResourceLibraryTemplate template)
    {
        return new LibraryTemplateDto
        {
            Id = template.Id,
            Code = template.Code,
            Name = template.Name,
            Version = template.Version,
            Description = template.Description,
            ConfigJson = template.ConfigJson,
            Status = template.Status,
            StatusName = template.Status switch
            {
                LibraryTemplateStatus.Draft => "草稿",
                LibraryTemplateStatus.Published => "已发布",
                LibraryTemplateStatus.Disabled => "已停用",
                _ => template.Status.ToString()
            }
        };
    }

    private static BuildTaskTemplateDto MapTaskTemplate(BuildTaskTemplate template)
    {
        return new BuildTaskTemplateDto
        {
            Id = template.Id,
            LibraryId = template.LibraryId,
            Code = template.Code,
            Name = template.Name,
            CourseId = template.CourseId,
            IsActive = template.IsActive,
            Items = template.Items.OrderBy(x => x.SortOrder).Select(x => new BuildTaskTemplateItemDto
            {
                Id = x.Id,
                ItemName = x.ItemName,
                ContentType = x.ContentType,
                ContentTypeName = ContentTypeName(x.ContentType),
                RequiredCount = x.RequiredCount,
                Acceptance = x.Acceptance,
                SortOrder = x.SortOrder
            }).ToList()
        };
    }

    private async Task<BuildTaskDto> ToTaskDtoAsync(BuildTask task, Guid? tenantId, Guid? userId)
    {
        // 跨租户读交付记录（协作校提交的记录可能归属其它租户的历史数据）
        List<BuildTaskDelivery> deliveries;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var deliveriesQuery = await _deliveryRepository.GetQueryableAsync();
            deliveries = await _asyncExecuter.ToListAsync(
                deliveriesQuery.Where(x => x.TaskId == task.Id).OrderByDescending(x => x.CreationTime));
        }

        var items = task.Items.OrderBy(x => x.SortOrder).Select(item => new BuildTaskItemDto
        {
            Id = item.Id,
            ItemName = item.ItemName,
            ContentType = item.ContentType,
            ContentTypeName = ContentTypeName(item.ContentType),
            RequiredCount = item.RequiredCount,
            AcceptedCount = deliveries.Count(d => d.TaskItemId == item.Id && d.Status == BuildTaskDeliveryStatus.Accepted),
            SubmittedCount = deliveries.Count(d => d.TaskItemId == item.Id && d.Status == BuildTaskDeliveryStatus.Submitted),
            Acceptance = item.Acceptance
        }).ToList();

        var requiredTotal = items.Sum(x => x.RequiredCount);
        var acceptedTotal = items.Sum(x => Math.Min(x.AcceptedCount, x.RequiredCount));
        var pending = items.Sum(x => x.SubmittedCount);

        string? libraryName = null;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            libraryName = (await _libraryRepository.FindAsync(task.LibraryId))?.Name;
        }

        /*
         * 交付记录的「内容」列必须给名称，不能只给 ContentId：
         * MapDelivery 是纯同步映射（拿不到资源/课程/实训名称），
         * 直接用它会让前端在内容列渲染一串 UUID（共建任务详情的实测问题）。
         * 这里先逐条回查名称（关闭租户过滤，协作校提交的记录可能归属其它租户）。
         */
        var deliveryDtos = new List<BuildTaskDeliveryDto>(deliveries.Count);
        foreach (var d in deliveries)
        {
            var itemName = items.FirstOrDefault(i => i.Id == d.TaskItemId)?.ItemName ?? string.Empty;
            deliveryDtos.Add(await ToDeliveryDtoAsync(d, itemName));
        }

        return new BuildTaskDto
        {
            Id = task.Id,
            LibraryId = task.LibraryId,
            LibraryName = libraryName,
            CourseId = task.CourseId,
            Title = task.Title,
            OwnerSchoolId = task.OwnerSchoolId,
            OwnerUserId = task.OwnerUserId,
            ReviewerUserId = task.ReviewerUserId,
            DueAt = task.DueAt,
            Requirements = task.Requirements,
            Status = task.Status,
            StatusName = TaskStatusName(task.Status),
            RequiredTotal = requiredTotal,
            AcceptedTotal = acceptedTotal,
            SubmittedPending = pending,
            ProgressPercent = requiredTotal == 0 ? 0 : (int)Math.Round(acceptedTotal * 100.0 / requiredTotal),
            IsOverdue = task.DueAt.HasValue && task.DueAt.Value < DateTime.UtcNow && task.Status != BuildTaskStatus.Accepted,
            Items = items,
            Deliveries = deliveryDtos,
            CanSubmit = task.Status is BuildTaskStatus.Published or BuildTaskStatus.InProgress or BuildTaskStatus.Returned
                        && userId.HasValue
                        && (task.OwnerUserId == userId.Value
                            || (tenantId.HasValue && task.Collaborators.Any(x => x.SchoolId == tenantId.Value))),
            CanReview = task.Status is not (BuildTaskStatus.Closed or BuildTaskStatus.Accepted) && IsTenantAdmin()
        };
    }

    private static BuildTaskDeliveryDto MapDelivery(BuildTaskDelivery d, string? itemName)
    {
        return new BuildTaskDeliveryDto
        {
            Id = d.Id,
            TaskId = d.TaskId,
            TaskItemId = d.TaskItemId,
            ItemName = itemName ?? string.Empty,
            ContentType = d.ContentType,
            ContentId = d.ContentId,
            PublicationId = d.PublicationId,
            SubmittedBy = d.SubmittedBy,
            Status = d.Status,
            StatusName = d.Status switch
            {
                BuildTaskDeliveryStatus.Submitted => "待验收",
                BuildTaskDeliveryStatus.Accepted => "已验收",
                BuildTaskDeliveryStatus.Returned => "已退回",
                _ => d.Status.ToString()
            },
            Comment = d.Comment,
            CreationTime = d.CreationTime
        };
    }

    private async Task<BuildTaskDeliveryDto> ToDeliveryDtoAsync(BuildTaskDelivery delivery, string itemName)
    {
        var dto = MapDelivery(delivery, itemName);

        using (_dataFilter.Disable<IMultiTenant>())
        {
            switch (delivery.ContentType)
            {
                case LibraryContentType.Resource:
                    dto.ContentName = (await _resourceRepository.FindAsync(delivery.ContentId))?.Name;
                    break;
                case LibraryContentType.Course:
                    dto.ContentName = (await _courseRepository.FindAsync(delivery.ContentId))?.Title;
                    break;
                case LibraryContentType.Practicum:
                    dto.ContentName = (await _practicumRepository.FindAsync(delivery.ContentId))?.Title;
                    break;
            }
        }

        return dto;
    }

    private static string ContentTypeName(LibraryContentType type)
    {
        return type switch
        {
            LibraryContentType.Resource => "文件资源",
            LibraryContentType.Course => "课程",
            LibraryContentType.Practicum => "实训",
            _ => type.ToString()
        };
    }

    private static string TaskStatusName(BuildTaskStatus status)
    {
        return status switch
        {
            BuildTaskStatus.Draft => "草稿",
            BuildTaskStatus.Published => "已发布",
            BuildTaskStatus.InProgress => "进行中",
            BuildTaskStatus.Submitted => "已提交待验收",
            BuildTaskStatus.Accepted => "已验收",
            BuildTaskStatus.Returned => "已退回",
            BuildTaskStatus.Closed => "已关闭",
            _ => status.ToString()
        };
    }
}
