using System;
using System.Collections.Concurrent;
using System.Linq;
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
using Volo.Abp.Users;
using KnowledgeHub.Permissions;
using Volo.Abp.Identity;

namespace KnowledgeHub.LiveWs;

/// <summary>
/// 招聘直播 WebSocket 信令处理器 — 多人版。
/// 支持一个教师 + 多个学生的 mesh 拓扑视频通信。
/// </summary>
public class RecruitmentLiveWebSocketHandler
{
    private static readonly ConcurrentDictionary<string, LiveRoom> Rooms = new();
    private const int MaxMessageSize = 32768; // 32KB
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
        using var scope = _serviceProvider.CreateScope();
        var dataFilter = scope.ServiceProvider.GetRequiredService<IDataFilter>();
        var liveRepo = scope.ServiceProvider.GetRequiredService<IRepository<RecruitmentLiveEntity, Guid>>();
        var participantRepo = scope.ServiceProvider.GetRequiredService<IRepository<RecruitmentLiveParticipant, Guid>>();

        using var disableMultiTenant = dataFilter.Disable<IMultiTenant>();

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

        // 验证用户是否为参与者
        var participants = await participantRepo.GetListAsync(p => p.LiveId == liveId);
        var participant = participants.FirstOrDefault(p => p.UserId == tokenUserId);
        var isParticipant = participant != null;

        if (!isParticipant)
        {
            var userMgr = scope.ServiceProvider.GetRequiredService<IdentityUserManager>();
            var user = await userMgr.FindByIdAsync(tokenUserId.ToString());
            var isAdmin = user != null && await userMgr.IsInRoleAsync(user, "admin");

            if (!isAdmin)
            {
                _logger.LogWarning("用户 {UserId} 不是直播 {LiveId} 的参与者", tokenUserId, liveId);
                await SendErrorAndClose(ws, "您不是该直播的参与者");
                return;
            }

            _logger.LogInformation("管理员 {UserId} 以教师身份代管直播 {LiveId}", tokenUserId, liveId);
            participant = new RecruitmentLiveParticipant(Guid.NewGuid(), liveId, tokenUserId, user?.Name ?? "管理员", "teacher");
        }

        var userName = participant.UserName;
        var role = participant.Role;

        _logger.LogInformation("ws handler 校验通过: userId={UserId}, role={Role}, liveId={LiveId}",
            tokenUserId, role, liveId);

        // 4. 加入房间
        var roomKey = liveId.ToString();
        var room = Rooms.GetOrAdd(roomKey, _ => new LiveRoom(liveId));

        var userIdStr = tokenUserId.ToString();

        // 踢掉同一用户的旧连接
        var existing = room.GetByUserId(userIdStr);
        if (existing?.Ws is { State: WebSocketState.Open } oldWs && oldWs != ws)
        {
            await TryCloseWebSocket(oldWs, "您已在其他设备进入直播间");
        }
        room.AddParticipant(userIdStr, ws, role, userName);

        _logger.LogInformation("用户 {UserId}({Role}) 进入直播间 {LiveId}", userIdStr, role, liveId);

        // 5. 发送参与者列表给新加入的人
        var allParticipants = room.GetAllParticipants();
        var participantListData = allParticipants.Select(p => new
        {
            userId = p.UserId,
            userName = p.UserName,
            role = p.Role,
            you = p.UserId == userIdStr,
        }).ToList();

        await SendJson(ws, new { type = "participant-list", participants = participantListData });

        // 6. 广播 user-joined 给房间内的其他人（带用户名）
        var others = room.GetOthers(ws);
        foreach (var other in others)
        {
            if (other.Ws is { State: WebSocketState.Open })
            {
                await SendJson(other.Ws, new { type = "user-joined", userId = userIdStr, userName, role });
            }
        }

        // 7. 如果直播状态是 Waiting，更新为 Active
        if (live.Status == RecruitmentLiveStatus.Waiting && room.HasAnyone)
        {
            live.Start();
            await liveRepo.UpdateAsync(live, autoSave: true);
        }

