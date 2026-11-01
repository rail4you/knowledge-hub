using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Enterprises.Dtos;
using KnowledgeHub.Permissions;
using KnowledgeHub.Review;
using KnowledgeHub.Review.Dtos;
using KnowledgeHub.Schools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;

namespace KnowledgeHub.Enterprises;

/// <summary>
/// 企业主体（V2.0 P5）。
///
/// <para>
/// 企业用户挂在某学校租户下（角色 <c>EnterpriseUser</c>），业务归属由
/// <see cref="EnterpriseProfile"/> 决定；认证走统一审核单（学校初审 → 联盟终审）。
/// </summary>
[IgnoreAntiforgeryToken]
public class EnterpriseAppService : KnowledgeHubAppService, IEnterpriseAppService
{
    private readonly IRepository<EnterpriseProfile, Guid> _profileRepository;
    private readonly IRepository<EnterpriseMember, Guid> _memberRepository;
    private readonly IReviewWorkflow _reviewWorkflow;
    private readonly IDataFilter _dataFilter;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IClock _clock;

    public EnterpriseAppService(
        IRepository<EnterpriseProfile, Guid> profileRepository,
        IRepository<EnterpriseMember, Guid> memberRepository,
        IReviewWorkflow reviewWorkflow,
        IDataFilter dataFilter,
        IAsyncQueryableExecuter asyncExecuter,
        IClock clock)
    {
        _profileRepository = profileRepository;
        _memberRepository = memberRepository;
        _reviewWorkflow = reviewWorkflow;
        _dataFilter = dataFilter;
        _asyncExecuter = asyncExecuter;
        _clock = clock;
    }

    /// <summary>我所属的企业（企业用户）；未绑定则返回 null。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.Default)]
    public async Task<EnterpriseProfileDto?> GetMineAsync()
    {
        var userId = CurrentUser.Id;
        if (!userId.HasValue)
        {
            return null;
        }

        // 同一用户可能属于多家企业（切换/历史），取「主联系人」优先、其次最近创建的那家。
        // 之前取 FirstOrDefault 会稳定命中最早的一家，导致新建企业后 mine 仍显示旧企业（E2E 发现）。
        var member = await FindPrimaryMembershipAsync(userId.Value);
        if (member == null)
        {
            return null;
        }

        var profile = await _profileRepository.FindAsync(member.EnterpriseProfileId);
        return profile == null ? null : await ToDtoAsync(profile);
    }

    /// <summary>按 Id 取企业（成员或院校管理员）。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.Default)]
    public async Task<EnterpriseProfileDto> GetAsync(Guid id)
    {
        var profile = await _profileRepository.GetAsync(id);

        if (!IsTenantAdmin())
        {
            var member = await FindMembershipAsync(CurrentUser.Id ?? Guid.Empty);
            if (member == null || member.EnterpriseProfileId != id)
            {
                throw new UserFriendlyException("无权查看该企业资料。");
            }
        }

        return await ToDtoAsync(profile);
    }

