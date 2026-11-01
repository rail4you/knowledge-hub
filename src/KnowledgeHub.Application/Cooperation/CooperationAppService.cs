using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Cooperation.Dtos;
using KnowledgeHub.Enterprises;
using KnowledgeHub.Permissions;
using KnowledgeHub.Review;
using KnowledgeHub.Review.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Cooperation;

/// <summary>
/// 产教合作：需求（企业发起）→ 响应（学校）→ 项目台账 + 里程碑。
///
/// <para><b>关键边界</b>（设计方案 13.1/13.2）：</para>
/// <list type="bullet">
/// <item><description>需求必须审核通过才进大厅（学校初审 → 联盟终审）。</description></item>
/// <item><description>合作意向<b>不自动产生资源库权限</b>；成果进资源库仍需走准入审核。</description></item>
/// <item><description>平台阶段记录不能代替正式合同 / 实习协议 / 知识产权许可。</description></item>
/// </list>
/// </summary>
[IgnoreAntiforgeryToken]
public class CooperationAppService : KnowledgeHubAppService, ICooperationAppService
{
    private readonly IRepository<CooperationDemand, Guid> _demandRepository;
    private readonly IRepository<CooperationResponse, Guid> _responseRepository;
    private readonly IRepository<CooperationProject, Guid> _projectRepository;
    private readonly IRepository<ProjectMilestone, Guid> _milestoneRepository;
    private readonly IRepository<EnterpriseProfile, Guid> _enterpriseRepository;
    private readonly IRepository<EnterpriseMember, Guid> _enterpriseMemberRepository;
    private readonly IReviewWorkflow _reviewWorkflow;
    private readonly ITenantRepository _tenantRepository;
    private readonly KnowledgeHub.Alliance.IAllianceMemberRepository _allianceMemberRepository;
    private readonly IDataFilter _dataFilter;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public CooperationAppService(
        IRepository<CooperationDemand, Guid> demandRepository,
        IRepository<CooperationResponse, Guid> responseRepository,
        IRepository<CooperationProject, Guid> projectRepository,
        IRepository<ProjectMilestone, Guid> milestoneRepository,
        IRepository<EnterpriseProfile, Guid> enterpriseRepository,
        IRepository<EnterpriseMember, Guid> enterpriseMemberRepository,
        IReviewWorkflow reviewWorkflow,
        ITenantRepository tenantRepository,
        KnowledgeHub.Alliance.IAllianceMemberRepository allianceMemberRepository,
        IDataFilter dataFilter,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _demandRepository = demandRepository;
        _responseRepository = responseRepository;
        _projectRepository = projectRepository;
        _milestoneRepository = milestoneRepository;
        _enterpriseRepository = enterpriseRepository;
        _enterpriseMemberRepository = enterpriseMemberRepository;
        _reviewWorkflow = reviewWorkflow;
        _tenantRepository = tenantRepository;
        _allianceMemberRepository = allianceMemberRepository;
        _dataFilter = dataFilter;
        _asyncExecuter = asyncExecuter;
    }

    // ───────────────────────── 需求 ─────────────────────────

    /// <summary>需求大厅（已发布的需求）。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.Default)]
    public async Task<PagedResultDto<CooperationDemandDto>> GetHallAsync(CooperationDemandListRequestDto input)
    {
        // 需求大厅天然跨校：发布的需求归属企业入驻校，其它学校也要能看到 → 关闭租户过滤器后按「可见范围」收敛。
        using var _crossTenant = _dataFilter.Disable<IMultiTenant>();

        var tenantId = CurrentTenant.Id;
        var query = await _demandRepository.GetQueryableAsync();
        query = query.Where(x => x.Status == CooperationDemandStatus.Published);

        // 可见范围：公开 → 所有学校；联盟内 → 同联盟学校；仅入驻校 → 仅本租户
        var allianceScopedTenantIds = await GetAllianceTenantIdsAsync(tenantId);

        query = query.Where(x =>
            x.Scope == CooperationDemandScope.Public
            || (x.Scope == CooperationDemandScope.HostSchoolOnly && tenantId.HasValue && x.TenantId == tenantId.Value)
            || (x.Scope == CooperationDemandScope.Alliance && (
                tenantId.HasValue && x.TenantId == tenantId.Value
                || allianceScopedTenantIds.Contains(x.TenantId ?? Guid.Empty))));

        if (input.Type.HasValue)
        {
            query = query.Where(x => x.Type == input.Type.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Region))
        {
            query = query.Where(x => x.Region != null && x.Region.Contains(input.Region));
        }

        if (!string.IsNullOrWhiteSpace(input.Major))
        {
            query = query.Where(x => x.Majors != null && x.Majors.Contains(input.Major));
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter.Trim();
            query = query.Where(x => x.Title.Contains(f) || (x.Background != null && x.Background.Contains(f)));
        }

        return await PageAsync(query, input);
    }

