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

    public LiveRoom(Guid liveId)
    {
        LiveId = liveId;
    }

    /// <summary>添加或更新参与者连接</summary>
    public void AddParticipant(string userId, WebSocket ws, string role, string userName)
    {
        _participants[userId] = new ParticipantConnection
        {
            UserId = userId,
            UserName = userName,
            Role = role,
            Ws = ws,
            ConnectedAt = DateTime.UtcNow,
        };
    }

    /// <summary>移除参与者</summary>
    public void RemoveParticipant(string userId)
    {
        _participants.TryRemove(userId, out _);
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