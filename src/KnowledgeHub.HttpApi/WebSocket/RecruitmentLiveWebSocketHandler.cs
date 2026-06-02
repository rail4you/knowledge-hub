using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.RecruitmentLive;
using RecruitmentLiveEntity = KnowledgeHub.RecruitmentLive.RecruitmentLive;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Users;

namespace KnowledgeHub.LiveWs;

/// <summary>招聘直播 WebSocket 信令处理器</summary>
public class RecruitmentLiveWebSocketHandler
{
    private static readonly ConcurrentDictionary<string, LiveRoom> Rooms = new();
    private const int MaxMessageSize = 16384; // 16KB
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger<RecruitmentLiveWebSocketHandler> _logger;
    private readonly IServiceProvider _serviceProvider;

    public RecruitmentLiveWebSocketHandler(
        ILogger<RecruitmentLiveWebSocketHandler> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task HandleAsync(System.Net.WebSockets.WebSocket ws, HttpContext httpContext)
    {
        // 1. 从 query string 提取参数
        var token = httpContext.Request.Query["token"].ToString();
        var liveIdStr = httpContext.Request.Query["liveId"].ToString();

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(liveIdStr))
        {
            await SendErrorAndClose(ws, "缺少 token 或 liveId 参数");
            return;
        }

        if (!Guid.TryParse(liveIdStr, out var liveId))
        {
            await SendErrorAndClose(ws, "无效的 liveId");
            return;
        }

        // 2. 验证 token
        if (!TryDecryptToken(token, out var tokenLiveId, out var tokenUserId, out var tokenRole, out var expiresAt))
        {
            await SendErrorAndClose(ws, "无效的 token");
            return;
        }

        if (tokenLiveId != liveId)
        {
            await SendErrorAndClose(ws, "token 与 liveId 不匹配");
            return;
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAt)
        {
            await SendErrorAndClose(ws, "token 已过期");
            return;
        }

        // 3. 创建 scope 验证直播权限
        using var scope = _serviceProvider.CreateScope();
        var liveRepo = scope.ServiceProvider.GetRequiredService<IRepository<RecruitmentLiveEntity, Guid>>();

        RecruitmentLiveEntity? live;
        try
        {
            live = await liveRepo.FindAsync(liveId);
        }
        catch
        {
            await SendErrorAndClose(ws, "直播不存在");
            return;
        }

        if (live == null)
        {
            await SendErrorAndClose(ws, "直播不存在");
            return;
        }

        if (live.Status == RecruitmentLiveStatus.Ended || live.Status == RecruitmentLiveStatus.Cancelled)
        {
            await SendErrorAndClose(ws, "该直播已结束或已取消");
            return;
        }

        // 验证用户是否是参与者
        var userIdStr = tokenUserId.ToString();
        if (live.TeacherId != tokenUserId && live.StudentId != tokenUserId)
        {
            await SendErrorAndClose(ws, "您不是该直播的参与者");
            return;
        }

        var role = live.TeacherId == tokenUserId ? "teacher" : "student";

        // 4. 加入房间
        var roomKey = liveId.ToString();
        var room = Rooms.GetOrAdd(roomKey, _ => new LiveRoom(liveId));

        // 踢掉同一角色的旧连接
        if (role == "teacher")
        {
            if (room.TeacherWs is { State: System.Net.WebSockets.WebSocketState.Open } oldWs)
            {
                await TryCloseWebSocket(oldWs, "您已在其他设备进入直播间");
            }
            room.TeacherWs = ws;
            room.TeacherUserId = userIdStr;
        }
        else
        {
            if (room.StudentWs is { State: System.Net.WebSockets.WebSocketState.Open } oldWs)
            {
                await TryCloseWebSocket(oldWs, "您已在其他设备进入直播间");
            }
            room.StudentWs = ws;
            room.StudentUserId = userIdStr;
        }

        _logger.LogInformation("用户 {UserId}({Role}) 进入直播间 {LiveId}", userIdStr, role, liveId);

        // 通知对方
        var other = room.GetOther(ws);
        if (other is { State: System.Net.WebSockets.WebSocketState.Open })
        {
            await SendJson(other, new { type = "user-joined", role });
        }

        // 如果直播状态是 Waiting，更新为 Active
        if (live.Status == RecruitmentLiveStatus.Waiting && room.HasTeacher && room.HasStudent)
        {
            live.Start();
            await liveRepo.UpdateAsync(live, autoSave: true);
        }

        // 5. 消息循环
        await MessageLoop(ws, room, role, liveId, liveRepo);
    }

