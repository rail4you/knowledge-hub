using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.MenuVisibility.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.MenuVisibility;

/// <summary>
/// 按用户（老师）控制侧边栏一级菜单可见性。
///
/// <para>数据只有一处：<see cref="UserMenuVisibility"/>（每个用户最多一行，"被隐藏的菜单名"列表）。
/// 前端启动时拉自己的隐藏列表，把对应的一级菜单从侧边栏过滤掉。</para>
///
/// <para>权限边界：改别人要 <c>KnowledgeHub.MenuVisibility</c>（院校管理员 / 宿主管理员）；
/// 读自己不需要额外权限（老师登录后必须能读到自己的配置）。</para>
/// </summary>
public class MenuVisibilityAppService : KnowledgeHubAppService, IMenuVisibilityAppService
{
    /// <summary>一级菜单名统一前缀：路由节点名形如 <c>::Menu:Employment</c>。</summary>
    private const string MenuNamePrefix = "::Menu:";

    /// <summary>「老师」角色名（每个租户各有一条同名角色）。</summary>
    private const string TeacherRoleName = "Teacher";

    private readonly IRepository<UserMenuVisibility, Guid> _repository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IdentityUserManager _userManager;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly IDataFilter _dataFilter;

    public MenuVisibilityAppService(
        IRepository<UserMenuVisibility, Guid> repository,
        IRepository<IdentityUser, Guid> userRepository,
        IdentityUserManager userManager,
        IRepository<Tenant, Guid> tenantRepository,
        IDataFilter dataFilter)
    {
        _repository = repository;
        _userRepository = userRepository;
        _userManager = userManager;
        _tenantRepository = tenantRepository;
        _dataFilter = dataFilter;
    }

    /// <summary>
    /// 当前用户被隐藏的一级菜单名。
    /// 未登录（如登录页）直接返回空列表，让前端走默认全量菜单。
    /// </summary>
    public async Task<List<string>> GetMyHiddenMenusAsync()
    {
        if (CurrentUser.Id == null)
        {
            return new List<string>();
        }

        var userId = CurrentUser.Id.Value;
        var row = await FindByUserAsync(userId, CurrentTenant.Id);
        return row?.GetHiddenMenus() ?? new List<string>();
    }