    /// <summary>我的需求（企业侧）；院校管理员默认看本校企业全部需求。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.Default)]
    public async Task<PagedResultDto<CooperationDemandDto>> GetMyDemandsAsync(CooperationDemandListRequestDto input)
    {
        var query = await _demandRepository.GetQueryableAsync();

        if (input.OnlyMine)
        {
            var enterpriseId = await GetMyEnterpriseIdAsync();
            query = enterpriseId.HasValue
                ? query.Where(x => x.EnterpriseProfileId == enterpriseId.Value)
                : query.Where(x => false);
        }

        if (input.Status.HasValue)
        {
            query = query.Where(x => x.Status == input.Status.Value);
        }

        if (input.Type.HasValue)
        {
            query = query.Where(x => x.Type == input.Type.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter.Trim();
            query = query.Where(x => x.Title.Contains(f));
        }

        return await PageAsync(query, input);
    }

    [Authorize(KnowledgeHubPermissions.Cooperation.Default)]
    public async Task<CooperationDemandDto> GetDemandAsync(Guid id)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var demand = await _demandRepository.GetAsync(id);
            return await ToDemandDtoAsync(demand);
        }
    }

    /// <summary>创建需求（企业用户 / 院校管理员代建）。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.ManageDemand)]
    public async Task<CooperationDemandDto> CreateDemandAsync(CreateUpdateCooperationDemandDto input)
    {
        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");

        var enterpriseId = await GetMyEnterpriseIdAsync();
        if (!enterpriseId.HasValue && !IsTenantAdmin())
        {
            throw new UserFriendlyException("当前账号未绑定企业，无法发布合作需求。");
        }

        if (!enterpriseId.HasValue)
        {
            // 院校管理员代建：取本校最近认证的企业
            var enterpriseQuery = await _enterpriseRepository.GetQueryableAsync();
            var latest = await _asyncExecuter.FirstOrDefaultAsync(
                enterpriseQuery.Where(x => x.CertificationStatus == EnterpriseCertificationStatus.Certified)
                               .OrderByDescending(x => x.CreationTime));

            if (latest == null)
            {
                throw new UserFriendlyException("本校暂无已认证企业，无法代建需求。");
            }

            enterpriseId = latest.Id;
        }

        var demand = new CooperationDemand
        {
            EnterpriseProfileId = enterpriseId.Value,
            TenantId = tenantId,
            Status = CooperationDemandStatus.Draft
        };

        ApplyDemand(demand, input);
        await _demandRepository.InsertAsync(demand, autoSave: true);
        return await ToDemandDtoAsync(demand);
    }

    [Authorize(KnowledgeHubPermissions.Cooperation.ManageDemand)]
    public async Task<CooperationDemandDto> UpdateDemandAsync(Guid id, CreateUpdateCooperationDemandDto input)
    {
        CooperationDemand demand;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            demand = await _demandRepository.GetAsync(id);
        }

        if (!await CanManageDemandAsync(demand))
        {
            throw new UserFriendlyException("无权修改该需求。");
        }

        if (demand.Status is CooperationDemandStatus.PendingReview or CooperationDemandStatus.Published)
        {
            throw new UserFriendlyException("审核中 / 已发布的需求不能直接修改，请先关闭或等待审核结果。");
        }

        ApplyDemand(demand, input);
        await _demandRepository.UpdateAsync(demand, autoSave: true);
        return await ToDemandDtoAsync(demand);
    }

    /// <summary>提交审核（学校初审 → 联盟终审）。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.ManageDemand)]
    public async Task<CooperationDemandDto> SubmitDemandAsync(Guid id)
    {
        CooperationDemand demand;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            demand = await _demandRepository.GetAsync(id);
        }

        if (!await CanManageDemandAsync(demand))
        {
            throw new UserFriendlyException("无权提交该需求。");
        }

        if (string.IsNullOrWhiteSpace(demand.Title))
        {
            throw new UserFriendlyException("请先填写需求标题。");
        }

        demand.MarkPendingReview();
        await _demandRepository.UpdateAsync(demand, autoSave: true);

        await _reviewWorkflow.SubmitAsync(new SubmitReviewInput
        {
            TargetType = ReviewTargetType.CooperationDemand,
            TargetId = demand.Id,
            ActionType = ReviewActionType.Publish,
            ContentTenantId = demand.TenantId,
            TargetName = demand.Title,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                demand.Id,
                demand.Title,
                demand.Type,
                demand.Scope,
                demand.Region,
                demand.Majors
            })
        });

        return await ToDemandDtoAsync(demand);
    }

    [Authorize(KnowledgeHubPermissions.Cooperation.ManageDemand)]
    public async Task<CooperationDemandDto> CloseDemandAsync(Guid id)
    {
        CooperationDemand demand;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            demand = await _demandRepository.GetAsync(id);
        }

        if (!await CanManageDemandAsync(demand))
        {
            throw new UserFriendlyException("无权关闭该需求。");
        }

        demand.Close();
        await _demandRepository.UpdateAsync(demand, autoSave: true);
        return await ToDemandDtoAsync(demand);
    }

    // ───────────────────────── 响应 ─────────────────────────

    /// <summary>学校响应需求（提交意向）。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.Respond)]
    public async Task<CooperationResponseDto> RespondAsync(CreateCooperationResponseDto input)
    {
        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var userId = CurrentUser.Id ?? throw new UserFriendlyException("请先登录。");

        CooperationDemand demand;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            demand = await _demandRepository.GetAsync(input.DemandId);
        }

        if (demand.Status != CooperationDemandStatus.Published)
        {
            throw new UserFriendlyException("该需求尚未发布或已关闭，暂不能响应。");
        }

        await EnsureDemandVisibleAsync(demand, tenantId);

        var query = await _responseRepository.GetQueryableAsync();
        var exists = await _asyncExecuter.AnyAsync(
            query.Where(x => x.DemandId == demand.Id && x.SchoolTenantId == tenantId));
        if (exists)
        {
            throw new UserFriendlyException("本校已响应该需求。");
        }

        var response = new CooperationResponse(GuidGenerator.Create())
        {
            DemandId = demand.Id,
            SchoolTenantId = tenantId,
            ResponderUserId = userId,
            ResponderName = CurrentUser.Name ?? CurrentUser.UserName,
            Note = input.Note,
            Status = CooperationResponseStatus.Submitted,
            TenantId = tenantId
        };

        await _responseRepository.InsertAsync(response, autoSave: true);
        return await ToResponseDtoAsync(response);
    }

    /// <summary>某需求的响应列表（企业侧 / 学校侧均可见，按权限过滤）。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.Default)]
    public async Task<List<CooperationResponseDto>> GetResponsesAsync(Guid demandId)
    {
        CooperationDemand demand;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            demand = await _demandRepository.GetAsync(demandId);
        }

        var tenantId = CurrentTenant.Id;

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _responseRepository.GetQueryableAsync();
            var list = await _asyncExecuter.ToListAsync(
                query.Where(x => x.DemandId == demandId).OrderByDescending(x => x.CreationTime));

            // 学校侧只看本校响应；企业侧（需求归属校）看全部
            if (!IsTenantAdmin() && demand.TenantId == tenantId)
            {
                list = list.Where(x => x.SchoolTenantId == tenantId).ToList();
            }

            var result = new List<CooperationResponseDto>(list.Count);
            foreach (var item in list)
            {
                result.Add(await ToResponseDtoAsync(item));
            }

            return result;
        }
    }

    /// <summary>企业确认 / 婉拒学校响应。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.ManageDemand)]
    public async Task<CooperationResponseDto> DecideResponseAsync(Guid responseId, DecideCooperationResponseDto input)
    {
        CooperationResponse response;
        CooperationDemand demand;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            response = await _responseRepository.GetAsync(responseId);
            demand = await _demandRepository.GetAsync(response.DemandId);
        }

        if (!await CanManageDemandAsync(demand))
        {
            throw new UserFriendlyException("无权处理该响应。");
        }

        if (response.Status != CooperationResponseStatus.Submitted)
        {
            throw new UserFriendlyException("该响应已处理。");
        }

        response.Status = input.Accept
            ? CooperationResponseStatus.Accepted
            : CooperationResponseStatus.Declined;
        response.DecidedAt = DateTime.UtcNow;
        response.DecisionNote = input.Note;

        await _responseRepository.UpdateAsync(response, autoSave: true);
        return await ToResponseDtoAsync(response);
    }

    // ───────────────────────── 项目台账 ─────────────────────────

    /// <summary>由已接受的响应建立项目台账。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.ManageProject)]
    public async Task<CooperationProjectDto> CreateProjectAsync(CreateCooperationProjectDto input)
    {
        if (!input.ResponseId.HasValue)
        {
            throw new UserFriendlyException("请选择已接受的学校响应来建立项目。");
        }

        CooperationResponse response;
        CooperationDemand demand;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            response = await _responseRepository.GetAsync(input.ResponseId.Value);
            demand = await _demandRepository.GetAsync(response.DemandId);
        }

        if (response.Status != CooperationResponseStatus.Accepted)
        {
            throw new UserFriendlyException("只有已接受的响应才能建立项目。");
        }

        var project = new CooperationProject
        {
            DemandId = demand.Id,
            ResponseId = response.Id,
            EnterpriseProfileId = demand.EnterpriseProfileId,
            SchoolTenantId = response.SchoolTenantId,
            Stage = CooperationProjectStage.Confirming,
            OwnerUserId = CurrentUser.Id,
            AgreementRef = input.AgreementRef,
            StartAt = input.StartAt,
            EndAt = input.EndAt,
            // 项目归属合作学校租户（学校侧主导推进），企业侧通过 EnterpriseProfileId 关联
            TenantId = response.SchoolTenantId
        };

        project.SetName(string.IsNullOrWhiteSpace(input.Name) ? demand.Title : input.Name);
        await _projectRepository.InsertAsync(project, autoSave: true);

        return await ToProjectDtoAsync(project);
    }

    [Authorize(KnowledgeHubPermissions.Cooperation.Default)]
    public async Task<PagedResultDto<CooperationProjectDto>> GetProjectsAsync(PagedResultRequestDto input)
    {
        List<CooperationProject> projects;
        int total;

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _projectRepository.WithDetailsAsync(x => x.Milestones);
            var tenantId = CurrentTenant.Id;
            var enterpriseId = await GetMyEnterpriseIdAsync();

            if (enterpriseId.HasValue && !IsTenantAdmin())
            {
                query = query.Where(x => x.EnterpriseProfileId == enterpriseId.Value);
            }
            else if (tenantId.HasValue)
            {
                // 管理员：本校作为合作校的项目 ∪ 本校入驻企业参与的项目
                // （项目 TenantId 记的是合作学校，入驻企业侧必须能看见，否则企业工作台空白）
                var myEnterpriseIds = await _asyncExecuter.ToListAsync(
                    (await _enterpriseRepository.GetQueryableAsync())
                        .Where(x => x.TenantId == tenantId.Value)
                        .Select(x => x.Id));

                query = query.Where(x => x.SchoolTenantId == tenantId.Value
                                         || myEnterpriseIds.Contains(x.EnterpriseProfileId));
            }

            total = await _asyncExecuter.CountAsync(query);
            projects = await _asyncExecuter.ToListAsync(
                query.OrderByDescending(x => x.CreationTime).Skip(input.SkipCount).Take(input.MaxResultCount));
        }

        var dtos = new List<CooperationProjectDto>(projects.Count);
        foreach (var project in projects)
        {
            dtos.Add(await ToProjectDtoAsync(project));
        }

        return new PagedResultDto<CooperationProjectDto>(total, dtos);
    }

    [Authorize(KnowledgeHubPermissions.Cooperation.ManageProject)]
    public async Task<ProjectMilestoneDto> AddMilestoneAsync(CreateMilestoneDto input)
    {
        CooperationProject project;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            project = await _projectRepository.GetAsync(input.ProjectId);
        }

        if (!await CanManageProjectAsync(project))
        {
            throw new UserFriendlyException("无权维护该项目。");
        }

        var milestone = new ProjectMilestone(GuidGenerator.Create())
        {
            ProjectId = project.Id,
            Name = input.Name,
            DueAt = input.DueAt,
            SortOrder = input.SortOrder
        };

        await _milestoneRepository.InsertAsync(milestone, autoSave: true);
        return MapMilestone(milestone);
    }

    [Authorize(KnowledgeHubPermissions.Cooperation.ManageProject)]
    public async Task<ProjectMilestoneDto> CompleteMilestoneAsync(CompleteMilestoneDto input)
    {
        ProjectMilestone milestone;
        CooperationProject project;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            milestone = await _milestoneRepository.GetAsync(input.MilestoneId);
            project = await _projectRepository.GetAsync(milestone.ProjectId);
        }

        if (!await CanManageProjectAsync(project))
        {
            throw new UserFriendlyException("无权维护该项目。");
        }

        milestone.Complete(input.EvidenceRef, input.Note);
        await _milestoneRepository.UpdateAsync(milestone, autoSave: true);
        return MapMilestone(milestone);
    }

    /// <summary>推进项目阶段（验收意见必填时校验）。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.ManageProject)]
    public async Task<CooperationProjectDto> AdvanceStageAsync(AdvanceProjectStageDto input)
    {
        // 必须带 details 加载：否则返回的 DTO 里里程碑恒为 0（E2E 发现）。
        CooperationProject project;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _projectRepository.WithDetailsAsync(x => x.Milestones);
            project = await _asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == input.ProjectId))
                      ?? throw new UserFriendlyException("合作项目不存在。");
        }

        if (!await CanManageProjectAsync(project))
        {
            throw new UserFriendlyException("无权推进该项目。");
        }

        if (input.Stage == CooperationProjectStage.Archived
            && string.IsNullOrWhiteSpace(input.AcceptanceNote)
            && string.IsNullOrWhiteSpace(project.AcceptanceNote))
        {
            throw new UserFriendlyException("归档前必须填写验收意见。");
        }

        project.AdvanceTo(input.Stage);
        if (!string.IsNullOrWhiteSpace(input.AcceptanceNote))
        {
            project.AcceptanceNote = input.AcceptanceNote;
        }

        await _projectRepository.UpdateAsync(project, autoSave: true);
        return await ToProjectDtoAsync(project);
    }

    // ───────────────────────── 内部 ─────────────────────────

    /// <summary>当前租户所在联盟下的全部学校租户（用于「联盟内可见」判定）。</summary>
    private async Task<List<Guid>> GetAllianceTenantIdsAsync(Guid? tenantId)
    {
        if (!tenantId.HasValue)
        {
            return new List<Guid>();
        }

        var membership = await _allianceMemberRepository.GetByTenantIdAsync(tenantId.Value);
        if (membership == null)
        {
            return new List<Guid>();
        }

        var members = await _allianceMemberRepository.GetByAllianceIdAsync(membership.AllianceId);
        return members.Select(x => x.MemberTenantId).ToList();
    }

    /// <summary>需求对当前学校是否可见（响应前的守卫）。</summary>
    private async Task EnsureDemandVisibleAsync(CooperationDemand demand, Guid? tenantId)
    {
        if (demand.Scope == CooperationDemandScope.Public)
        {
            return;
        }

        if (demand.Scope == CooperationDemandScope.HostSchoolOnly)
        {
            if (tenantId.HasValue && demand.TenantId == tenantId.Value)
            {
                return;
            }

            throw new UserFriendlyException("该需求仅对入驻校可见。");
        }

        // Alliance
        var allianceTenantIds = await GetAllianceTenantIdsAsync(tenantId);
        if ((tenantId.HasValue && demand.TenantId == tenantId.Value)
            || allianceTenantIds.Contains(demand.TenantId ?? Guid.Empty))
        {
            return;
        }

        throw new UserFriendlyException("该需求仅对联盟内学校可见。");
    }

    private static void ApplyDemand(CooperationDemand demand, CreateUpdateCooperationDemandDto input)
    {
        demand.SetTitle(input.Title);
        demand.Type = input.Type;
        demand.Majors = input.Majors;
        demand.Industries = input.Industries;
        demand.Region = input.Region;
        demand.Background = input.Background;
        demand.EnterpriseInput = input.EnterpriseInput;
        demand.SchoolConditions = input.SchoolConditions;
        demand.Cycle = input.Cycle;
        demand.Scale = input.Scale;
        demand.BudgetNote = input.BudgetNote;
        demand.DeliveryAcceptance = input.DeliveryAcceptance;
        demand.IpTerms = input.IpTerms;
        demand.ContactName = input.ContactName;
        demand.ContactPhone = input.ContactPhone;
        demand.ContactEmail = input.ContactEmail;
        demand.Scope = input.Scope;
    }

    private bool IsTenantAdmin()
    {
        var roles = CurrentUser.Roles;
        return roles != null && (roles.Contains("SchoolAdmin") || roles.Contains("admin"));
    }

    private async Task<Guid?> GetMyEnterpriseIdAsync()
    {
        var userId = CurrentUser.Id;
        if (!userId.HasValue)
        {
            return null;
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _enterpriseMemberRepository.GetQueryableAsync();
            var list = await _asyncExecuter.ToListAsync(query.Where(x => x.UserId == userId.Value));

            return list
                .OrderByDescending(x => x.IsPrimary)
                .ThenByDescending(x => x.CreationTime)
                .FirstOrDefault()?.EnterpriseProfileId;
        }
    }

    private async Task<bool> CanManageDemandAsync(CooperationDemand demand)
    {
        if (IsTenantAdmin())
        {
            return true;
        }

        var enterpriseId = await GetMyEnterpriseIdAsync();
        return enterpriseId.HasValue && enterpriseId.Value == demand.EnterpriseProfileId;
    }

    /// <summary>项目维护权：院校管理员；或本人所属学校为合作校；或本人所属企业为项目企业。</summary>
    private async Task<bool> CanManageProjectAsync(CooperationProject project)
    {
        if (IsTenantAdmin())
        {
            return true;
        }

        var tenantId = CurrentTenant.Id;
        if (tenantId.HasValue && project.SchoolTenantId == tenantId.Value)
        {
            return true;
        }

        var enterpriseId = await GetMyEnterpriseIdAsync();
        return enterpriseId.HasValue && enterpriseId.Value == project.EnterpriseProfileId;
    }

    private async Task<PagedResultDto<CooperationDemandDto>> PageAsync(
        IQueryable<CooperationDemand> query,
        CooperationDemandListRequestDto input)
    {
        var total = await _asyncExecuter.CountAsync(query);
        var items = await _asyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.PublishedAt ?? x.CreationTime)
                 .Skip(input.SkipCount).Take(input.MaxResultCount));

        var dtos = new List<CooperationDemandDto>(items.Count);
        foreach (var item in items)
        {
            dtos.Add(await ToDemandDtoAsync(item));
        }

        return new PagedResultDto<CooperationDemandDto>(total, dtos);
    }

    private async Task<CooperationDemandDto> ToDemandDtoAsync(CooperationDemand demand)
    {
        string? enterpriseName = null;
        int responseCount = 0;

        using (_dataFilter.Disable<IMultiTenant>())
        {
            enterpriseName = (await _enterpriseRepository.FindAsync(demand.EnterpriseProfileId))?.Name;
            var responseQuery = await _responseRepository.GetQueryableAsync();
            responseCount = await _asyncExecuter.CountAsync(responseQuery.Where(x => x.DemandId == demand.Id));
        }

        return new CooperationDemandDto
        {
            Id = demand.Id,
            EnterpriseProfileId = demand.EnterpriseProfileId,
            EnterpriseName = enterpriseName,
            Type = demand.Type,
            TypeName = DemandTypeName(demand.Type),
            Title = demand.Title,
            Majors = demand.Majors,
            Industries = demand.Industries,
            Region = demand.Region,
            Background = demand.Background,
            EnterpriseInput = demand.EnterpriseInput,
            SchoolConditions = demand.SchoolConditions,
            Cycle = demand.Cycle,
            Scale = demand.Scale,
            BudgetNote = demand.BudgetNote,
            DeliveryAcceptance = demand.DeliveryAcceptance,
            IpTerms = demand.IpTerms,
            ContactName = demand.ContactName,
            ContactEmail = demand.ContactEmail,
            Scope = demand.Scope,
            Status = demand.Status,
            StatusName = DemandStatusName(demand.Status),
            PublishedAt = demand.PublishedAt,
            RejectReason = demand.RejectReason,
            ResponseCount = responseCount,
            CreationTime = demand.CreationTime,
            CanEdit = demand.Status is CooperationDemandStatus.Draft or CooperationDemandStatus.Rejected
        };
    }

    private async Task<CooperationResponseDto> ToResponseDtoAsync(CooperationResponse response)
    {
        string? schoolName = null;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            schoolName = (await _tenantRepository.FindAsync(response.SchoolTenantId))?.Name;
        }

        return new CooperationResponseDto
        {
            Id = response.Id,
            DemandId = response.DemandId,
            SchoolTenantId = response.SchoolTenantId,
            SchoolTenantName = schoolName,
            ResponderUserId = response.ResponderUserId,
            ResponderName = response.ResponderName,
            Note = response.Note,
            Status = response.Status,
            StatusName = response.Status switch
            {
                CooperationResponseStatus.Submitted => "待企业确认",
                CooperationResponseStatus.Accepted => "已接受",
                CooperationResponseStatus.Declined => "已婉拒",
                CooperationResponseStatus.Withdrawn => "已撤回",
                _ => response.Status.ToString()
            },
            DecidedAt = response.DecidedAt,
            DecisionNote = response.DecisionNote,
            CreationTime = response.CreationTime,
            CanDecide = response.Status == CooperationResponseStatus.Submitted && IsTenantAdmin()
        };
    }

    private async Task<CooperationProjectDto> ToProjectDtoAsync(CooperationProject project)
    {
        string? enterpriseName = null;
        string? schoolName = null;

        using (_dataFilter.Disable<IMultiTenant>())
        {
            enterpriseName = (await _enterpriseRepository.FindAsync(project.EnterpriseProfileId))?.Name;
            schoolName = (await _tenantRepository.FindAsync(project.SchoolTenantId))?.Name;
        }

        var milestones = project.Milestones?.OrderBy(x => x.SortOrder).Select(MapMilestone).ToList()
                         ?? new List<ProjectMilestoneDto>();

        return new CooperationProjectDto
        {
            Id = project.Id,
            DemandId = project.DemandId,
            ResponseId = project.ResponseId,
            EnterpriseProfileId = project.EnterpriseProfileId,
            EnterpriseName = enterpriseName,
            SchoolTenantId = project.SchoolTenantId,
            SchoolTenantName = schoolName,
            Name = project.Name,
            Stage = project.Stage,
            StageName = project.Stage switch
            {
                CooperationProjectStage.Confirming => "洽谈确认",
                CooperationProjectStage.Executing => "执行中",
                CooperationProjectStage.AcceptancePending => "待验收",
                CooperationProjectStage.Archived => "已归档",
                CooperationProjectStage.Terminated => "已终止",
                _ => project.Stage.ToString()
            },
            AgreementRef = project.AgreementRef,
            StartAt = project.StartAt,
            EndAt = project.EndAt,
            AcceptanceNote = project.AcceptanceNote,
            MilestoneCount = milestones.Count,
            CompletedMilestoneCount = milestones.Count(x => x.IsCompleted),
            Milestones = milestones
        };
    }

    private static ProjectMilestoneDto MapMilestone(ProjectMilestone m)
    {
        return new ProjectMilestoneDto
        {
            Id = m.Id,
            Name = m.Name,
            DueAt = m.DueAt,
            CompletedAt = m.CompletedAt,
            EvidenceRef = m.EvidenceRef,
            Note = m.Note,
            IsCompleted = m.IsCompleted
        };
    }

    private static string DemandTypeName(CooperationDemandType type)
    {
        return type switch
        {
            CooperationDemandType.TalentTraining => "人才培养与订单班",
            CooperationDemandType.Internship => "实习实训与就业",
            CooperationDemandType.Curriculum => "课程教材及资源共建",
            CooperationDemandType.BaseSharing => "基地设备共享",
            CooperationDemandType.TeacherPractice => "教师企业实践与培训",
            CooperationDemandType.TechTransfer => "技术研发与成果转化",
            CooperationDemandType.IndustrialCollege => "产业学院与联合体",
            _ => type.ToString()
        };
    }

    private static string DemandStatusName(CooperationDemandStatus status)
    {
        return status switch
        {
            CooperationDemandStatus.Draft => "草稿",
            CooperationDemandStatus.PendingReview => "待审核",
            CooperationDemandStatus.Published => "已发布",
            CooperationDemandStatus.Rejected => "已驳回",
            CooperationDemandStatus.Closed => "已关闭",
            CooperationDemandStatus.Archived => "已归档",
            _ => status.ToString()
        };
    }
}

