using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Whiteboards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Hubs;

/// <summary>
/// 教研白板实时协同 Hub。
///
/// <para><b>为什么用 SignalR 而不是轮询</b>：多个教师同时编辑同一画布，
/// 需要「元素增改移 + 光标位置 + 在线成员」的毫秒级同步，轮询在 15 秒演示里抖动明显。</para>
///
/// <para><b>群组模型</b>：一个白板 = 一个 SignalR Group（<c>board:{whiteboardId}</c>）。</para>
///
/// <para><b>持久化边界</b>：</para>
/// <list type="bullet">
/// <item><description>元素 upsert / delete → **写库 + 广播**（断线重连、后来者进房都能拿到全量）。</description></item>
/// <item><description>光标 → **只广播不写库**（高频、无历史价值）。</description></item>
/// </list>
/// </summary>
[Authorize]
public class WhiteboardHub : Hub
{
    public const string Path = "/signalr/whiteboards";

    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, WhiteboardPresence>> Presence = new();

    private readonly IRepository<Whiteboard, Guid> _boardRepository;
    private readonly IRepository<WhiteboardMember, Guid> _memberRepository;
    private readonly IRepository<WhiteboardElement, Guid> _elementRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly ILogger<WhiteboardHub> _logger;

    public WhiteboardHub(
        IRepository<Whiteboard, Guid> boardRepository,
        IRepository<WhiteboardMember, Guid> memberRepository,
        IRepository<WhiteboardElement, Guid> elementRepository,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter,
        ILogger<WhiteboardHub> logger)
    {
        _boardRepository = boardRepository;
        _memberRepository = memberRepository;
        _elementRepository = elementRepository;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
        _logger = logger;
    }

    private static string GroupOf(Guid boardId) => $"board:{boardId:N}";

    /// <summary>进入白板：校验成员身份 → 加入群组 → 回推在线成员与画布全量。</summary>
    public async Task<JoinResult> Join(Guid whiteboardId)
    {
        var me = _currentUser.Id ?? throw new HubException("未登录。");

        var board = await FindBoardAsync(whiteboardId)
                    ?? throw new HubException("白板不存在。");

        var member = await FindMemberAsync(whiteboardId, me);
        var isOwner = board.CreatorId == me;
        if (member == null && !isOwner)
        {
            throw new HubException("你不是该白板的共创成员。");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupOf(whiteboardId));

        var displayName = member?.UserName ?? _currentUser.UserName ?? "教师";
        var color = member?.Color ?? "#2f6fed";

        var presence = Presence.GetOrAdd(GroupOf(whiteboardId), _ => new ConcurrentDictionary<Guid, WhiteboardPresence>());
        presence[me] = new WhiteboardPresence(me, displayName, color, Context.ConnectionId);

        // 通知其他人「有人进来了」
        await Clients.OthersInGroup(GroupOf(whiteboardId))
            .SendAsync("PresenceJoin", new { userId = me, userName = displayName, color });

        _logger.LogInformation("[Whiteboard] {User} 加入白板 {Board}", displayName, whiteboardId);

        // 回推给本人：在线成员 + 画布全量（后来者靠这个补齐历史）
        var elements = await LoadElementsAsync(whiteboardId);
        return new JoinResult
        {
            WhiteboardId = whiteboardId,
            MyColor = color,
            MyName = displayName,
            OnlineMembers = presence.Values
                .Select(p => (object)new PresenceItem { UserId = p.UserId, UserName = p.UserName, Color = p.Color })
                .ToList(),
            Elements = elements,
        };
    }

