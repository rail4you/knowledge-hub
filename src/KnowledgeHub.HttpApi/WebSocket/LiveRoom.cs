using System;
using System.Net.WebSockets;

namespace KnowledgeHub.LiveWs;

/// <summary>直播房间（内存中管理 WebSocket 连接引用）</summary>
internal class LiveRoom
{
    public Guid LiveId { get; }
    public WebSocket? TeacherWs { get; set; }
    public WebSocket? StudentWs { get; set; }
    public string? TeacherUserId { get; set; }
    public string? StudentUserId { get; set; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public LiveRoom(Guid liveId)
    {
        LiveId = liveId;
    }

    public bool HasTeacher => TeacherWs is { State: WebSocketState.Open };
    public bool HasStudent => StudentWs is { State: WebSocketState.Open };

    /// <summary>获取对方 WebSocket</summary>
    public WebSocket? GetOther(WebSocket ws)
    {
        if (ws == TeacherWs) return StudentWs;
        if (ws == StudentWs) return TeacherWs;
        return null;
    }

    /// <summary>获取对方角色名</summary>
    public string? GetOtherRole(WebSocket ws)
    {
        if (ws == TeacherWs) return "student";
        if (ws == StudentWs) return "teacher";
        return null;
    }

    /// <summary>获取当前用户角色名</summary>
    public string? GetRole(WebSocket ws)
    {
        if (ws == TeacherWs) return "teacher";
        if (ws == StudentWs) return "student";
        return null;
    }

    /// <summary>房间是否有至少一人在线</summary>
    public bool HasAnyone => HasTeacher || HasStudent;
}