public interface ICooperationAppService
{
    Task<PagedResultDto<CooperationDemandDto>> GetHallAsync(CooperationDemandListRequestDto input);
    Task<PagedResultDto<CooperationDemandDto>> GetMyDemandsAsync(CooperationDemandListRequestDto input);
    Task<CooperationDemandDto> GetDemandAsync(Guid id);
    Task<CooperationDemandDto> CreateDemandAsync(CreateUpdateCooperationDemandDto input);
    Task<CooperationDemandDto> UpdateDemandAsync(Guid id, CreateUpdateCooperationDemandDto input);
    Task<CooperationDemandDto> SubmitDemandAsync(Guid id);
    Task<CooperationDemandDto> CloseDemandAsync(Guid id);
    Task<CooperationResponseDto> RespondAsync(CreateCooperationResponseDto input);
    Task<List<CooperationResponseDto>> GetResponsesAsync(Guid demandId);
    Task<CooperationResponseDto> DecideResponseAsync(Guid responseId, DecideCooperationResponseDto input);
    Task<CooperationProjectDto> CreateProjectAsync(CreateCooperationProjectDto input);
    Task<PagedResultDto<CooperationProjectDto>> GetProjectsAsync(PagedResultRequestDto input);
    Task<ProjectMilestoneDto> AddMilestoneAsync(CreateMilestoneDto input);
    Task<ProjectMilestoneDto> CompleteMilestoneAsync(CompleteMilestoneDto input);
    Task<CooperationProjectDto> AdvanceStageAsync(AdvanceProjectStageDto input);
}
