using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.Whiteboards.Dtos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Whiteboards;

/// <summary>
/// 教研白板元信息 / 成员 / 快照。
///
/// <para>协作要素说明：</para>
/// <list type="bullet">
/// <item><description>「邀请其他教师」范围 = **当前租户（学校）内的其他教师**，由 Identity 角色过滤。</description></item>
/// <item><description>画布元素按元素粒度存表，实时增量走 SignalR Hub，本服务只提供进入房间的全量快照。</description></item>
/// <item><description>创建者 = Owner（可邀请/改名/清空），被邀请者默认 Editor（可改画布）。</description></item>
/// </list>
/// </summary>
[Authorize(KnowledgeHubPermissions.Whiteboards.Default)]
public class WhiteboardAppService : ApplicationService, IWhiteboardAppService
{
    /// <summary>光标配色池（同一白板内尽量不重复）。</summary>
    private static readonly string[] Palette =
    {
        "#2f6fed", "#e0662f", "#18a058", "#a24bd6", "#d6a21e", "#0f9b8e", "#d63b6a", "#5c6bc0",
    };

    private readonly IRepository<Whiteboard, Guid> _boardRepository;
    private readonly IRepository<WhiteboardMember, Guid> _memberRepository;
    private readonly IRepository<WhiteboardElement, Guid> _elementRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    /// <summary>按角色取「本租户教师」（UserManager.GetUsersInRoleAsync；IdentityUserRole 是复合主键，
    /// 无法用 IRepository&lt;,&gt; 直接查 join 表）。</summary>
    private readonly IdentityUserManager _userManager;
    private readonly IDataFilter _dataFilter;

    public WhiteboardAppService(
        IRepository<Whiteboard, Guid> boardRepository,
        IRepository<WhiteboardMember, Guid> memberRepository,
        IRepository<WhiteboardElement, Guid> elementRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IdentityUserManager userManager,
        IDataFilter dataFilter)
    {
        _boardRepository = boardRepository;
        _memberRepository = memberRepository;
        _elementRepository = elementRepository;
        _userRepository = userRepository;
        _userManager = userManager;
        _dataFilter = dataFilter;
    }

    private Guid Me => CurrentUser.Id ?? throw new UserFriendlyException("请先登录。");

