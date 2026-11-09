using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
using KnowledgeHub.Studios.Dtos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Studios;

/// <summary>
/// 名师工作室：建改启停 / 成员 / 资料上传与领衔人审核发布。
///
/// <para><b>审核口径（对齐采购要求）</b>：</para>
/// <list type="bullet">
/// <item><description>成员上传资料 → 默认 <c>PendingReview</c>，只有领衔人（或管理员）能通过。</description></item>
/// <item><description>若上传人本身就是领衔人 → **跳过审核直接发布**（「同一人时跳过审核」）。</description></item>
/// </list>
/// </summary>
[Authorize(KnowledgeHubPermissions.Studios.Default)]
public class StudioAppService : ApplicationService, IStudioAppService
{
    private readonly IRepository<Studio, Guid> _studioRepository;
    private readonly IRepository<StudioMember, Guid> _memberRepository;
    private readonly IRepository<StudioMaterial, Guid> _materialRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IdentityUserManager _userManager;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IDataFilter _dataFilter;

    public StudioAppService(
        IRepository<Studio, Guid> studioRepository,
        IRepository<StudioMember, Guid> memberRepository,
        IRepository<StudioMaterial, Guid> materialRepository,
        IRepository<Resource, Guid> resourceRepository,
        IdentityUserManager userManager,
        IRepository<IdentityUser, Guid> userRepository,
        IDataFilter dataFilter)
    {
        _studioRepository = studioRepository;
        _memberRepository = memberRepository;
        _materialRepository = materialRepository;
        _resourceRepository = resourceRepository;
        _userManager = userManager;
        _userRepository = userRepository;
        _dataFilter = dataFilter;
    }

    private Guid Me => CurrentUser.Id ?? throw new UserFriendlyException("请先登录。");
    private string MyName => CurrentUser.UserName ?? "教师";

    public async Task<List<StudioListItemDto>> GetListAsync()
    {
        var me = Me;

        List<Studio> studios;
        List<StudioMember> myMemberships;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var sq = await _studioRepository.GetQueryableAsync();
            var mq = await _memberRepository.GetQueryableAsync();

            myMemberships = mq.Where(m => m.UserId == me).ToList();
            var joinedIds = myMemberships.Select(m => m.StudioId).ToList();

            // 本校全部工作室可见（工作室是校内公开的教研组织）；他校不可见
            studios = sq.Where(s => s.TenantId == CurrentTenant.Id || joinedIds.Contains(s.Id))
                .OrderBy(s => s.SortOrder)
                .ThenByDescending(s => s.CreationTime)
                .ToList();
        }

        var ids = studios.Select(s => s.Id).ToList();
        var allMembers = await _memberRepository.GetListAsync(m => ids.Contains(m.StudioId));
        var allMaterials = await _materialRepository.GetListAsync(x => ids.Contains(x.StudioId));

