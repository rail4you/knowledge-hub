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
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using KnowledgeHub.Permissions;
using Volo.Abp.Identity;

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
        _logger.LogInformation("=== WebSocket 连接请求: Path={Path}, RemoteIp={Ip} ===",
            httpContext.Request.Path, httpContext.Connection.RemoteIpAddress);

        try
        {
            await HandleAsyncInternal(ws, httpContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HandleAsync 未处理异常");
            await TryCloseWebSocket(ws, "服务器内部错误");
        }
    }

    private async Task HandleAsyncInternal(System.Net.WebSockets.WebSocket ws, HttpContext httpContext)
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
        // 注意：WebSocket 请求不走 HTTP 认证管线，没有租户上下文，
        // 必须禁用多租户过滤器，否则 FindAsync 会被租户过滤掉导致返回 null
        using var scope = _serviceProvider.CreateScope();
        var dataFilter = scope.ServiceProvider.GetRequiredService<IDataFilter>();
        using var _ = dataFilter.Disable<IMultiTenant>();
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

        // 验证用户是否是参与者（或具有管理权限）
        var userIdStr = tokenUserId.ToString();
        var isParticipant = live.TeacherId == tokenUserId || live.StudentId == tokenUserId;

        if (!isParticipant)
        {
            // 非参与者：检查用户是否为管理员（拥有 Manage 权限的管理员可代管教师端）
            var userMgr = scope.ServiceProvider.GetRequiredService<IdentityUserManager>();
            var user = await userMgr.FindByIdAsync(userIdStr);
            var isAdmin = user != null && await userMgr.IsInRoleAsync(user, "admin");

            if (!isAdmin)
            {
                _logger.LogWarning("用户 {UserId} 不是直播 {LiveId} 的参与者 (teacherId={TeacherId}, studentId={StudentId})",
                    tokenUserId, liveId, live.TeacherId, live.StudentId);
                await SendErrorAndClose(ws, "您不是该直播的参与者");
                return;
            }

            _logger.LogInformation("管理员 {UserId} 以教师身份代管直播 {LiveId}", tokenUserId, liveId);
        }

        _logger.LogInformation("ws handler 校验通过: userId={UserId}, role={Role}, liveId={LiveId}",
            tokenUserId, live.TeacherId == tokenUserId || !isParticipant ? "teacher" : "student", liveId);

        var role = (live.TeacherId == tokenUserId || !isParticipant) ? "teacher" : "student";

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

        // 通知双方：新加入的人需要知道对方是否已在房间，先加入的人需要知道新人加入了
        var other = room.GetOther(ws);
        _logger.LogInformation("房间通知检查: role={Role}, otherWsState={OtherState}",
            role, other?.State.ToString() ?? "null");
        if (other is { State: System.Net.WebSockets.WebSocketState.Open })
        {
            // 通知先加入的人："新成员 {role} 已加入"
            _logger.LogInformation("→ 通知已有用户 user-joined (role={Role})", role);
            await SendJson(other, new { type = "user-joined", role });
            // 通知刚加入的人："房间里已有人，对方是 {otherRole}"
            var otherRole = role == "teacher" ? "student" : "teacher";
            _logger.LogInformation("→ 通知新用户 user-joined (role={Role})", otherRole);
            await SendJson(ws, new { type = "user-joined", role = otherRole });
        }
        else
        {
            _logger.LogInformation("对方尚未连接，不发送 user-joined");
        }

        // 如果直播状态是 Waiting，更新为 Active
        if (live.Status == RecruitmentLiveStatus.Waiting && room.HasTeacher && room.HasStudent)
        {
            live.Start();
            await liveRepo.UpdateAsync(live, autoSave: true);
        }

        // 5. 消息循环
        await MessageLoop(ws, room, role, liveId, liveRepo, tokenUserId);
    }

    private async Task MessageLoop(
        System.Net.WebSockets.WebSocket ws,
        LiveRoom room,
        string role,
        Guid liveId,
        IRepository<RecruitmentLiveEntity, Guid> liveRepo,
        Guid userId)
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
                    await HandleMessage(ws, room, role, message, liveId, userId);
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
        string message,
        Guid liveId,
        Guid userId)
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
                _logger.LogInformation("转发 {Type} from {Role}", type, role);
                if (other is { State: System.Net.WebSockets.WebSocketState.Open })
                {
                    var data = json?.TryGetProperty("data", out var d) == true ? (object?)d : null;
                    await SendJson(other, new { type, data });
                }
                else
                {
                    _logger.LogWarning("无法转发 {Type}: 对方已断开", type);
                }
                break;

            case "chat":
                var text = json?.TryGetProperty("data", out var cd) == true ? cd.GetString() : null;
                if (!string.IsNullOrWhiteSpace(text) && text.Length <= 500)
                {
                    // 持久化消息
                    await SaveChatMessageAsync(liveId, role, userId, text);

                    if (other is { State: System.Net.WebSockets.WebSocketState.Open })
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

        // 如果房间空了，清理房间缓存（不自动结束直播，允许反复进入退出）
        if (!room.HasAnyone)
        {
            _logger.LogInformation("直播间 {LiveId} 双方都已离开，清理房间缓存", liveId);
            Rooms.TryRemove(liveId.ToString(), out _);
        }

        await TryCloseWebSocket(ws, null);
    }

    // ── 工具方法 ──

    private async Task SendJson(System.Net.WebSockets.WebSocket ws, object obj)
    {
        if (ws.State != System.Net.WebSockets.WebSocketState.Open)
        {
            _logger.LogWarning("SendJson: WebSocket not open, state={State}", ws.State);
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(obj, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            _logger.LogDebug("SendJson: {Json}", json);
            await ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendJson: Failed to send");
        }
    }

    private async Task SendErrorAndClose(System.Net.WebSockets.WebSocket ws, string message)
    {
        _logger.LogWarning("SendErrorAndClose: {Message}", message);
        await SendJson(ws, new { type = "error", message });
        await TryCloseWebSocket(ws, message);
    }

    private async Task TryCloseWebSocket(System.Net.WebSockets.WebSocket ws, string? reason)
    {
        if (ws.State == System.Net.WebSockets.WebSocketState.Open)
        {
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "关闭 WebSocket 失败");
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

    private async Task SaveChatMessageAsync(Guid liveId, string role, Guid userId, string content)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
            using var uow = uowManager.Begin();
            var repo = scope.ServiceProvider.GetRequiredService<IRepository<RecruitmentLiveChatMessage, Guid>>();
            var msg = new RecruitmentLiveChatMessage(
                Guid.NewGuid(), liveId, role, userId, content);
            await repo.InsertAsync(msg);
            await uow.CompleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存聊天消息失败");
        }
    }
}