    private async Task MessageLoop(
        System.Net.WebSockets.WebSocket ws,
        LiveRoom room,
        string role,
        Guid liveId,
        IRepository<RecruitmentLiveEntity, Guid> liveRepo)
    {
        var buffer = new byte[MaxMessageSize];
        var lastPing = DateTime.UtcNow;

        try
        {
            while (ws.State == System.Net.WebSockets.WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                }
                catch (WebSocketException)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleMessage(ws, room, role, message);
                    lastPing = DateTime.UtcNow;
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Binary pong
                    lastPing = DateTime.UtcNow;
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "直播间 {LiveId} 消息循环异常", liveId);
        }
        finally
        {
            // 断开清理
            await HandleDisconnect(ws, room, role, liveId, liveRepo);
        }
    }

    private async Task HandleMessage(
        System.Net.WebSockets.WebSocket ws,
        LiveRoom room,
        string role,
        string message)
    {
        JsonElement? json = null;
        try
        {
            json = JsonSerializer.Deserialize<JsonElement>(message, JsonOptions);
        }
        catch
        {
            return;
        }

        var type = json?.TryGetProperty("type", out var t) == true ? t.GetString() : null;
        var other = room.GetOther(ws);

        switch (type)
        {
            case "ping":
                await SendJson(ws, new { type = "pong" });
                break;

            case "pong":
                // 心跳不处理
                break;

            case "offer":
            case "answer":
            case "ice-candidate":
                if (other is { State: System.Net.WebSockets.WebSocketState.Open })
                {
                    var data = json?.TryGetProperty("data", out var d) == true ? (object?)d : null;
                    await SendJson(other, new { type, data });
                }
                break;

            case "chat":
                if (other is { State: System.Net.WebSockets.WebSocketState.Open })
                {
                    var text = json?.TryGetProperty("data", out var cd) == true ? cd.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(text) && text.Length <= 500)
                    {
                        await SendJson(other, new { type = "chat", data = text, from = role });
                        await SendJson(ws, new { type = "chat", data = text, from = role, self = true });
                    }
                }
                break;

            case "hang-up":
                if (other is { State: System.Net.WebSockets.WebSocketState.Open })
                {
                    await SendJson(other, new { type = "hang-up", reason = "对方已挂断" });
                }
                break;

            default:
                _logger.LogDebug("直播间未知消息类型: {Type}", type);
                break;
        }
    }

    private async Task HandleDisconnect(
        System.Net.WebSockets.WebSocket ws,
        LiveRoom room,
        string role,
        Guid liveId,
        IRepository<RecruitmentLiveEntity, Guid> liveRepo)
    {
        _logger.LogInformation("用户 {Role} 离开直播间 {LiveId}", role, liveId);

        // 通知对方
        var other = room.GetOther(ws);
        if (other is { State: System.Net.WebSockets.WebSocketState.Open })
        {
            await SendJson(other, new { type = "user-left", role, reason = "对方已断开连接" });
        }

        // 清理房间引用
        if (role == "teacher")
        {
            room.TeacherWs = null;
            room.TeacherUserId = null;
        }
        else
        {
            room.StudentWs = null;
            room.StudentUserId = null;
        }

        // 如果房间空了，更新直播状态
        if (!room.HasAnyone)
        {
            try
            {
                var live = await liveRepo.FindAsync(liveId);
                if (live != null && live.Status == RecruitmentLiveStatus.Active)
                {
                    live.End();
                    await liveRepo.UpdateAsync(live, autoSave: true);
                    _logger.LogInformation("直播间 {LiveId} 双方都已离开，自动结束", liveId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "自动结束直播 {LiveId} 失败", liveId);
            }

            Rooms.TryRemove(liveId.ToString(), out _);
        }

        await TryCloseWebSocket(ws, null);
    }

    // ── 工具方法 ──

    private static async Task SendJson(System.Net.WebSockets.WebSocket ws, object obj)
    {
        if (ws.State != System.Net.WebSockets.WebSocketState.Open) return;

        try
        {
            var json = JsonSerializer.Serialize(obj, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            await ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        catch
        {
            // 发送失败忽略
        }
    }

    private static async Task SendErrorAndClose(System.Net.WebSockets.WebSocket ws, string message)
    {
        await SendJson(ws, new { type = "error", message });
        await TryCloseWebSocket(ws, message);
    }

    private static async Task TryCloseWebSocket(System.Net.WebSockets.WebSocket ws, string? reason)
    {
        if (ws.State == System.Net.WebSockets.WebSocketState.Open)
        {
            // 如果提供了原因，先发一条消息
            if (reason != null)
            {
                try
                {
                    await SendJson(ws, new { type = "error", message = reason });
                }
                catch { }
            }

            try
            {
                await ws.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    reason ?? "关闭连接",
                    CancellationToken.None);
            }
            catch
            {
                // 忽略关闭失败
            }
        }
    }

    // ── Token 加解密 ──

    private static readonly byte[] AesKey = SHA256.HashData(Encoding.UTF8.GetBytes("KnowledgeHub-RecruitmentLive-WS-2026"));

    internal static bool TryDecryptToken(string token, out Guid liveId, out Guid userId, out string role, out long expiresAt)
    {
        liveId = Guid.Empty;
        userId = Guid.Empty;
        role = string.Empty;
        expiresAt = 0;

        try
        {
            var data = Convert.FromBase64String(token);
            if (data.Length < 16 + 4) return false; // IV(16) + at least some data

            var iv = new byte[16];
            Buffer.BlockCopy(data, 0, iv, 0, 16);
            var cipher = new byte[data.Length - 16];
            Buffer.BlockCopy(data, 16, cipher, 0, cipher.Length);

            using var aes = Aes.Create();
            aes.Key = AesKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            var payload = Encoding.UTF8.GetString(plain);

            // 格式: liveId|userId|role|expiresAt
            var parts = payload.Split('|');
            if (parts.Length != 4) return false;

            liveId = Guid.Parse(parts[0]);
            userId = Guid.Parse(parts[1]);
            role = parts[2];
            expiresAt = long.Parse(parts[3]);

            return true;
        }
        catch
        {
            return false;
        }
    }
}