    /// <summary>本校企业列表（院校管理员：认证审核 / 绑定账号用）。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.Review)]
    public async Task<PagedResultDto<EnterpriseProfileDto>> GetListAsync(PagedResultRequestDto input)
    {
        var query = await _profileRepository.GetQueryableAsync();
        var total = await _asyncExecuter.CountAsync(query);
        var items = await _asyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CreationTime).Skip(input.SkipCount).Take(input.MaxResultCount));

        var dtos = new List<EnterpriseProfileDto>(items.Count);
        foreach (var item in items)
        {
            dtos.Add(await ToDtoAsync(item));
        }

        return new PagedResultDto<EnterpriseProfileDto>(total, dtos);
    }

    /// <summary>新建企业资料（院校管理员或企业用户）。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.Default)]
    public async Task<EnterpriseProfileDto> CreateAsync(CreateUpdateEnterpriseProfileDto input)
    {
        var tenantId = CurrentTenant.Id ?? throw new UserFriendlyException("当前不在租户上下文中。");
        var userId = CurrentUser.Id;

        var profile = new EnterpriseProfile
        {
            TenantId = tenantId,
            OwnerUserId = userId,
            CertificationStatus = EnterpriseCertificationStatus.Draft
        };

        ApplyInput(profile, input);
        await _profileRepository.InsertAsync(profile, autoSave: true);

        if (userId.HasValue)
        {
            // 已有主企业时不抢主，避免 mine 语义漂移
            var hasPrimary = await FindPrimaryMembershipAsync(userId.Value) != null;

            await _memberRepository.InsertAsync(new EnterpriseMember(GuidGenerator.Create())
            {
                EnterpriseProfileId = profile.Id,
                UserId = userId.Value,
                UserName = CurrentUser.UserName,
                Role = EnterpriseMemberRole.Admin,
                IsPrimary = !hasPrimary,
                TenantId = tenantId
            }, autoSave: true);
        }

        return await ToDtoAsync(profile);
    }

    /// <summary>更新企业资料（院校管理员或该企业管理员）。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.Default)]
    public async Task<EnterpriseProfileDto> UpdateAsync(Guid id, CreateUpdateEnterpriseProfileDto input)
    {
        var profile = await _profileRepository.GetAsync(id);
        if (!await CanManageAsync(profile, CurrentUser.Id))
        {
            throw new UserFriendlyException("无权修改该企业资料。");
        }

        ApplyInput(profile, input);
        await _profileRepository.UpdateAsync(profile, autoSave: true);
        return await ToDtoAsync(profile);
    }

    private static void ApplyInput(EnterpriseProfile profile, CreateUpdateEnterpriseProfileDto input)
    {
        profile.SetName(input.Name);
        profile.UnifiedSocialCreditCode = input.UnifiedSocialCreditCode;
        profile.Industry = input.Industry;
        profile.ContactName = input.ContactName;
        profile.ContactPhone = input.ContactPhone;
        profile.ContactEmail = input.ContactEmail;
        profile.Summary = input.Summary;
        profile.LogoUrl = input.LogoUrl;
        profile.SetValidity(input.ValidFrom, input.ValidTo);
    }

    /// <summary>提交认证（学校初审 → 联盟终审）。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.Default)]
    public async Task<EnterpriseProfileDto> SubmitCertificationAsync(Guid id)
    {
        var profile = await _profileRepository.GetAsync(id);
        if (!await CanManageAsync(profile, CurrentUser.Id))
        {
            throw new UserFriendlyException("无权提交该企业认证。");
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new UserFriendlyException("请先填写企业名称。");
        }

        if (profile.CertificationStatus == EnterpriseCertificationStatus.Certified)
        {
            throw new UserFriendlyException("该企业已认证。");
        }

        profile.MarkPendingReview();
        await _profileRepository.UpdateAsync(profile, autoSave: true);

        await _reviewWorkflow.SubmitAsync(new SubmitReviewInput
        {
            TargetType = ReviewTargetType.EnterpriseProfile,
            TargetId = profile.Id,
            ActionType = ReviewActionType.Certify,
            ContentTenantId = profile.TenantId,
            TargetName = profile.Name,
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                profile.Id,
                profile.Name,
                profile.UnifiedSocialCreditCode,
                profile.Industry,
                profile.ContactName,
                profile.ValidFrom,
                profile.ValidTo
            })
        });

        return await ToDtoAsync(profile);
    }

    /// <summary>学校管理员把某个账号绑定为企业成员。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.Review)]
    public async Task<EnterpriseMemberDto> BindMemberAsync(BindEnterpriseMemberDto input)
    {
        var profile = await _profileRepository.GetAsync(input.EnterpriseProfileId);
        var tenantId = CurrentTenant.Id;

        if (tenantId.HasValue && profile.TenantId != tenantId)
        {
            throw new UserFriendlyException("只能绑定本校入驻的企业。");
        }

        var query = await _memberRepository.GetQueryableAsync();
        var existing = await _asyncExecuter.FirstOrDefaultAsync(
            query.Where(x => x.EnterpriseProfileId == input.EnterpriseProfileId && x.UserId == input.UserId));

        if (existing != null)
        {
            existing.Role = input.Role;
            existing.IsPrimary = input.IsPrimary;
            await _memberRepository.UpdateAsync(existing, autoSave: true);
            return MapMember(existing);
        }

        var member = new EnterpriseMember(GuidGenerator.Create())
        {
            EnterpriseProfileId = input.EnterpriseProfileId,
            UserId = input.UserId,
            Role = input.Role,
            IsPrimary = input.IsPrimary,
            TenantId = tenantId
        };

        await _memberRepository.InsertAsync(member, autoSave: true);
        return MapMember(member);
    }

    /// <summary>企业成员列表。</summary>
    [Authorize(KnowledgeHubPermissions.Cooperation.Default)]
    public async Task<List<EnterpriseMemberDto>> GetMembersAsync(Guid enterpriseProfileId)
    {
        var query = await _memberRepository.GetQueryableAsync();
        var list = await _asyncExecuter.ToListAsync(
            query.Where(x => x.EnterpriseProfileId == enterpriseProfileId));

        return list.Select(MapMember).ToList();
    }

    // ── 内部 ──

    private async Task<EnterpriseMember?> FindPrimaryMembershipAsync(Guid userId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _memberRepository.GetQueryableAsync();
            var list = await _asyncExecuter.ToListAsync(query.Where(x => x.UserId == userId));

            return list
                .OrderByDescending(x => x.IsPrimary)
                .ThenByDescending(x => x.CreationTime)
                .FirstOrDefault();
        }
    }

    private async Task<EnterpriseMember?> FindMembershipAsync(Guid userId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _memberRepository.GetQueryableAsync();
            return await _asyncExecuter.FirstOrDefaultAsync(query.Where(x => x.UserId == userId));
        }
    }

    private async Task<bool> CanManageAsync(EnterpriseProfile profile, Guid? userId)
    {
        if (IsTenantAdmin())
        {
            return true;
        }

        if (!userId.HasValue)
        {
            return false;
        }

        if (profile.OwnerUserId == userId.Value)
        {
            return true;
        }

        var member = await FindMembershipAsync(userId.Value);
        return member != null
               && member.EnterpriseProfileId == profile.Id
               && member.Role == EnterpriseMemberRole.Admin;
    }

    private bool IsTenantAdmin()
    {
        var roles = CurrentUser.Roles;
        return roles != null && (roles.Contains("SchoolAdmin") || roles.Contains("admin"));
    }

    private async Task<EnterpriseProfileDto> ToDtoAsync(EnterpriseProfile profile)
    {
        var memberQuery = await _memberRepository.GetQueryableAsync();
        var memberCount = await _asyncExecuter.CountAsync(
            memberQuery.Where(x => x.EnterpriseProfileId == profile.Id));

        return new EnterpriseProfileDto
        {
            Id = profile.Id,
            TenantId = profile.TenantId,
            Name = profile.Name,
            UnifiedSocialCreditCode = profile.UnifiedSocialCreditCode,
            Industry = profile.Industry,
            ContactName = profile.ContactName,
            ContactPhone = profile.ContactPhone,
            ContactEmail = profile.ContactEmail,
            Summary = profile.Summary,
            LogoUrl = profile.LogoUrl,
            CertificationStatus = profile.CertificationStatus,
            CertificationStatusName = profile.CertificationStatus switch
            {
                EnterpriseCertificationStatus.Draft => "草稿",
                EnterpriseCertificationStatus.PendingReview => "待审核",
                EnterpriseCertificationStatus.Certified => "已认证",
                EnterpriseCertificationStatus.Rejected => "已驳回",
                EnterpriseCertificationStatus.Suspended => "已冻结",
                _ => profile.CertificationStatus.ToString()
            },
            RejectReason = profile.RejectReason,
            OwnerUserId = profile.OwnerUserId,
            CertifiedAt = profile.CertifiedAt,
            ValidFrom = profile.ValidFrom,
            ValidTo = profile.ValidTo,
            IsUsable = profile.IsUsable(_clock.Now),
            MemberCount = memberCount
        };
    }

    private static EnterpriseMemberDto MapMember(EnterpriseMember member)
    {
        return new EnterpriseMemberDto
        {
            Id = member.Id,
            EnterpriseProfileId = member.EnterpriseProfileId,
            UserId = member.UserId,
            UserName = member.UserName,
            Role = member.Role,
            IsPrimary = member.IsPrimary
        };
    }
}

public interface IEnterpriseAppService
{
    Task<EnterpriseProfileDto?> GetMineAsync();
    Task<EnterpriseProfileDto> GetAsync(Guid id);
    Task<PagedResultDto<EnterpriseProfileDto>> GetListAsync(PagedResultRequestDto input);
    Task<EnterpriseProfileDto> CreateAsync(CreateUpdateEnterpriseProfileDto input);
    Task<EnterpriseProfileDto> UpdateAsync(Guid id, CreateUpdateEnterpriseProfileDto input);
    Task<EnterpriseProfileDto> SubmitCertificationAsync(Guid id);
    Task<EnterpriseMemberDto> BindMemberAsync(BindEnterpriseMemberDto input);
    Task<List<EnterpriseMemberDto>> GetMembersAsync(Guid enterpriseProfileId);
}
