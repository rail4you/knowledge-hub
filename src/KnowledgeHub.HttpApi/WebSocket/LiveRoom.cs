using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.WebSockets;

namespace KnowledgeHub.LiveWs;

/// <summary>
/// 直播房间（内存中管理 WebSocket 连接引用）。
/// 支持一个教师 + 多个学生同时在线。
/// </summary>
internal class LiveRoom
{
    public Guid LiveId { get; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    /// <summary>房间内所有参与者连接</summary>
    private readonly ConcurrentDictionary<string, ParticipantConnection> _participants = new();

    /// <summary>
    /// 保护“清空后移除房间”的临界区。
    /// 房间被判定为空时会标记 _closed；并发的加入必须看到该标记并重建房间，
    /// 否则会出现“旧连接把刚有新人的房间从 Rooms 中删掉”，导致晚来的学生被隔离到另一个房间对象。
    /// </summary>
    private readonly object _sync = new();
    private bool _closed;

    public LiveRoom(Guid liveId)
    {
        LiveId = liveId;
    }

    /// <summary>房间是否已被判定为空并标记关闭</summary>
    public bool IsClosed
    {
        get { lock (_sync) { return _closed; } }
    }

    /// <summary>
    /// 添加或更新参与者连接。
    /// 若房间已被标记关闭则返回 false（调用方应丢弃该房间并重建）。
    /// </summary>
    public bool TryAddParticipant(string userId, WebSocket ws, string role, string userName)
    {
        lock (_sync)
        {
            if (_closed) return false;

            _participants[userId] = new ParticipantConnection
            {
                UserId = userId,
                UserName = userName,
                Role = role,
                Ws = ws,
                ConnectedAt = DateTime.UtcNow,
            };
            return true;
        }
    }

    /// <summary>添加或更新参与者连接</summary>
    public void AddParticipant(string userId, WebSocket ws, string role, string userName)
        => TryAddParticipant(userId, ws, role, userName);

    /// <summary>移除参与者</summary>
    public void RemoveParticipant(string userId)
    {
        lock (_sync)
        {
            _participants.TryRemove(userId, out _);
        }
    }

    /// <summary>
    /// 移除参与者（仅当存量连接就是断开的这条 WS 时才删），并原子地返回移除后房间是否已无人。
    /// 用户快速重进时，旧连接的断开清理不得删除新连接，否则新会话收不到后续信令。
    /// </summary>
    public bool RemoveParticipantAndCheckEmpty(string userId, WebSocket? ws)
    {
        lock (_sync)
        {
            if (ws == null)
            {
                _participants.TryRemove(userId, out _);
            }
            else if (_participants.TryGetValue(userId, out var p) && p.Ws == ws)
            {
                _participants.TryRemove(userId, out _);
            }

            var empty = !_participants.Values.Any(p => p.Ws is { State: WebSocketState.Open });
            if (empty) _closed = true;
            return empty;
        }
    }

    /// <summary>
    /// 移除参与者（仅当存量连接就是断开的这条 WS 时才删）。
    /// 用户快速重进时，旧连接的断开清理不得删除新连接，否则新会话收不到后续信令。
    /// </summary>
    public void RemoveParticipant(string userId, WebSocket? ws)
    {
        RemoveParticipantAndCheckEmpty(userId, ws);
    }

    /// <summary>根据 WebSocket 查找参与者信息</summary>
    public ParticipantConnection? GetParticipantByWs(WebSocket ws)
    {
        return _participants.Values.FirstOrDefault(p => p.Ws == ws);
    }

    /// <summary>获取所有在线参与者</summary>
    public System.Collections.Generic.List<ParticipantConnection> GetAllParticipants()
    {
        return _participants.Values
            .Where(p => p.Ws is { State: WebSocketState.Open })
            .OrderBy(p => p.Role == "teacher" ? 0 : 1)
            .ThenBy(p => p.UserName)
            .ToList();
    }

    /// <summary>获取除指定 WebSocket 以外的所有参与者</summary>
    public System.Collections.Generic.List<ParticipantConnection> GetOthers(WebSocket ws)
    {
        return _participants.Values
            .Where(p => p.Ws != ws && p.Ws is { State: WebSocketState.Open })
            .ToList();
    }

    /// <summary>获取特定用户名的参与者</summary>
    public ParticipantConnection? GetByUserId(string userId)
    {
        _participants.TryGetValue(userId, out var p);
        return p;
    }

    /// <summary>按角色获取参与者的 WebSocket</summary>
    public System.Collections.Generic.List<WebSocket> GetWebSocketsByRole(string role)
    {
        return _participants.Values
            .Where(p => p.Role == role && p.Ws is { State: WebSocketState.Open })
            .Select(p => p.Ws!)
            .ToList();
    }

    /// <summary>房间是否有参与者在在线</summary>
    public bool HasAnyone => _participants.Values.Any(p => p.Ws is { State: WebSocketState.Open });
}

internal class ParticipantConnection
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public WebSocket? Ws { get; set; }
    public DateTime ConnectedAt { get; set; }
}