    [Authorize(KnowledgeHubPermissions.MenuVisibility.Default)]
    public async Task<PagedResultDto<MenuVisibilityListItemDto>> GetTeachersAsync(
        GetMenuVisibilityTeachersInput input)
    {
        // 租户管理员恒定只能看本租户（忽略传入的 tenantId）；
        // 宿主管理员必须指定租户 —— 「Teacher」是同名角色按租户各建一份，
        // 宿主上下文（tenantId == null）下按角色名查不到任何老师。
        var tenantId = CurrentTenant.Id ?? input.TenantId;
        if (tenantId == null)
        {
            return new PagedResultDto<MenuVisibilityListItemDto>(0, new List<MenuVisibilityListItemDto>());
        }

        // 宿主管理员查某个租户：需要租户名给前端展示（此时当前租户为 null，要关过滤器）。
        string? tenantName = null;
        if (!CurrentTenant.Id.HasValue)
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var tenant = await _tenantRepository.FindAsync(tenantId.Value);
                tenantName = tenant?.Name;
            }
        }

        // 切到目标租户上下文：UserManager / 仓储的租户过滤器都按它来。
        using var tenantScope = CurrentTenant.Change(tenantId.Value);

        // 「老师」= 拥有 Teacher 角色的用户。
        // IdentityUserRole 是复合主键实体（不是 IEntity<Guid>），无法注入仓储 join，
        // 因此沿用项目既有做法：UserManager 按角色名取回（仅本租户）。
        var teachers = await _userManager.GetUsersInRoleAsync(TeacherRoleName);

        IEnumerable<IdentityUser> query = teachers;
        if (!input.Filter.IsNullOrWhiteSpace())
        {
            var keyword = input.Filter!.Trim();
            query = query.Where(u =>
                (u.UserName != null && u.UserName.Contains(keyword)) ||
                (u.Name != null && u.Name.Contains(keyword)));
        }

        var filtered = query.OrderBy(u => u.UserName).ToList();
        var totalCount = filtered.Count;
        var users = filtered
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        if (!users.Any())
        {
            return new PagedResultDto<MenuVisibilityListItemDto>(totalCount, new List<MenuVisibilityListItemDto>());
        }

        // 一次性把这一页用户的配置捞回来，避免 N+1。
        var pageUserIds = users.Select(u => u.Id).ToList();
        var visibilityQuery = await _repository.GetQueryableAsync();
        var visibilityRows = await AsyncExecuter.ToListAsync(
            visibilityQuery.Where(v => pageUserIds.Contains(v.UserId)));
        var hiddenByUser = visibilityRows.ToDictionary(v => v.UserId, v => v.GetHiddenMenus());

        var items = users.Select(u => new MenuVisibilityListItemDto
        {
            UserId = u.Id,
            UserName = u.UserName ?? string.Empty,
            DisplayName = u.Name,
            TenantId = u.TenantId,
            TenantName = tenantName,
            HiddenMenus = hiddenByUser.GetValueOrDefault(u.Id) ?? new List<string>(),
        }).ToList();

        return new PagedResultDto<MenuVisibilityListItemDto>(totalCount, items);
    }

    [Authorize(KnowledgeHubPermissions.MenuVisibility.Default)]
    public async Task<MenuVisibilityDto> GetUserMenusAsync(Guid userId)
    {
        var user = await GetTargetUserAsync(userId);
        var row = await FindByUserAsync(user.Id, user.TenantId ?? CurrentTenant.Id);

        return new MenuVisibilityDto
        {
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            DisplayName = user.Name,
            HiddenMenus = row?.GetHiddenMenus() ?? new List<string>(),
        };
    }

    [Authorize(KnowledgeHubPermissions.MenuVisibility.Default)]
    public async Task SetUserMenusAsync(SetMenuVisibilityInput input)
    {
        var user = await GetTargetUserAsync(input.UserId);
        var hidden = Sanitize(input.HiddenMenus);

        var tenantId = user.TenantId ?? CurrentTenant.Id;
        var row = await FindByUserAsync(user.Id, tenantId);
        if (row == null)
        {
            row = new UserMenuVisibility(GuidGenerator.Create(), user.Id, tenantId);
            row.SetHiddenMenus(hidden);
            await _repository.InsertAsync(row, autoSave: true);
        }
        else
        {
            row.SetHiddenMenus(hidden);
            await _repository.UpdateAsync(row, autoSave: true);
        }
    }

    [Authorize(KnowledgeHubPermissions.MenuVisibility.Default)]
    public async Task ResetUserMenusAsync(Guid userId)
    {
        var user = await GetTargetUserAsync(userId);
        var row = await FindByUserAsync(user.Id, user.TenantId ?? CurrentTenant.Id);
        if (row == null)
        {
            return;
        }

        // 不做物理删除：实体是软删除的，唯一索引 (TenantId, UserId) 会把软删行也算进去，
        // 删完再保存会撞唯一索引。这里直接把隐藏列表清空，效果等价。
        row.SetHiddenMenus(Array.Empty<string>());
        await _repository.UpdateAsync(row, autoSave: true);
    }

    /// <summary>
    /// 校验目标用户存在且属于"我能管理的范围"：
    /// 租户管理员只能管本租户用户，宿主管理员可管任意租户用户。
    /// </summary>
    private async Task<IdentityUser> GetTargetUserAsync(Guid userId)
    {
        IdentityUser? user;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            user = await _userRepository.FindAsync(userId);
        }

        if (user == null)
        {
            throw new UserFriendlyException("用户不存在。");
        }

        if (CurrentTenant.Id.HasValue)
        {
            if (user.TenantId != CurrentTenant.Id.Value)
            {
                throw new UserFriendlyException("只能管理本租户用户的菜单可见性。");
            }
        }
        else if (user.TenantId == null)
        {
            // 宿主管理员不改宿主账号的菜单（宿主只有 admin，改坏了会把自己锁在外面）。
            throw new UserFriendlyException("不能设置宿主账号的菜单可见性。");
        }

        return user;
    }

    /// <summary>
    /// 按「用户 + 租户」精确查配置行。
    /// 必须关闭租户过滤器：宿主管理员为租户用户配置时当前租户为 null。
    /// </summary>
    private async Task<UserMenuVisibility?> FindByUserAsync(Guid userId, Guid? tenantId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _repository.GetQueryableAsync();
            return query.FirstOrDefault(x => x.UserId == userId && x.TenantId == tenantId);
        }
    }

    /// <summary>只接受 <c>::Menu:</c> 前缀的节点名，避免前端传脏数据进来。</summary>
    private static List<string> Sanitize(List<string>? menuNames)
    {
        if (menuNames == null || menuNames.Count == 0)
        {
            return new List<string>();
        }

        return menuNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(x => x.StartsWith(MenuNamePrefix, StringComparison.Ordinal))
            .Where(x => x.Length <= UserMenuVisibility.MaxMenuNameLength)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(UserMenuVisibility.MaxHiddenMenuCount)
            .ToList();
    }
}
