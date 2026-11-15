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
using Volo.Abp.Uow;
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
/// <item><description>协作消息（文字）→ **写库 + 广播**（刷新后还要能看到讨论结论）。</description></item>
/// <item><description>语音状态与 WebRTC 信令 → **只广播 / 点对点转发，不落库**（连接态是瞬时的）。</description></item>
/// </list>
///
/// <para><b>语音模型</b>：服务端**不碰媒体流**，只做信令交换（offer / answer / candidate）。
/// 音频走浏览器之间的 WebRTC 点对点（mesh），因此 3~5 人的教研讨论完全够用；
/// 更多人数需要换成 SFU（如 LiveKit），当前不引入额外服务。</para>
/// </summary>
[Authorize]
public class WhiteboardHub : Hub
{
    public const string Path = "/signalr/whiteboards";

    /// <summary>聊天历史回放条数上限（进入白板时一次性下发）。</summary>
    private const int ChatHistoryLimit = 100;

    /// <summary>单条消息正文长度上限（与实体 HasMaxLength 一致）。</summary>
    private const int ChatTextMaxLength = 2000;

    /// <summary>在线成员表：白板 Id → (用户 Id → 在场状态)。进程内内存即可，连接断开即失效。</summary>
    private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, WhiteboardPresence>> Presence = new();

    private readonly IRepository<Whiteboard, Guid> _boardRepository;
    private readonly IRepository<WhiteboardMember, Guid> _memberRepository;
    private readonly IRepository<WhiteboardElement, Guid> _elementRepository;
    private readonly IRepository<WhiteboardChatMessage, Guid> _chatRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<WhiteboardHub> _logger;

