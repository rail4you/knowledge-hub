using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.TeachingAgents.Dtos;
using KnowledgeHub.TeachingAgents.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.TeachingAgents;

[Authorize(KnowledgeHubPermissions.TeachingAgents.Default)]
public class TeachingAgentAppService : KnowledgeHubAppService, ITeachingAgentAppService
{
    private const string FixedModelId = "qwen-plus";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRepository<TeachingAgent, Guid> _teachingAgentRepository;
    private readonly IRepository<TeachingAgentVersion, Guid> _teachingAgentVersionRepository;
    private readonly IRepository<ClassroomAgentTask, Guid> _taskRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;

    public TeachingAgentAppService(
        IRepository<TeachingAgent, Guid> teachingAgentRepository,
        IRepository<TeachingAgentVersion, Guid> teachingAgentVersionRepository,
        IRepository<ClassroomAgentTask, Guid> taskRepository,
        IRepository<IdentityUser, Guid> userRepository)
    {
        _teachingAgentRepository = teachingAgentRepository;
        _teachingAgentVersionRepository = teachingAgentVersionRepository;
        _taskRepository = taskRepository;
        _userRepository = userRepository;
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Manage)]
    public Task<List<TeachingAgentPresetDto>> GetPresetsAsync()
    {
        return Task.FromResult(BuildPresets());
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Manage)]
    public async Task<TeachingAgentDto> CreateDraftAsync(CreateUpdateTeachingAgentDto input)
    {
        var ownerUserId = CurrentUser.GetId();
        var agent = new TeachingAgent(GuidGenerator.Create(), ownerUserId, input.Name)
        {
            TenantId = CurrentTenant.Id,
            Description = input.Description,
            Visibility = input.Visibility,
            Status = TeachingAgentStatus.Draft
        };

        await _teachingAgentRepository.InsertAsync(agent, autoSave: true);

        var version = new TeachingAgentVersion(GuidGenerator.Create(), agent.Id, 1)
        {
            TenantId = CurrentTenant.Id,
            SystemPrompt = input.SystemPrompt,
            WelcomeMessage = input.WelcomeMessage,
            ModelId = FixedModelId,
            Temperature = input.Temperature,
            SkillsJson = SerializeSkills(input.Skills),
            VersionNote = input.VersionNote,
            IsPublished = false
        };

        await _teachingAgentVersionRepository.InsertAsync(version, autoSave: true);
        return await MapAgentAsync(agent, new[] { version });
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Manage)]
    public async Task<TeachingAgentDto> UpdateDraftAsync(Guid id, CreateUpdateTeachingAgentDto input)
    {
        var agent = await _teachingAgentRepository.GetAsync(id);
        await EnsureCanManageAgentAsync(agent);

        agent.Name = input.Name;
        agent.Description = input.Description;
        agent.Visibility = input.Visibility;
        agent.Status = agent.PublishedVersionId.HasValue ? TeachingAgentStatus.Published : TeachingAgentStatus.Draft;
        await _teachingAgentRepository.UpdateAsync(agent, autoSave: true);

        var versions = await GetVersionsAsync(agent.Id);
        var latestVersion = versions.OrderByDescending(x => x.VersionNumber).FirstOrDefault();
        if (latestVersion == null || latestVersion.IsPublished)
        {
            latestVersion = new TeachingAgentVersion(
                GuidGenerator.Create(),
                agent.Id,
                (versions.Max(x => (int?)x.VersionNumber) ?? 0) + 1)
            {
                TenantId = CurrentTenant.Id
            };
            await _teachingAgentVersionRepository.InsertAsync(latestVersion, autoSave: true);
        }

        latestVersion.SystemPrompt = input.SystemPrompt;
        latestVersion.WelcomeMessage = input.WelcomeMessage;
        latestVersion.ModelId = FixedModelId;
        latestVersion.Temperature = input.Temperature;
        latestVersion.SkillsJson = SerializeSkills(input.Skills);
        latestVersion.VersionNote = input.VersionNote;
        latestVersion.IsPublished = false;

        await _teachingAgentVersionRepository.UpdateAsync(latestVersion, autoSave: true);
        versions = await GetVersionsAsync(agent.Id);
        return await MapAgentAsync(agent, versions);
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Manage)]
    public async Task<TeachingAgentDto> UnpublishAsync(Guid id)
    {
        var agent = await _teachingAgentRepository.GetAsync(id);
        await EnsureCanManageAgentAsync(agent);

        if (!agent.PublishedVersionId.HasValue)
        {
            throw new UserFriendlyException("该智能体尚未发布，无需下架。");
        }

        var versions = await GetVersionsAsync(agent.Id);
        var publishedVersion = versions.FirstOrDefault(x => x.Id == agent.PublishedVersionId);
        if (publishedVersion != null)
        {
            publishedVersion.IsPublished = false;
            await _teachingAgentVersionRepository.UpdateAsync(publishedVersion, autoSave: true);
        }

        agent.PublishedVersionId = null;
        agent.Status = TeachingAgentStatus.Draft;

        await _teachingAgentRepository.UpdateAsync(agent, autoSave: true);
        versions = await GetVersionsAsync(agent.Id);
        return await MapAgentAsync(agent, versions);
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        var agent = await _teachingAgentRepository.GetAsync(id);

        // 只能删除自己创建的智能体（owner == 当前用户）。
        // 跨租户 Public agent 也不会被其他租户误删，因为 owner 始终是创建者本人。
        if (agent.OwnerUserId != CurrentUser.Id)
        {
            throw new UserFriendlyException("只能删除自己创建的智能体。");
        }

        // 检查是否已被课堂任务引用。ClassroomAgentTask 在同租户创建，因此只需检查
        // 当前租户的 task 即可（跨租户引用 guozhou 公开 agent 的 qidi 任务属于 qidi 租户，
        // 其任务列表已能在解析不到 agent 时跳过脏数据；guozhou 本租户不允许直接引用自己
        // 的 Private / School agent（只能是当前租户 School/Public），所以不会有冲突）。
        var taskQuery = await _taskRepository.GetQueryableAsync();
        var versionQuery = await _teachingAgentVersionRepository.GetQueryableAsync();

        var versionIds = await versionQuery
            .Where(v => v.TeachingAgentId == agent.Id)
            .Select(v => v.Id)
            .ToListAsync();

        var referencedCount = await taskQuery
            .Where(t => t.TeachingAgentId == agent.Id
                        || (versionIds.Count > 0 && versionIds.Contains(t.TeachingAgentVersionId)))
            .LongCountAsync();

        if (referencedCount > 0)
        {
            throw new UserFriendlyException(
                $"该智能体已被 {referencedCount} 个课堂任务引用，无法删除。请先删除相关课堂任务后再试。");
        }

        // 先删除所有 version（软删除，让审计可追溯）
        foreach (var versionId in versionIds)
        {
            var version = await _teachingAgentVersionRepository.GetAsync(versionId);
            await _teachingAgentVersionRepository.DeleteAsync(version, autoSave: true);
        }

        await _teachingAgentRepository.DeleteAsync(agent, autoSave: true);
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Manage)]
    public async Task<TeachingAgentDto> PublishVersionAsync(Guid id, PublishTeachingAgentVersionDto input)
    {
        var agent = await _teachingAgentRepository.GetAsync(id);
        await EnsureCanManageAgentAsync(agent);

        var versions = await GetVersionsAsync(agent.Id);
        var latestVersion = versions.OrderByDescending(x => x.VersionNumber).FirstOrDefault();
        if (latestVersion == null)
        {
            throw new UserFriendlyException("请先创建智能体版本草稿。");
        }

        latestVersion.IsPublished = true;
        if (!string.IsNullOrWhiteSpace(input.VersionNote))
        {
            latestVersion.VersionNote = input.VersionNote;
        }

        agent.PublishedVersionId = latestVersion.Id;
        agent.Status = TeachingAgentStatus.Published;

        await _teachingAgentVersionRepository.UpdateAsync(latestVersion, autoSave: true);
        await _teachingAgentRepository.UpdateAsync(agent, autoSave: true);
        return await MapAgentAsync(agent, versions);
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Manage)]
    public async Task<PagedResultDto<TeachingAgentDto>> GetListAsync(PagedTeachingAgentRequestDto input)
    {
        var currentUserId = CurrentUser.GetId();
        var currentTenantId = CurrentTenant.Id;

        // 跨租户可见性：
        // 1) 自己创建的智能体（任意租户）；
        // 2) 同租户的全部智能体（同租户管理员和老师共享，无论可见性）；
        // 3) 跨租户标记为 Public 的智能体（全局公开）。
        //
        // TeachingAgent 实现 IMultiTenant，ABP 仓储默认按当前租户过滤，
        // 因此跨租户 Public 的智能体必须通过 DataFilter.Disable<IMultiTenant>() 后再手动加可见性条件。
        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _teachingAgentRepository.GetQueryableAsync();
            query = query.Where(x =>
                x.OwnerUserId == currentUserId
                || x.TenantId == currentTenantId
                || x.Visibility == TeachingAgentVisibility.Public);

            query = query
                .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x =>
                    x.Name.Contains(input.Filter!) || (x.Description != null && x.Description.Contains(input.Filter!)));

            var totalCount = await query.LongCountAsync();
            var agents = await query
                .OrderByDescending(x => x.LastModificationTime ?? x.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();

            // 跨租户 Public agent 的 version 与 owner 都可能在另一个租户，同样需要禁用多租户过滤
            var versions = await GetVersionsAsync(agents.Select(x => x.Id).ToList(), disableTenantFilter: true);
            var owners = await GetUserNamesAsync(agents.Select(x => x.OwnerUserId).Distinct().ToList(), disableTenantFilter: true);

            var items = agents.Select(agent =>
            {
                var agentVersions = versions.Where(x => x.TeachingAgentId == agent.Id).ToList();
                return MapAgent(agent, agentVersions, owners);
            }).ToList();

            return new PagedResultDto<TeachingAgentDto>(totalCount, items);
        }
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Manage)]
    public async Task<TeachingAgentDetailDto> GetDetailAsync(Guid id)
    {
        // 跨租户 Public agent 允许被其他租户查看版本与提示词，但不允许编辑；
        // 必须先禁用多租户过滤拿到 agent，再做可读性校验。
        TeachingAgent agent;
        using (DataFilter.Disable<IMultiTenant>())
        {
            agent = await _teachingAgentRepository.GetAsync(id);
        }
        await EnsureCanReadAgentAsync(agent);

        var versions = await GetVersionsAsync(agent.Id, disableTenantFilter: true);
        var owners = await GetUserNamesAsync(new List<Guid> { agent.OwnerUserId }, disableTenantFilter: true);
        var dto = MapAgent(agent, versions, owners);

        return new TeachingAgentDetailDto
        {
            Id = dto.Id,
            Name = dto.Name,
            Description = dto.Description,
            OwnerUserId = dto.OwnerUserId,
            OwnerUserName = dto.OwnerUserName,
            Visibility = dto.Visibility,
            Status = dto.Status,
            PublishedVersionId = dto.PublishedVersionId,
            DraftVersion = dto.DraftVersion,
            PublishedVersion = versions
                .Where(x => x.Id == agent.PublishedVersionId)
                .Select(MapVersion)
                .FirstOrDefault(),
            Versions = versions
                .OrderByDescending(x => x.VersionNumber)
                .Select(MapVersion)
                .ToList(),
            CreationTime = dto.CreationTime,
            CreatorId = dto.CreatorId,
            LastModificationTime = dto.LastModificationTime,
            LastModifierId = dto.LastModifierId
        };
    }

    [Authorize(KnowledgeHubPermissions.TeachingAgents.Manage)]
    public async Task<TeachingAgentDto> CloneFromPresetAsync(CloneTeachingAgentFromPresetDto input)
    {
        var preset = BuildPresets().FirstOrDefault(x => x.Code == input.PresetCode);
        if (preset == null)
        {
            throw new UserFriendlyException("未找到对应的智能体模板。");
        }

        return await CreateDraftAsync(new CreateUpdateTeachingAgentDto
        {
            Name = input.Name,
            Description = preset.Description,
            Visibility = input.Visibility,
            SystemPrompt = preset.SystemPrompt,
            WelcomeMessage = preset.WelcomeMessage,
            Skills = preset.Skills
        });
    }

    private async Task EnsureCanManageAgentAsync(TeachingAgent agent)
    {
        // 编辑权限：
        // 1) 创建者本人（owner）始终可管理自己的智能体；
        // 2) 同租户 Review 权限的管理员可管理同租户的智能体；
        // 3) 跨租户 Public 智能体只能被源租户的 owner / Review 管理员管理，
        //    避免 qidi 的管理员误改 guozhou 公开的智能体。
        if (agent.OwnerUserId == CurrentUser.Id)
        {
            return;
        }

        if (agent.TenantId == CurrentTenant.Id)
        {
            var canReview = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.TeachingAgents.Review);
            if (canReview)
            {
                return;
            }
        }

        throw new AbpAuthorizationException("You are not allowed to manage this teaching agent.");
    }

    private async Task EnsureCanReadAgentAsync(TeachingAgent agent)
    {
        if (await CanReadAgentAsync(agent))
        {
            return;
        }
        throw new AbpAuthorizationException("You are not allowed to view this teaching agent.");
    }

    private async Task<bool> CanReadAgentAsync(TeachingAgent agent)
    {
        if (agent.OwnerUserId == CurrentUser.Id)
        {
            return true;
        }

        // 同租户任意智能体都可见（同租户管理员和老师共享工作台）
        if (agent.TenantId == CurrentTenant.Id)
        {
            return true;
        }

        // 跨租户仅 Public 可见，School / Private 不可见
        return agent.Visibility == TeachingAgentVisibility.Public;
    }

    private async Task<List<TeachingAgentVersion>> GetVersionsAsync(Guid agentId, bool disableTenantFilter = false)
    {
        // 注意：IQueryable 是延迟执行，ToListAsync 必须发生在 using 块内，
        // 否则 DataFilter 在外层被恢复后再执行 SQL，过滤器不会生效。
        if (disableTenantFilter)
        {
            using (DataFilter.Disable<IMultiTenant>())
            {
                var versionQuery = await _teachingAgentVersionRepository.GetQueryableAsync();
                return await versionQuery.Where(x => x.TeachingAgentId == agentId).ToListAsync();
            }
        }

        var query = await _teachingAgentVersionRepository.GetQueryableAsync();
        return await query.Where(x => x.TeachingAgentId == agentId).ToListAsync();
    }

    private async Task<List<TeachingAgentVersion>> GetVersionsAsync(List<Guid> agentIds, bool disableTenantFilter = false)
    {
        if (agentIds.Count == 0)
        {
            return new List<TeachingAgentVersion>();
        }

        if (disableTenantFilter)
        {
            using (DataFilter.Disable<IMultiTenant>())
            {
                var versionQuery = await _teachingAgentVersionRepository.GetQueryableAsync();
                return await versionQuery.Where(x => agentIds.Contains(x.TeachingAgentId)).ToListAsync();
            }
        }

        var query = await _teachingAgentVersionRepository.GetQueryableAsync();
        return await query.Where(x => agentIds.Contains(x.TeachingAgentId)).ToListAsync();
    }

    private async Task<Dictionary<Guid, string>> GetUserNamesAsync(List<Guid> userIds, bool disableTenantFilter = false)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        // 同租户调用默认走 ABP 多租户过滤；跨租户读取时（如跨租户 Public agent 的 owner 名字）显式禁用。
        if (disableTenantFilter)
        {
            using (DataFilter.Disable<IMultiTenant>())
            {
                var userQuery = await _userRepository.GetQueryableAsync();
                return await userQuery
                    .Where(x => userIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, x => !string.IsNullOrEmpty(x.Name) ? x.Name : x.UserName);
            }
        }

        var query = await _userRepository.GetQueryableAsync();
        return await query
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => !string.IsNullOrEmpty(x.Name) ? x.Name : x.UserName);
    }

    private async Task<TeachingAgentDto> MapAgentAsync(TeachingAgent agent, IEnumerable<TeachingAgentVersion> versions)
    {
        var owners = await GetUserNamesAsync(new List<Guid> { agent.OwnerUserId });
        return MapAgent(agent, versions, owners);
    }

    private TeachingAgentDto MapAgent(
        TeachingAgent agent,
        IEnumerable<TeachingAgentVersion> versions,
        Dictionary<Guid, string> owners)
    {
        var orderedVersions = versions.OrderByDescending(x => x.VersionNumber).ToList();
        var draftVersion = orderedVersions.FirstOrDefault(x => !x.IsPublished) ?? orderedVersions.FirstOrDefault();

        return new TeachingAgentDto
        {
            Id = agent.Id,
            Name = agent.Name,
            Description = agent.Description,
            OwnerUserId = agent.OwnerUserId,
            OwnerUserName = owners.GetValueOrDefault(agent.OwnerUserId, string.Empty),
            Visibility = agent.Visibility,
            Status = agent.Status,
            PublishedVersionId = agent.PublishedVersionId,
            DraftVersion = draftVersion == null ? null : MapVersion(draftVersion),
            CreationTime = agent.CreationTime,
            CreatorId = agent.CreatorId,
            LastModificationTime = agent.LastModificationTime,
            LastModifierId = agent.LastModifierId
        };
    }

    private static TeachingAgentVersionDto MapVersion(TeachingAgentVersion version)
    {
        return new TeachingAgentVersionDto
        {
            Id = version.Id,
            TeachingAgentId = version.TeachingAgentId,
            VersionNumber = version.VersionNumber,
            SystemPrompt = version.SystemPrompt,
            WelcomeMessage = version.WelcomeMessage,
            ModelId = FixedModelId,
            Temperature = version.Temperature,
            Skills = DeserializeSkills(version.SkillsJson),
            VersionNote = version.VersionNote,
            IsPublished = version.IsPublished,
            CreationTime = version.CreationTime,
            CreatorId = version.CreatorId,
            LastModificationTime = version.LastModificationTime,
            LastModifierId = version.LastModifierId
        };
    }

    private static string SerializeSkills(List<TeachingAgentSkillBindingDto> skills)
    {
        return JsonSerializer.Serialize(skills, JsonOptions);
    }

    private static List<TeachingAgentSkillBindingDto> DeserializeSkills(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<TeachingAgentSkillBindingDto>();
        }

        return JsonSerializer.Deserialize<List<TeachingAgentSkillBindingDto>>(value, JsonOptions) ?? new List<TeachingAgentSkillBindingDto>();
    }

    private static List<TeachingAgentPresetDto> BuildPresets()
    {
        return new List<TeachingAgentPresetDto>
        {
            new TeachingAgentPresetDto
            {
                Code = "course-explainer",
                Name = "课程讲解助手",
                Description = "围绕课程大纲、章节和知识点进行课前导学与课堂答疑。",
                SystemPrompt = "你是一个课程讲解助手。请严格围绕老师分配的课程上下文进行讲解，优先帮助学生理解课程结构、章节重点和知识资源。",
                WelcomeMessage = "你好，我会根据老师分配的课程任务，帮助你快速理解课程重点。",
                SuggestedTaskPrompt = "先概括课程重点，再根据学生问题展开解释。",
                Skills = new List<TeachingAgentSkillBindingDto>
                {
                    new() { Code = "course_detail", Name = "课程讲解", Description = "查看课程详情、章节与知识资源", Enabled = true },
                    new() { Code = "assignment_context", Name = "课堂任务助手", Description = "理解教师布置的任务要求", Enabled = true }
                }
            },
            new TeachingAgentPresetDto
            {
                Code = "resource-guide",
                Name = "资源导读助手",
                Description = "针对老师指定的文档、视频和资源进行导读、摘要和重点提示。",
                SystemPrompt = "你是一个资源导读助手。请优先帮助学生快速把握资源主题、核心观点和建议阅读路径，不要脱离老师分配的资源上下文。",
                WelcomeMessage = "你好，我会带你快速读懂老师分配的资源，并指出关键内容。",
                SuggestedTaskPrompt = "先总结资源，再指出建议重点阅读的部分。",
                Skills = new List<TeachingAgentSkillBindingDto>
                {
                    new() { Code = "resource_detail", Name = "资源导读", Description = "查看资源信息和相关上下文", Enabled = true },
                    new() { Code = "assignment_context", Name = "课堂任务助手", Description = "理解教师布置的任务要求", Enabled = true }
                }
            },
            new TeachingAgentPresetDto
            {
                Code = "exercise-coach",
                Name = "习题辅导助手",
                Description = "根据老师指定的习题集进行思路点拨、易错提醒和知识回顾。",
                SystemPrompt = "你是一个习题辅导助手。你可以引导学生分析题目、归纳考点、指出易错点，但不要直接泄露完整标准答案。",
                WelcomeMessage = "你好，我会陪你一起分析老师布置的习题任务。",
                SuggestedTaskPrompt = "按题型分组讲解考点，必要时给出提示而不是完整答案。",
                Skills = new List<TeachingAgentSkillBindingDto>
                {
                    new() { Code = "exercise_set", Name = "习题辅导", Description = "查看习题集和题目摘要", Enabled = true },
                    new() { Code = "course_detail", Name = "课程讲解", Description = "关联课程知识点", Enabled = true }
                }
            }
        };
    }
}