        // 8. 消息循环
        await MessageLoop(ws, room, userIdStr, role, userName, liveId, liveRepo);
    }

    private async Task MessageLoop(
        System.Net.WebSockets.WebSocket ws,
        LiveRoom room,
        string userId,
        string role,
        string userName,
        Guid liveId,
        IRepository<RecruitmentLiveEntity, Guid> liveRepo)
    {
        var buffer = new byte[MaxMessageSize];
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

                if (result.MessageType == WebSocketMessageType.Close) break;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleMessage(ws, room, userId, role, userName, message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "直播间 {LiveId} 消息循环异常", liveId);
        }
        finally
        {
            await HandleDisconnect(ws, room, userId, role, liveId, liveRepo);
        }
    }

    private async Task HandleMessage(
        System.Net.WebSockets.WebSocket ws,
        LiveRoom room,
        string userId,
        string role,
        string userName,
        string message)
    {
        JsonElement? json;
        try
        {
            json = JsonSerializer.Deserialize<JsonElement>(message, JsonOptions);
        }
        catch
        {
            return;
        }

        var type = json?.TryGetProperty("type", out var t) == true ? t.GetString() : null;

        switch (type)
        {
            case "ping":
                await SendJson(ws, new { type = "pong" });
                break;

            case "pong":
                break;

            case "offer":
            case "answer":
            case "ice-candidate":
                // 转发给指定目标用户
                var targetUserId = json?.TryGetProperty("targetUserId", out var target) == true
                    ? target.GetString()
                    : null;
                if (!string.IsNullOrEmpty(targetUserId))
                {
                    var targetParticipant = room.GetByUserId(targetUserId);
                    if (targetParticipant?.Ws is { State: WebSocketState.Open } targetWs)
                    {
                        var data = json?.TryGetProperty("data", out var d) == true ? (object?)d : null;
                        _logger.LogInformation("转发 {Type} from {UserId} to {TargetId}", type, userId, targetUserId);
                        await SendJson(targetWs, new { type, data, fromUserId = userId, fromRole = role, fromUserName = userName });
                    }
                    else
                    {
                        _logger.LogWarning("无法转发 {Type}: 目标用户 {TargetId} 已断开", type, targetUserId);
                    }
                }
                else
                {
                    _logger.LogWarning("{Type} 消息缺少 targetUserId", type);
                }
                break;

            case "chat":
                var text = json?.TryGetProperty("data", out var cd) == true ? cd.GetString() : null;
                if (!string.IsNullOrWhiteSpace(text) && text.Length <= 500)
                {
                    // 广播给房间内所有其他人
                    var others = room.GetOthers(ws);
                    foreach (var other in others)
                    {
                        if (other.Ws is { State: WebSocketState.Open })
                        {
                            await SendJson(other.Ws, new { type = "chat", data = text, from = role, fromUserId = userId, fromUserName = userName });
                        }
                    }
                    // Echo 给自己（标记 self=true 让发送方知道自己已发送）
                    await SendJson(ws, new { type = "chat", data = text, from = role, fromUserId = userId, fromUserName = userName, self = true });
                }
                break;

            case "hang-up":
                // 不通知他人，只是自己离开
                break;

            default:
                _logger.LogDebug("直播间未知消息类型: {Type}", type);
                break;
        }
    }

    private async Task HandleDisconnect(
        System.Net.WebSockets.WebSocket ws,
        LiveRoom room,
        string userId,
        string role,
        Guid liveId,
        IRepository<RecruitmentLiveEntity, Guid> liveRepo)
    {
        _logger.LogInformation("用户 {UserId}({Role}) 离开直播间 {LiveId}", userId, role, liveId);

        // 广播 user-left
        var others = room.GetOthers(ws);
        foreach (var other in others)
        {
            if (other.Ws is { State: WebSocketState.Open })
            {
                await SendJson(other.Ws, new { type = "user-left", userId, role, reason = "对方已断开连接" });
            }
        }

        // 清理房间引用
        room.RemoveParticipant(userId);

        // 如果房间空了，清理房间缓存
        if (!room.HasAnyone)
        {
            _logger.LogInformation("直播间 {LiveId} 所有人已离开，清理房间缓存", liveId);
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
            if (data.Length < 16 + 4) return false;

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