    public WhiteboardHub(
        IRepository<Whiteboard, Guid> boardRepository,
        IRepository<WhiteboardMember, Guid> memberRepository,
        IRepository<WhiteboardElement, Guid> elementRepository,
        IRepository<WhiteboardChatMessage, Guid> chatRepository,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<WhiteboardHub> logger)
    {
        _boardRepository = boardRepository;
        _memberRepository = memberRepository;
        _elementRepository = elementRepository;
        _chatRepository = chatRepository;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    /// <summary>
    /// SignalR 的长连接会持有一个「环境 UnitOfWork」，连接结束时整体回滚：
    /// 直接用仓储写入的画布元素会随连接断开被回滚（库里查不到、刷新即丢）。
    /// </summary>
    /// <remarks>
    /// 这里显式开一个**独立且不参与外层事务**的 UoW，写完立即提交，
    /// 保证「元素落库 + 广播」两件事都不受连接生命周期影响。
    /// </remarks>
    private async Task WithIndependentUnitOfWorkAsync(Func<Task> action)
    {
        using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
        {
            await action();
            await uow.CompleteAsync();
        }
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

        var presence = Presence.GetOrAdd(whiteboardId, _ => new ConcurrentDictionary<Guid, WhiteboardPresence>());
        // 已经在语音里的用户（新连接默认不在语音中，麦克风由前端显式开启）
        presence[me] = new WhiteboardPresence(me, displayName, color, Context.ConnectionId);

        // 通知其他人「有人进来了」
        await Clients.OthersInGroup(GroupOf(whiteboardId))
            .SendAsync("PresenceJoin", new { userId = me, userName = displayName, color });

        _logger.LogInformation("[Whiteboard] {User} 加入白板 {Board}", displayName, whiteboardId);

        // 回推给本人：在线成员 + 语音在麦成员 + 画布全量 + 聊天历史（后来者靠这个补齐上下文）
        var elements = await LoadElementsAsync(whiteboardId);
        var chat = await LoadChatHistoryAsync(whiteboardId);
        return new JoinResult
        {
            WhiteboardId = whiteboardId,
            MyUserId = me,
            MyColor = color,
            MyName = displayName,
            OnlineMembers = presence.Values
                .Select(p => (object)new PresenceItem
                {
                    UserId = p.UserId,
                    UserName = p.UserName,
                    Color = p.Color,
                    InVoice = p.InVoice,
                    Muted = p.Muted,
                })
                .ToList(),
            Elements = elements,
            ChatHistory = chat,
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

        await WithIndependentUnitOfWorkAsync(async () =>
        {
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
        });

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
            await WithIndependentUnitOfWorkAsync(async () =>
            {
                var target = await FindElementAsync(whiteboardId, elementKey);
                if (target != null)
                {
                    await _elementRepository.DeleteAsync(target, autoSave: true);
                }
            });
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
        var color = Presence.TryGetValue(whiteboardId, out var p) && p.TryGetValue(me.Value, out var pr)
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

    // ── 聊天（文字）──────────────────────────────────────────────

    /// <summary>
    /// 发送一条聊天消息：落库后广播给房间所有人（含自己，便于多标签页同步）。
    /// </summary>
    /// <returns>落库后的消息（前端用它替换本地乐观气泡）。</returns>
    public async Task<ChatMessageItem> SendChatMessage(Guid whiteboardId, string text, string? clientId)
    {
        var me = _currentUser.Id ?? throw new HubException("未登录。");
        await EnsureMemberAsync(whiteboardId, me);

        var body = (text ?? string.Empty).Trim();
        if (body.Length == 0)
        {
            throw new HubException("消息内容不能为空。");
        }

        if (body.Length > ChatTextMaxLength)
        {
            body = body[..ChatTextMaxLength];
        }

        if (clientId != null && clientId.Length > 64)
        {
            clientId = clientId[..64];
        }

        var item = await SaveChatMessageAsync(whiteboardId, me, body,
            WhiteboardChatMessageKind.Text, clientId);

        await Clients.Group(GroupOf(whiteboardId)).SendAsync("ChatMessage", item);
        return item;
    }

    /// <summary>「正在输入」提示：只广播，不落库。</summary>
    public async Task Typing(Guid whiteboardId)
    {
        var me = _currentUser.Id;
        if (!me.HasValue)
        {
            return;
        }

        await Clients.OthersInGroup(GroupOf(whiteboardId)).SendAsync("ChatTyping", new
        {
            userId = me.Value,
            userName = DisplayNameOf(whiteboardId, me.Value),
        });
    }

    // ── 语音（WebRTC 信令）───────────────────────────────────────

    /// <summary>
    /// 加入语音通话：把自己的状态标记为「在麦上」，并返回**已经在语音里的人**，
    /// 由客户端向这些人逐个发 offer，形成点对点音频 mesh。
    /// </summary>
    public async Task<List<VoiceParticipant>> JoinVoice(Guid whiteboardId)
    {
        var me = _currentUser.Id ?? throw new HubException("未登录。");
        await EnsureMemberAsync(whiteboardId, me);

        var group = GroupOf(whiteboardId);
        var presence = Presence.GetOrAdd(whiteboardId, _ => new ConcurrentDictionary<Guid, WhiteboardPresence>());
        var others = presence.Values
            .Where(p => p.UserId != me && p.InVoice)
            .Select(p => new VoiceParticipant { UserId = p.UserId, UserName = p.UserName })
            .ToList();

        presence[me] = presence.TryGetValue(me, out var self)
            ? self with { InVoice = true, ConnectionId = Context.ConnectionId }
            : new WhiteboardPresence(me, _currentUser.UserName ?? "教师", "#2f6fed", Context.ConnectionId, InVoice: true);

        var name = presence[me].UserName;

        await Clients.OthersInGroup(group).SendAsync("VoiceStateChanged", new
        {
            userId = me,
            userName = name,
            inVoice = true,
            muted = presence[me].Muted,
        });

        await BroadcastSystemMessageAsync(whiteboardId, $"{name} 加入了语音通话");

        return others;
    }

    /// <summary>离开语音通话：关麦并通知其他人拆除对应的 PeerConnection。</summary>
    public async Task LeaveVoice(Guid whiteboardId)
    {
        var me = _currentUser.Id;
        if (!me.HasValue)
        {
            return;
        }

        var group = GroupOf(whiteboardId);
        if (!Presence.TryGetValue(whiteboardId, out var presence) || !presence.TryGetValue(me.Value, out var pr))
        {
            return;
        }

        presence[me.Value] = pr with { InVoice = false, Muted = false };

        await Clients.OthersInGroup(group).SendAsync("VoiceStateChanged", new
        {
            userId = me.Value,
            userName = pr.UserName,
            inVoice = false,
            muted = false,
        });

        await BroadcastSystemMessageAsync(whiteboardId, $"{pr.UserName} 离开了语音通话");
    }

    /// <summary>麦克风静音状态：其他人据此在成员头像上显示静音图标。</summary>
    public async Task SetMic(Guid whiteboardId, bool muted)
    {
        var me = _currentUser.Id;
        if (!me.HasValue)
        {
            return;
        }

        var group = GroupOf(whiteboardId);
        if (!Presence.TryGetValue(whiteboardId, out var presence) || !presence.TryGetValue(me.Value, out var pr))
        {
            return;
        }

        presence[me.Value] = pr with { Muted = muted };

        await Clients.OthersInGroup(group).SendAsync("VoiceStateChanged", new
        {
            userId = me.Value,
            userName = pr.UserName,
            inVoice = pr.InVoice,
            muted,
        });
    }

    /// <summary>
    /// WebRTC 信令转发（offer / answer / candidate）。
    ///
    /// <para>服务端只做「按 userId 找连接」转发，不解包也不落库 ——
    /// 媒体流本身在浏览器之间直连，服务端带宽不随通话增长。</para>
    /// </summary>
    public async Task VoiceSignal(Guid whiteboardId, Guid targetUserId, string type, string payload)
    {
        var me = _currentUser.Id ?? throw new HubException("未登录。");

        if (type is not ("offer" or "answer" or "candidate"))
        {
            return;
        }

        // 高频信令不走数据库校验，只认「双方都在这个房间」
        if (!Presence.TryGetValue(whiteboardId, out var presence)
            || !presence.TryGetValue(targetUserId, out var target)
            || !presence.TryGetValue(me, out var sender))
        {
            return;
        }

        if (payload != null && payload.Length > 64_000)
        {
            return;
        }

        await Clients.Client(target.ConnectionId).SendAsync("VoiceSignal", new
        {
            fromUserId = me,
            fromUserName = sender.UserName,
            type,
            payload,
        });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var me = _currentUser.Id;
        if (me.HasValue)
        {
            foreach (var (boardId, members) in Presence)
            {
                var group = GroupOf(boardId);
                if (members.TryGetValue(me.Value, out var pr) && pr.ConnectionId == Context.ConnectionId)
                {
                    members.TryRemove(me.Value, out _);
                    await Clients.Group(group).SendAsync("PresenceLeave", new { userId = me.Value, userName = pr.UserName });

                    if (pr.InVoice)
                    {
                        // 掉线时连接已被中断，注入的 CancellationToken 会直接取消写库；
                        // 这条提示只是「锦上添花」，失败不能影响断开流程（否则 Hub 会刷一堆 ERR）。
                        try
                        {
                            await BroadcastSystemMessageAsync(boardId, $"{pr.UserName} 因掉线离开了语音通话");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[Whiteboard] 掉线提示写入失败（可忽略）: {Board}", boardId);
                        }
                    }

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

    /// <summary>最近 N 条聊天记录（按时间正序返回，前端直接往下追加）。</summary>
    private async Task<List<ChatMessageItem>> LoadChatHistoryAsync(Guid boardId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var q = await _chatRepository.GetQueryableAsync();
            var recent = q.Where(m => m.WhiteboardId == boardId)
                .OrderByDescending(m => m.CreationTime)
                .Take(ChatHistoryLimit)
                .ToList();

            recent.Reverse();
            return recent.Select(ToChatItem).ToList();
        }
    }

    /// <summary>写一条消息（独立 UoW，写完整提交 —— 不能跟着连接的 UoW 回滚）。</summary>
    private async Task<ChatMessageItem> SaveChatMessageAsync(Guid boardId, Guid senderId, string text,
        WhiteboardChatMessageKind kind, string? clientId)
    {
        var color = Presence.TryGetValue(boardId, out var p) && p.TryGetValue(senderId, out var pr)
            ? pr.Color
            : null;
        var name = DisplayNameOf(boardId, senderId);

        var entity = new WhiteboardChatMessage(
            Guid.NewGuid(), _currentTenant.Id, boardId, senderId, name, text, kind, color, clientId);

        await WithIndependentUnitOfWorkAsync(async () =>
        {
            await _chatRepository.InsertAsync(entity, autoSave: true);
        });

        return ToChatItem(entity);
    }

    private static ChatMessageItem ToChatItem(WhiteboardChatMessage m) => new()
    {
        Id = m.Id,
        ClientId = m.ClientId,
        SenderId = m.SenderId,
        SenderName = m.SenderName,
        SenderColor = m.SenderColor,
        Text = m.Text,
        Kind = (int)m.Kind,
        CreationTime = m.CreationTime,
    };

    /// <summary>系统提示（加入/离开语音）：落库 + 广播，和普通发言同一条时间线。</summary>
    private async Task BroadcastSystemMessageAsync(Guid boardId, string text)
    {
        var me = _currentUser.Id ?? Guid.Empty;
        var item = await SaveChatMessageAsync(boardId, me, text, WhiteboardChatMessageKind.System, null);
        await Clients.Group(GroupOf(boardId)).SendAsync("ChatMessage", item);
    }

    private string DisplayNameOf(Guid boardId, Guid userId)
    {
        if (Presence.TryGetValue(boardId, out var p) && p.TryGetValue(userId, out var pr))
        {
            return pr.UserName;
        }

        return _currentUser.UserName ?? "教师";
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
        if (Presence.TryGetValue(boardId, out var members) && members.TryRemove(me.Value, out var pr))
        {
            await Clients.Group(group).SendAsync("PresenceLeave", new { userId = me.Value, userName = pr.UserName });
        }
    }

    public sealed record WhiteboardPresence(
        Guid UserId,
        string UserName,
        string Color,
        string ConnectionId,
        bool InVoice = false,
        bool Muted = false);

    public sealed class PresenceItem
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Color { get; set; } = "#2f6fed";
        public bool InVoice { get; set; }
        public bool Muted { get; set; }
    }

    /// <summary>语音通话参与者（JoinVoice 回给新加入者，用于逐人建立 offer）。</summary>
    public sealed class VoiceParticipant
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
    }

    public sealed class ChatMessageItem
    {
        public Guid Id { get; set; }
        public string? ClientId { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string? SenderColor { get; set; }
        public string Text { get; set; } = string.Empty;
        public int Kind { get; set; }
        public DateTime CreationTime { get; set; }
    }

    public sealed class JoinResult
    {
        public Guid WhiteboardId { get; set; }
        public Guid MyUserId { get; set; }
        public string MyColor { get; set; } = "#2f6fed";
        public string MyName { get; set; } = string.Empty;
        public List<object> OnlineMembers { get; set; } = new();
        public List<object> Elements { get; set; } = new();
        public List<ChatMessageItem> ChatHistory { get; set; } = new();
    }
}