    public async Task<List<WhiteboardListItemDto>> GetListAsync()
    {
        var me = Me;

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var memberQuery = await _memberRepository.GetQueryableAsync();
            var myBoardIds = memberQuery.Where(m => m.UserId == me).Select(m => m.WhiteboardId).ToList();

            var boardQuery = await _boardRepository.GetQueryableAsync();
            var boards = boardQuery
                .Where(b => b.CreatorId == me || myBoardIds.Contains(b.Id))
                .OrderByDescending(b => b.LastModificationTime ?? b.CreationTime)
                .ToList();

            var ids = boards.Select(b => b.Id).ToList();
            var members = memberQuery.Where(m => ids.Contains(m.WhiteboardId)).ToList();
            var elementQuery = await _elementRepository.GetQueryableAsync();
            var elements = elementQuery.Where(e => ids.Contains(e.WhiteboardId))
                .Select(e => e.WhiteboardId).ToList();

            return boards.Select(b => new WhiteboardListItemDto
            {
                Id = b.Id,
                Title = b.Title,
                Topic = b.Topic,
                Subject = b.Subject,
                CreatorName = b.CreatorName,
                MemberCount = members.Count(m => m.WhiteboardId == b.Id),
                ElementCount = elements.Count(x => x == b.Id),
                IsOwner = b.CreatorId == me,
                CreationTime = b.CreationTime,
                LastModificationTime = b.LastModificationTime,
            }).ToList();
        }
    }

    public async Task<WhiteboardDetailDto> GetAsync(Guid id)
    {
        var me = Me;
        var board = await GetBoardAsync(id);
        var membership = await GetMembershipAsync(id, me);

        if (board.CreatorId != me && membership == null && !await IsTenantAdminAsync())
        {
            throw new UserFriendlyException("你不是该白板的共创成员，无法进入。");
        }

        var members = await GetMemberListAsync(id);
        var elements = await GetElementListAsync(id);

        var dto = MapDetail(board, members, elements, me);
        dto.CanEdit = board.CreatorId == me
                      || membership is { Role: WhiteboardMemberRole.Editor or WhiteboardMemberRole.Owner }
                      || await IsTenantAdminAsync();
        dto.MyColor = membership?.Color ?? Palette[0];
        dto.MyName = CurrentUser.UserName ?? "教师";
        return dto;
    }

    [Authorize(KnowledgeHubPermissions.Whiteboards.Create)]
    public async Task<WhiteboardDetailDto> CreateAsync(CreateWhiteboardDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("请填写白板标题。");
        }

        var me = Me;
        var name = CurrentUser.UserName ?? "教师";

        var board = new Whiteboard(GuidGenerator.Create(), CurrentTenant.Id, input.Title, me, name);
        board.SetMeta(input.Topic, input.Subject, input.Description);
        await _boardRepository.InsertAsync(board, autoSave: true);

        // 创建者自动成为 Owner 成员（成员表口径统一，列表/光标/头像都读这张表）
        await _memberRepository.InsertAsync(new WhiteboardMember(
            GuidGenerator.Create(), CurrentTenant.Id, board.Id, me, name, Palette[0],
            WhiteboardMemberRole.Owner), autoSave: true);

        var members = await GetMemberListAsync(board.Id);
        var created = MapDetail(board, members, new List<WhiteboardElementDto>(), me);
        // 刚创建的白板：创建者当然可编辑（MapDetail 不推断，这里显式给）
        created.CanEdit = true;
        created.MyColor = members.FirstOrDefault(m => m.UserId == me)?.Color ?? Palette[0];
        created.MyName = name;
        return created;
    }

    public async Task<WhiteboardDetailDto> UpdateAsync(Guid id, UpdateWhiteboardDto input)
    {
        var me = Me;
        var board = await GetBoardAsync(id);
        await EnsureOwnerOrAdminAsync(board, me);

        if (!string.IsNullOrWhiteSpace(input.Title))
        {
            board.Rename(input.Title!);
        }

        board.SetMeta(input.Topic ?? board.Topic, input.Subject ?? board.Subject,
            input.Description ?? board.Description);
        await _boardRepository.UpdateAsync(board, autoSave: true);

        return MapDetail(board, await GetMemberListAsync(id), await GetElementListAsync(id), me);
    }

    public async Task DeleteAsync(Guid id)
    {
        var me = Me;
        var board = await GetBoardAsync(id);
        await EnsureOwnerOrAdminAsync(board, me);

        using (_dataFilter.Disable<IMultiTenant>())
        {
            await _elementRepository.DeleteAsync(e => e.WhiteboardId == id);
            await _memberRepository.DeleteAsync(m => m.WhiteboardId == id);
        }

        await _boardRepository.DeleteAsync(board);
    }

    public async Task<List<InvitableTeacherDto>> GetInvitableTeachersAsync(Guid id)
    {
        var board = await GetBoardAsync(id);
        await EnsureOwnerOrAdminAsync(board, Me);

        var memberIds = (await GetMemberListAsync(id)).Select(m => m.UserId).ToHashSet();

        // 同租户内的「教师」：按 Identity 角色 Teacher 过滤；无角色信息的用户不列出，避免把学生/家长混进来
        var teachers = await GetTenantTeachersAsync();

        return teachers
            .Where(t => t.Id != board.CreatorId)
            .Select(t => new InvitableTeacherDto
            {
                UserId = t.Id,
                UserName = t.UserName ?? string.Empty,
                Name = t.Name,
                Joined = memberIds.Contains(t.Id),
            })
            .OrderBy(t => t.Joined)
            .ThenBy(t => t.UserName)
            .ToList();
    }

    public async Task<List<WhiteboardMemberDto>> InviteAsync(Guid id, InviteWhiteboardMembersDto input)
    {
        var me = Me;
        var board = await GetBoardAsync(id);
        await EnsureOwnerOrAdminAsync(board, me);

        if (input.UserIds == null || input.UserIds.Count == 0)
        {
            throw new UserFriendlyException("请选择要邀请的教师。");
        }

        var role = input.Role == (int)WhiteboardMemberRole.Viewer
            ? WhiteboardMemberRole.Viewer
            : WhiteboardMemberRole.Editor;

        var existing = await GetMemberListAsync(id);
        var usedColors = existing.Select(m => m.Color).ToHashSet();
        var added = 0;

        foreach (var userId in input.UserIds.Distinct())
        {
            if (existing.Any(m => m.UserId == userId))
            {
                continue;
            }

            using (_dataFilter.Disable<IMultiTenant>())
            {
                var user = await _userRepository.FindAsync(userId);
                if (user == null)
                {
                    continue;
                }

                var color = Palette.FirstOrDefault(c => !usedColors.Contains(c))
                            ?? Palette[added % Palette.Length];
                usedColors.Add(color);

                await _memberRepository.InsertAsync(new WhiteboardMember(
                    GuidGenerator.Create(), board.TenantId, board.Id, userId,
                    user.UserName ?? user.Name ?? "教师", color, role), autoSave: true);
                added++;
            }
        }

        if (added == 0)
        {
            throw new UserFriendlyException("所选教师都已在白板中。");
        }

        return await GetMemberListAsync(id);
    }

    public async Task RemoveMemberAsync(Guid id, Guid userId)
    {
        var board = await GetBoardAsync(id);
        await EnsureOwnerOrAdminAsync(board, Me);

        if (userId == board.CreatorId)
        {
            throw new UserFriendlyException("不能移除白板创建者。");
        }

        using (_dataFilter.Disable<IMultiTenant>())
        {
            await _memberRepository.DeleteAsync(m => m.WhiteboardId == id && m.UserId == userId);
        }
    }

    public async Task ClearElementsAsync(Guid id)
    {
        var board = await GetBoardAsync(id);
        await EnsureOwnerOrAdminAsync(board, Me);

        using (_dataFilter.Disable<IMultiTenant>())
        {
            await _elementRepository.DeleteAsync(e => e.WhiteboardId == id);
        }
    }

    // ── 内部 ──────────────────────────────────────────────────────────

    private async Task<Whiteboard> GetBoardAsync(Guid id)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await _boardRepository.FindAsync(id)
                   ?? throw new UserFriendlyException("白板不存在或已删除。");
        }
    }

    private async Task<WhiteboardMember?> GetMembershipAsync(Guid boardId, Guid userId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _memberRepository.GetQueryableAsync();
            return query.FirstOrDefault(m => m.WhiteboardId == boardId && m.UserId == userId);
        }
    }

    private async Task<List<WhiteboardMemberDto>> GetMemberListAsync(Guid boardId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _memberRepository.GetQueryableAsync();
            return query.Where(m => m.WhiteboardId == boardId)
                .OrderBy(m => m.Role)
                .ThenBy(m => m.JoinedAt)
                .Select(m => new WhiteboardMemberDto
                {
                    UserId = m.UserId,
                    UserName = m.UserName,
                    Color = m.Color,
                    Role = (int)m.Role,
                    RoleName = m.Role == WhiteboardMemberRole.Owner ? "创建者"
                        : m.Role == WhiteboardMemberRole.Editor ? "共创者" : "只读",
                    JoinedAt = m.JoinedAt,
                })
                .ToList();
        }
    }

    private async Task<List<WhiteboardElementDto>> GetElementListAsync(Guid boardId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _elementRepository.GetQueryableAsync();
            return query.Where(e => e.WhiteboardId == boardId)
                .OrderBy(e => e.CreationTime)
                .Select(e => new WhiteboardElementDto
                {
                    Key = e.ElementKey,
                    Json = e.Json,
                    ElementType = (int)e.ElementType,
                    LastEditorName = e.LastEditorName,
                    LastModificationTime = e.LastModificationTime,
                })
                .ToList();
        }
    }

    private async Task<List<IdentityUser>> GetTenantTeachersAsync()
    {
        // UserManager.GetUsersInRoleAsync 不区分租户，取回后按当前租户过滤
        // （学校租户里 Teacher 角色是按租户播种的，同名角色各租户独立）。
        var teachers = await _userManager.GetUsersInRoleAsync("Teacher");
        return teachers
            .Where(u => u.TenantId == CurrentTenant.Id)
            .ToList();
    }

    private async Task<bool> IsTenantAdminAsync()
    {
        return await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Whiteboards.Manage);
    }

    /// <summary>仅创建者或租户管理员可执行（改名 / 邀请 / 清空 / 删除）。</summary>
    private async Task EnsureOwnerOrAdminAsync(Whiteboard board, Guid userId)
    {
        if (board.CreatorId == userId)
        {
            return;
        }

        if (await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Whiteboards.Manage))
        {
            return;
        }

        throw new UserFriendlyException("只有白板创建者可以执行该操作。");
    }

    private WhiteboardDetailDto MapDetail(Whiteboard board, List<WhiteboardMemberDto> members,
        List<WhiteboardElementDto> elements, Guid me)
    {
        return new WhiteboardDetailDto
        {
            Id = board.Id,
            Title = board.Title,
            Topic = board.Topic,
            Subject = board.Subject,
            Description = board.Description,
            Status = (int)board.Status,
            CreatorId = board.CreatorId,
            CreatorName = board.CreatorName,
            MemberCount = members.Count,
            ElementCount = elements.Count,
            IsOwner = board.CreatorId == me,
            CreationTime = board.CreationTime,
            LastModificationTime = board.LastModificationTime,
            ViewX = board.ViewX,
            ViewY = board.ViewY,
            ViewScale = board.ViewScale,
            Members = members,
            Elements = elements,
        };
    }
}