        return studios.Select(s => Map(s,
                allMembers.Where(m => m.StudioId == s.Id).ToList(),
                allMaterials.Where(m => m.StudioId == s.Id).ToList(),
                me))
            .ToList();
    }

    public async Task<StudioDetailDto> GetAsync(Guid id)
    {
        var me = Me;
        var studio = await GetStudioAsync(id);
        var members = await _memberRepository.GetListAsync(m => m.StudioId == id);
        var materials = await _materialRepository.GetListAsync(m => m.StudioId == id);

        var isLeader = studio.LeaderUserId == me || members.Any(m => m.UserId == me && m.Role == StudioMemberRole.Leader);
        var isAdmin = await IsAdminAsync();

        // 资料可见性：已发布对全室可见；未发布只有上传者与领衔人/管理员可见
        var visible = materials
            .Where(m => m.Status == StudioMaterialStatus.Published
                        || m.SubmittedByUserId == me || isLeader || isAdmin)
            .OrderByDescending(m => m.PublishedAt ?? m.CreationTime)
            .ToList();

        var resourceIds = visible.Select(m => m.ResourceId).Distinct().ToList();
        var resources = resourceIds.Count == 0
            ? new List<Resource>()
            : await _resourceRepository.GetListAsync(r => resourceIds.Contains(r.Id));
        var resourceById = resources.ToDictionary(r => r.Id);

        var dto = new StudioDetailDto();
        FillBase(dto, studio, members, materials, me);
        dto.Members = members.Select(ToMemberDto).ToList();
        dto.Materials = visible.Select(m => ToMaterialDto(m, resourceById.GetValueOrDefault(m.ResourceId), me, isLeader || isAdmin)).ToList();
        return dto;
    }

    [Authorize(KnowledgeHubPermissions.Studios.Manage)]
    public async Task<StudioDetailDto> CreateAsync(CreateStudioDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new UserFriendlyException("请填写工作室名称。");
        }

        var studio = new Studio(GuidGenerator.Create(), CurrentTenant.Id, input.Name);
        studio.SetInfo(input.Description, input.CoverUrl);

        if (input.LeaderUserId.HasValue)
        {
            var leader = await FindUserAsync(input.LeaderUserId.Value);
            studio.SetLeader(leader.Id, leader.UserName ?? leader.Name);
        }

        await _studioRepository.InsertAsync(studio, autoSave: true);

        // 领衔人自动成为 Leader 成员
        if (studio.LeaderUserId.HasValue)
        {
            await _memberRepository.InsertAsync(new StudioMember(
                GuidGenerator.Create(), CurrentTenant.Id, studio.Id,
                studio.LeaderUserId.Value, studio.LeaderName ?? "领衔人", StudioMemberRole.Leader), autoSave: true);
        }

        return await GetAsync(studio.Id);
    }

    [Authorize(KnowledgeHubPermissions.Studios.Manage)]
    public async Task<StudioDetailDto> UpdateAsync(Guid id, UpdateStudioDto input)
    {
        var studio = await GetStudioAsync(id);

        if (!string.IsNullOrWhiteSpace(input.Name))
        {
            studio.Rename(input.Name!);
        }

        studio.SetInfo(input.Description ?? studio.Description, input.CoverUrl ?? studio.CoverUrl);

        if (input.LeaderUserId.HasValue && input.LeaderUserId != studio.LeaderUserId)
        {
            var leader = await FindUserAsync(input.LeaderUserId.Value);
            studio.SetLeader(leader.Id, leader.UserName ?? leader.Name);

            using (_dataFilter.Disable<IMultiTenant>())
            {
                var existing = await _memberRepository.FirstOrDefaultAsync(
                    m => m.StudioId == id && m.UserId == leader.Id);
                if (existing == null)
                {
                    await _memberRepository.InsertAsync(new StudioMember(
                        GuidGenerator.Create(), studio.TenantId, id, leader.Id,
                        studio.LeaderName ?? "领衔人", StudioMemberRole.Leader), autoSave: true);
                }
                else
                {
                    existing.Role = StudioMemberRole.Leader;
                    await _memberRepository.UpdateAsync(existing, autoSave: true);
                }
            }
        }

        if (input.Status.HasValue)
        {
            studio.SetStatus(input.Status.Value == (int)StudioStatus.Disabled
                ? StudioStatus.Disabled
                : StudioStatus.Active);
        }

        await _studioRepository.UpdateAsync(studio, autoSave: true);
        return await GetAsync(id);
    }

    [Authorize(KnowledgeHubPermissions.Studios.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        var studio = await GetStudioAsync(id);
        using (_dataFilter.Disable<IMultiTenant>())
        {
            await _materialRepository.DeleteAsync(m => m.StudioId == id);
            await _memberRepository.DeleteAsync(m => m.StudioId == id);
        }

        await _studioRepository.DeleteAsync(studio);
    }

    public async Task<List<StudioMemberDto>> GetCandidateTeachersAsync(Guid id)
    {
        var studio = await GetStudioAsync(id);
        await EnsureLeaderOrAdminAsync(studio);

        var members = await _memberRepository.GetListAsync(m => m.StudioId == id);
        var memberIds = members.Select(m => m.UserId).ToHashSet();

        var teachers = await _userManager.GetUsersInRoleAsync("Teacher");
        return teachers
            .Where(u => u.TenantId == CurrentTenant.Id)
            .Select(u => new StudioMemberDto
            {
                UserId = u.Id,
                UserName = u.UserName ?? string.Empty,
                Role = memberIds.Contains(u.Id) ? 1 : -1,
                RoleName = memberIds.Contains(u.Id) ? "已加入" : "可加入",
            })
            .OrderByDescending(x => x.Role == 1)
            .ThenBy(x => x.UserName)
            .ToList();
    }

    public async Task<List<StudioMemberDto>> AddMemberAsync(Guid id, StudioMemberInputDto input)
    {
        var studio = await GetStudioAsync(id);
        await EnsureLeaderOrAdminAsync(studio);

        var user = await FindUserAsync(input.UserId);
        var role = input.Role == (int)StudioMemberRole.Leader ? StudioMemberRole.Leader : StudioMemberRole.Member;

        var existing = await _memberRepository.FirstOrDefaultAsync(m => m.StudioId == id && m.UserId == user.Id);
        if (existing == null)
        {
            await _memberRepository.InsertAsync(new StudioMember(
                GuidGenerator.Create(), studio.TenantId, id, user.Id,
                user.UserName ?? user.Name ?? "教师", role), autoSave: true);
        }

        if (role == StudioMemberRole.Leader)
        {
            studio.SetLeader(user.Id, user.UserName ?? user.Name);
            await _studioRepository.UpdateAsync(studio, autoSave: true);
        }

        var members = await _memberRepository.GetListAsync(m => m.StudioId == id);
        return members.Select(ToMemberDto).ToList();
    }

    public async Task RemoveMemberAsync(Guid id, Guid userId)
    {
        var studio = await GetStudioAsync(id);
        await EnsureLeaderOrAdminAsync(studio);

        if (studio.LeaderUserId == userId)
        {
            throw new UserFriendlyException("不能移除工作室领衔人，请先更换领衔人。");
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            await _memberRepository.DeleteAsync(m => m.StudioId == id && m.UserId == userId);
        }
    }

    [Authorize(KnowledgeHubPermissions.Studios.Contribute)]
    public async Task<StudioMaterialDto> CreateMaterialAsync(Guid id, CreateStudioMaterialDto input)
    {
        var studio = await GetStudioAsync(id);
        if (studio.Status == StudioStatus.Disabled)
        {
            throw new UserFriendlyException("工作室已停用，无法上传资料。");
        }

        await EnsureMemberAsync(studio);

        var resource = await _resourceRepository.FindAsync(input.ResourceId)
                       ?? throw new UserFriendlyException("资源不存在，请先在资源中心上传文件。");

        var me = Me;
        var material = new StudioMaterial(GuidGenerator.Create(), studio.TenantId, id,
            resource.Id, string.IsNullOrWhiteSpace(input.Title) ? resource.Name : input.Title,
            input.Description, me, MyName);

        var isLeader = await IsLeaderAsync(studio);
        if (input.SubmitNow)
        {
            // 「同一人时跳过审核」：领衔人自己上传 → 直接发布
            if (isLeader)
            {
                material.Publish(me, MyName, "领衔人本人上传，自动发布");
            }
            else
            {
                material.Submit();
            }
        }

        await _materialRepository.InsertAsync(material, autoSave: true);
        return ToMaterialDto(material, resource, me, isLeader);
    }

    public async Task<StudioMaterialDto> SubmitMaterialAsync(Guid id, Guid materialId)
    {
        var studio = await GetStudioAsync(id);
        var material = await GetMaterialAsync(id, materialId);

        if (material.SubmittedByUserId != Me)
        {
            throw new UserFriendlyException("只能提交自己上传的资料。");
        }

        if (material.Status is StudioMaterialStatus.Published or StudioMaterialStatus.PendingReview)
        {
            throw new UserFriendlyException("该资料已发布或正在审核中。");
        }

        var isLeader = await IsLeaderAsync(studio);
        if (isLeader)
        {
            material.Publish(Me, MyName, "领衔人本人提交，自动发布");
        }
        else
        {
            material.Submit();
        }

        await _materialRepository.UpdateAsync(material, autoSave: true);
        var resource = await _resourceRepository.FindAsync(material.ResourceId);
        return ToMaterialDto(material, resource, Me, isLeader);
    }

    [Authorize(KnowledgeHubPermissions.Studios.Review)]
    public async Task<StudioMaterialDto> ReviewMaterialAsync(Guid id, Guid materialId, ReviewStudioMaterialDto input)
    {
        var studio = await GetStudioAsync(id);
        await EnsureLeaderOrAdminAsync(studio);

        var material = await GetMaterialAsync(id, materialId);
        if (material.Status != StudioMaterialStatus.PendingReview)
        {
            throw new UserFriendlyException("只有「待审核」的资料可以审核。");
        }

        if (input.Approved)
        {
            material.Publish(Me, MyName, input.Comment);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(input.Comment))
            {
                throw new UserFriendlyException("驳回必须填写原因。");
            }

            material.Reject(Me, MyName, input.Comment!);
        }

        await _materialRepository.UpdateAsync(material, autoSave: true);
        var resource = await _resourceRepository.FindAsync(material.ResourceId);
        return ToMaterialDto(material, resource, Me, true);
    }

    public async Task DeleteMaterialAsync(Guid id, Guid materialId)
    {
        var studio = await GetStudioAsync(id);
        var material = await GetMaterialAsync(id, materialId);

        var isLeader = await IsLeaderAsync(studio) || await IsAdminAsync();
        if (material.SubmittedByUserId != Me && !isLeader)
        {
            throw new UserFriendlyException("只能删除自己上传的资料。");
        }

        await _materialRepository.DeleteAsync(material);
    }

    // ── 内部 ──────────────────────────────────────────────────────────

    private async Task<Studio> GetStudioAsync(Guid id)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await _studioRepository.FindAsync(id)
                   ?? throw new UserFriendlyException("工作室不存在。");
        }
    }

    private async Task<StudioMaterial> GetMaterialAsync(Guid studioId, Guid materialId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var m = await _materialRepository.FindAsync(materialId);
            if (m == null || m.StudioId != studioId)
            {
                throw new UserFriendlyException("资料不存在。");
            }

            return m;
        }
    }

    private async Task<IdentityUser> FindUserAsync(Guid userId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await _userRepository.FindAsync(userId)
                   ?? throw new UserFriendlyException("用户不存在。");
        }
    }

    private async Task EnsureMemberAsync(Studio studio)
    {
        if (await IsLeaderAsync(studio) || await IsAdminAsync())
        {
            return;
        }

        var me = Me;
        var member = await _memberRepository.FirstOrDefaultAsync(m => m.StudioId == studio.Id && m.UserId == me);
        if (member == null)
        {
            throw new UserFriendlyException("只有工作室成员可以上传资料。");
        }
    }

    private async Task EnsureLeaderOrAdminAsync(Studio studio)
    {
        if (await IsLeaderAsync(studio) || await IsAdminAsync())
        {
            return;
        }

        throw new UserFriendlyException("只有工作室领衔人或管理员可以执行该操作。");
    }

    private async Task<bool> IsLeaderAsync(Studio studio)
    {
        var me = Me;
        if (studio.LeaderUserId == me)
        {
            return true;
        }

        var member = await _memberRepository.FirstOrDefaultAsync(m => m.StudioId == studio.Id && m.UserId == me);
        return member?.Role == StudioMemberRole.Leader;
    }

    private Task<bool> IsAdminAsync() =>
        AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Studios.Manage);

    private static StudioMemberDto ToMemberDto(StudioMember m) => new()
    {
        UserId = m.UserId,
        UserName = m.UserName,
        Role = (int)m.Role,
        RoleName = m.Role == StudioMemberRole.Leader ? "领衔人" : "成员",
        JoinedAt = m.JoinedAt,
    };

    private static StudioMaterialDto ToMaterialDto(StudioMaterial m, Resource? resource, Guid me, bool reviewer)
    {
        return new StudioMaterialDto
        {
            Id = m.Id,
            StudioId = m.StudioId,
            ResourceId = m.ResourceId,
            Title = m.Title,
            Description = m.Description,
            Status = (int)m.Status,
            StatusName = m.Status switch
            {
                StudioMaterialStatus.Draft => "草稿",
                StudioMaterialStatus.PendingReview => "待审核",
                StudioMaterialStatus.Published => "已发布",
                _ => "已驳回",
            },
            SubmittedByName = m.SubmittedByName,
            SubmittedAt = m.SubmittedAt,
            ReviewedByName = m.ReviewedByName,
            ReviewedAt = m.ReviewedAt,
            ReviewComment = m.ReviewComment,
            PublishedAt = m.PublishedAt,
            FileExtension = resource?.FileExtension,
            FileSize = resource?.FileSize,
            OriginalFileName = resource?.OriginalFileName,
            CanSubmit = m.SubmittedByUserId == me
                        && m.Status is StudioMaterialStatus.Draft or StudioMaterialStatus.Rejected,
            CanReview = reviewer && m.Status == StudioMaterialStatus.PendingReview,
            CanPreview = m.Status == StudioMaterialStatus.Published || reviewer || m.SubmittedByUserId == me,
        };
    }

    private static void FillBase(StudioListItemDto dto, Studio studio, List<StudioMember> members,
        List<StudioMaterial> materials, Guid me)
    {
        dto.Id = studio.Id;
        dto.Name = studio.Name;
        dto.Description = studio.Description;
        dto.CoverUrl = studio.CoverUrl;
        dto.LeaderUserId = studio.LeaderUserId;
        dto.LeaderName = studio.LeaderName;
        dto.Status = (int)studio.Status;
        dto.StatusName = studio.Status switch
        {
            StudioStatus.Draft => "草稿",
            StudioStatus.Active => "启用",
            _ => "已停用",
        };
        dto.MemberCount = members.Count;
        dto.PublishedCount = materials.Count(m => m.Status == StudioMaterialStatus.Published);
        dto.PendingCount = materials.Count(m => m.Status == StudioMaterialStatus.PendingReview);
        dto.IsLeader = studio.LeaderUserId == me
                       || members.Any(m => m.UserId == me && m.Role == StudioMemberRole.Leader);
        dto.IsMember = dto.IsLeader || members.Any(m => m.UserId == me);
        dto.CanReview = dto.IsLeader;
        dto.CreationTime = studio.CreationTime;
    }

    private static StudioListItemDto Map(Studio studio, List<StudioMember> members,
        List<StudioMaterial> materials, Guid me)
    {
        var dto = new StudioListItemDto();
        FillBase(dto, studio, members, materials, me);
        return dto;
    }
}