    public async Task Leave(Guid whiteboardId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupOf(whiteboardId));
        await RemovePresenceAsync(whiteboardId);
    }

    /// <summary>元素新增/更新（同一个消息处理两者的 upsert）。</summary>
    public async Task UpsertElement(Guid whiteboardId, string elementKey, int elementType, string json)
    {
        var me = _currentUser.Id ?? throw new HubException("未登录。");
        if (string.IsNullOrWhiteSpace(elementKey) || elementKey.Length > 64)
        {
            throw new HubException("元素标识非法。");
        }

        const int maxJson = 200_000; // 单元素属性上限，防滥用（图片/视频只存 resourceId，不存 base64）
        if (json != null && json.Length > maxJson)
        {
            throw new HubException("元素内容过大（图片/视频请用资源引用，不要内联 base64）。");
        }

        await EnsureEditableAsync(whiteboardId, me);

        var editorName = _currentUser.UserName ?? "教师";
        var el = await FindElementAsync(whiteboardId, elementKey);

        if (el == null)
        {
            await _elementRepository.InsertAsync(new WhiteboardElement(
                Guid.NewGuid(), _currentTenant.Id, whiteboardId, elementKey,
                (WhiteboardElementType)elementType, json ?? "{}"), autoSave: true);
        }
        else
        {
            el.Update(json ?? "{}", (WhiteboardElementType)elementType, me, editorName);
            await _elementRepository.UpdateAsync(el, autoSave: true);
        }

        await Clients.OthersInGroup(GroupOf(whiteboardId)).SendAsync("ElementUpserted",
            new { key = elementKey, elementType, json, editorName });
    }

    public async Task DeleteElement(Guid whiteboardId, string elementKey)
    {
        var me = _currentUser.Id ?? throw new HubException("未登录。");
        await EnsureEditableAsync(whiteboardId, me);

        var el = await FindElementAsync(whiteboardId, elementKey);
        if (el != null)
        {
            await _elementRepository.DeleteAsync(el, autoSave: true);
        }

        await Clients.OthersInGroup(GroupOf(whiteboardId)).SendAsync("ElementDeleted", new { key = elementKey });
    }

    /// <summary>光标位置：只广播，不落库。</summary>
    public async Task Cursor(Guid whiteboardId, double x, double y)
    {
        var me = _currentUser.Id;
        if (!me.HasValue)
        {
            return;
        }

        var name = _currentUser.UserName ?? "教师";
        var color = Presence.TryGetValue(GroupOf(whiteboardId), out var p) && p.TryGetValue(me.Value, out var pr)
            ? pr.Color
            : "#2f6fed";

        await Clients.OthersInGroup(GroupOf(whiteboardId))
            .SendAsync("CursorMoved", new { userId = me.Value, name, color, x, y });
    }

    /// <summary>视口同步（可选）：让新加入者对齐当前视角。</summary>
    public async Task Viewport(Guid whiteboardId, double x, double y, double scale)
    {
        var me = _currentUser.Id ?? throw new HubException("未登录。");
        await EnsureMemberAsync(whiteboardId, me);

        var board = await FindBoardAsync(whiteboardId);
        if (board != null)
        {
            board.SetViewport(x, y, scale);
            // 视口变化频繁，不逐次落库；只在离开时持久化（DisconnectedAsync）
            _pendingViewport[board.Id] = (x, y, scale);
        }

        await Clients.OthersInGroup(GroupOf(whiteboardId)).SendAsync("ViewportChanged", new { x, y, scale });
    }

    private static readonly ConcurrentDictionary<Guid, (double X, double Y, double Scale)> _pendingViewport = new();

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var me = _currentUser.Id;
        if (me.HasValue)
        {
            foreach (var (group, members) in Presence)
            {
                if (members.TryGetValue(me.Value, out var pr) && pr.ConnectionId == Context.ConnectionId)
                {
                    members.TryRemove(me.Value, out _);
                    await Clients.Group(group).SendAsync("PresenceLeave", new { userId = me.Value, userName = pr.UserName });
                    break;
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ── 内部 ──────────────────────────────────────────────────────────

    private async Task<Whiteboard?> FindBoardAsync(Guid id)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await _boardRepository.FindAsync(id);
        }
    }

    private async Task<WhiteboardMember?> FindMemberAsync(Guid boardId, Guid userId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var q = await _memberRepository.GetQueryableAsync();
            return q.FirstOrDefault(m => m.WhiteboardId == boardId && m.UserId == userId);
        }
    }

    private async Task<WhiteboardElement?> FindElementAsync(Guid boardId, string key)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var q = await _elementRepository.GetQueryableAsync();
            return q.FirstOrDefault(e => e.WhiteboardId == boardId && e.ElementKey == key);
        }
    }

    private async Task<List<object>> LoadElementsAsync(Guid boardId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var q = await _elementRepository.GetQueryableAsync();
            return q.Where(e => e.WhiteboardId == boardId)
                .OrderBy(e => e.CreationTime)
                .ToList()
                .Select(e => (object)new { key = e.ElementKey, elementType = (int)e.ElementType, json = e.Json })
                .ToList();
        }
    }

    private async Task EnsureMemberAsync(Guid boardId, Guid userId)
    {
        var board = await FindBoardAsync(boardId) ?? throw new HubException("白板不存在。");
        if (board.CreatorId == userId)
        {
            return;
        }

        if (await FindMemberAsync(boardId, userId) == null)
        {
            throw new HubException("你不是该白板的共创成员。");
        }
    }

    /// <summary>可编辑校验：Owner / Editor 才能改画布；Viewer 只读。</summary>
    private async Task EnsureEditableAsync(Guid boardId, Guid userId)
    {
        var board = await FindBoardAsync(boardId) ?? throw new HubException("白板不存在。");
        if (board.CreatorId == userId)
        {
            return;
        }

        var member = await FindMemberAsync(boardId, userId);
        if (member == null)
        {
            throw new HubException("你不是该白板的共创成员。");
        }

        if (member.Role == WhiteboardMemberRole.Viewer)
        {
            throw new HubException("你在该白板中是只读成员，无法编辑。");
        }
    }

    private async Task RemovePresenceAsync(Guid boardId)
    {
        var me = _currentUser.Id;
        if (!me.HasValue)
        {
            return;
        }

        var group = GroupOf(boardId);
        if (Presence.TryGetValue(group, out var members) && members.TryRemove(me.Value, out var pr))
        {
            await Clients.Group(group).SendAsync("PresenceLeave", new { userId = me.Value, userName = pr.UserName });
        }
    }

    public sealed record WhiteboardPresence(Guid UserId, string UserName, string Color, string ConnectionId);

    public sealed class PresenceItem
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Color { get; set; } = "#2f6fed";
    }

    public sealed class JoinResult
    {
        public Guid WhiteboardId { get; set; }
        public string MyColor { get; set; } = "#2f6fed";
        public string MyName { get; set; } = string.Empty;
        public List<object> OnlineMembers { get; set; } = new();
        public List<object> Elements { get; set; } = new();
    }
}
