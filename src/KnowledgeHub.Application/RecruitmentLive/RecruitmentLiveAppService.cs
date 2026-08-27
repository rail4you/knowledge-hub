using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.RecruitmentLive;
using KnowledgeHub.RecruitmentLive.Dtos;
using Microsoft.AspNetCore.Authorization;
using RecruitmentLiveEntity = KnowledgeHub.RecruitmentLive.RecruitmentLive;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.RecruitmentLiveService;

public class RecruitmentLiveAppService : KnowledgeHubAppService, IRecruitmentLiveAppService
{
    private readonly IRepository<RecruitmentLiveEntity, Guid> _liveRepository;
    private readonly IRepository<RecruitmentLiveChatMessage, Guid> _chatMessageRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ICurrentUser _currentUser;
    private static readonly char[] RoomCodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray(); // 去掉易混淆的 0/O/1/I

    public RecruitmentLiveAppService(
        IRepository<RecruitmentLiveEntity, Guid> liveRepository,
        IRepository<RecruitmentLiveChatMessage, Guid> chatMessageRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IConfiguration configuration,
        ICurrentUser currentUser)
    {
        _liveRepository = liveRepository;
        _chatMessageRepository = chatMessageRepository;
        _userRepository = userRepository;
        _configuration = configuration;
        _currentUser = currentUser;
    }

    // ── 教师端 ──

    [Authorize(KnowledgeHubPermissions.RecruitmentLive.Create)]
    public async Task<PagedResultDto<RecruitmentLiveDto>> GetTeacherLivesAsync(PagedRecruitmentLiveRequestDto input)
    {
        var currentUserId = _currentUser.GetId();
        var query = await _liveRepository.GetQueryableAsync();

        query = query.Where(x => x.TeacherId == currentUserId);

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            query = query.Where(x => x.Title.Contains(input.Filter) || (x.StudentName != null && x.StudentName.Contains(input.Filter)));
        }
        if (input.Status.HasValue)
        {
            query = query.Where(x => x.Status == input.Status.Value);
        }

        var totalCount = await query.LongCountAsync();
        var items = await query
            .OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        return new PagedResultDto<RecruitmentLiveDto>(totalCount, items.Select(MapToDto).ToList());
    }

    [Authorize(KnowledgeHubPermissions.RecruitmentLive.Create)]
    public async Task<List<RecruitmentLiveDto>> CreateLiveAsync(CreateRecruitmentLiveDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("直播标题不能为空。");
        }

        ValidateScheduleRange(input.ScheduledAt, input.ScheduledEndAt);

        var currentUserId = _currentUser.GetId();
        var currentUser = await _userRepository.GetAsync(currentUserId);

        var studentIds = input.StudentIds?.Distinct().ToList() ?? new List<Guid>();
        // 至少需要一个参与者（教师自己），如果没有学生则创建空直播
        if (studentIds.Count == 0)
        {
            var roomCode = await GenerateUniqueRoomCodeAsync();
            var entity = new RecruitmentLiveEntity(
                GuidGenerator.Create(),
                input.Title.Trim(),
                currentUserId,
                currentUser.Name ?? currentUser.UserName ?? "未知",
                roomCode)
            {
                TenantId = CurrentTenant.Id,
                Description = input.Description?.Trim(),
                ScheduledAt = input.ScheduledAt?.ToUniversalTime(),
                ScheduledEndAt = input.ScheduledEndAt?.ToUniversalTime(),
            };
            await _liveRepository.InsertAsync(entity, autoSave: true);
            return new List<RecruitmentLiveDto> { MapToDto(entity) };
        }

        // 每个学生创建一个独立的直播间
        var results = new List<RecruitmentLiveDto>();
        foreach (var studentId in studentIds)
        {
            var student = await _userRepository.GetAsync(studentId);
            var roomCode = await GenerateUniqueRoomCodeAsync();
            var entity = new RecruitmentLiveEntity(
                GuidGenerator.Create(),
                input.Title.Trim(),
                currentUserId,
                currentUser.Name ?? currentUser.UserName ?? "未知",
                roomCode)
            {
                TenantId = CurrentTenant.Id,
                Description = input.Description?.Trim(),
                ScheduledAt = input.ScheduledAt?.ToUniversalTime(),
                ScheduledEndAt = input.ScheduledEndAt?.ToUniversalTime(),
            };
            entity.AssignStudent(student.Id, student.Name ?? student.UserName ?? "未知");
            await _liveRepository.InsertAsync(entity, autoSave: true);
            results.Add(MapToDto(entity));
        }
        return results;
    }

    [Authorize(KnowledgeHubPermissions.RecruitmentLive.Create)]
    public async Task<RecruitmentLiveDto> UpdateLiveAsync(Guid id, UpdateRecruitmentLiveDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("直播标题不能为空。");
        }

        ValidateScheduleRange(input.ScheduledAt, input.ScheduledEndAt);

        var entity = await GetOwnedLiveAsync(id);

        if (entity.Status == RecruitmentLiveStatus.Active)
        {
            throw new UserFriendlyException("正在进行的直播不能编辑。");
        }

        entity.Title = input.Title.Trim();
        entity.Description = input.Description?.Trim();
        entity.ScheduledAt = input.ScheduledAt?.ToUniversalTime();
        entity.ScheduledEndAt = input.ScheduledEndAt?.ToUniversalTime();

        if (input.StudentId.HasValue && input.StudentId.Value != entity.StudentId)
        {
            var student = await _userRepository.GetAsync(input.StudentId.Value);
            entity.AssignStudent(student.Id, student.Name ?? student.UserName ?? "未知");
        }

        await _liveRepository.UpdateAsync(entity, autoSave: true);
        return MapToDto(entity);
    }

    [Authorize(KnowledgeHubPermissions.RecruitmentLive.Create)]
    public async Task CancelLiveAsync(Guid id)
    {
        var entity = await GetOwnedLiveAsync(id);
        entity.Cancel();
        await _liveRepository.UpdateAsync(entity, autoSave: true);
    }

    /// <summary>
    /// 结束直播（仅教师/管理员可调用，学生退出不结束直播）。
    /// 进行中→已结束，等待中→已取消，已结束/已取消幂等返回。
    /// </summary>
    [Authorize]
    public async Task EndLiveAsync(Guid id)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var entity = await _liveRepository.GetAsync(id);
            var currentUserId = _currentUser.GetId();

            // 只有教师（或管理员）可以结束直播；学生退出不结束直播
            var isTeacherOrAdmin = entity.TeacherId == currentUserId
                || await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.RecruitmentLive.Manage);
            if (!isTeacherOrAdmin)
            {
                throw new UserFriendlyException("只有教师可以结束直播。");
            }

            if (entity.Status == RecruitmentLiveStatus.Active)
            {
                entity.End(); // Active → Ended
            }
            else if (entity.Status == RecruitmentLiveStatus.Waiting)
            {
                entity.Cancel(); // Waiting → Cancelled
            }
            // 已结束/已取消：幂等，不重复修改

            await _liveRepository.UpdateAsync(entity, autoSave: true);
        }
    }

    [Authorize(KnowledgeHubPermissions.RecruitmentLive.Create)]
    public async Task DeleteLiveAsync(Guid id)
    {
        var entity = await GetOwnedLiveAsync(id);
        await _liveRepository.DeleteAsync(entity);
    }

    [Authorize]
    public async Task<WsTokenDto> GetWebSocketTokenAsync(Guid liveId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var entity = await _liveRepository.GetAsync(liveId);
            var currentUserId = _currentUser.GetId();

            // 验证当前用户是否为该直播的参与者
            var isTeacher = entity.TeacherId == currentUserId;
            var isStudent = entity.StudentId == currentUserId;

            if (!isTeacher && !isStudent)
            {
                var canManage = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.RecruitmentLive.Manage);
                if (!canManage)
                {
                    throw new UserFriendlyException("您不是该直播的参与者。");
                }
                isTeacher = true; // 管理员当教师处理
            }

            if (entity.Status == RecruitmentLiveStatus.Ended || entity.Status == RecruitmentLiveStatus.Cancelled)
            {
                throw new UserFriendlyException("该直播已结束或已取消。");
            }

            var role = isTeacher ? "teacher" : "student";
            var token = GenerateWsToken(liveId, currentUserId, role);
            var wsBase = _configuration["App:SelfUrl"] ?? "https://localhost:44305";
            var wsUrl = wsBase.Replace("https://", "wss://").Replace("http://", "ws://").TrimEnd('/');

            return new WsTokenDto
            {
                Token = token,
                WsUrl = $"{wsUrl}/api/recruitment-live/ws"
            };
        }
    }

    // ── 学生端 ──

    [Authorize]
    public async Task<PagedResultDto<RecruitmentLiveDto>> GetStudentLivesAsync(PagedRecruitmentLiveRequestDto input)
    {
        var currentUserId = _currentUser.GetId();

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _liveRepository.GetQueryableAsync();
            query = query.Where(x => x.StudentId == currentUserId);

            if (!string.IsNullOrWhiteSpace(input.Filter))
            {
                query = query.Where(x => x.Title.Contains(input.Filter));
            }
            if (input.Status.HasValue)
            {
                query = query.Where(x => x.Status == input.Status.Value);
            }

            var totalCount = await query.LongCountAsync();
            var items = await query
                .OrderByDescending(x => x.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();

            return new PagedResultDto<RecruitmentLiveDto>(totalCount, items.Select(MapToDto).ToList());
        }
    }

    // ── 通用 ──

    [Authorize]
    public async Task<RecruitmentLiveDto> GetLiveAsync(Guid id)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var entity = await _liveRepository.GetAsync(id);

            // 检查是否已过期（设置了计划结束时间且已过计划结束时间的直播不允许进入；
            // 未设置计划结束时间或尚未到计划结束时间（时间范围内）的直播不会过期）
            if (entity.ScheduledEndAt.HasValue && entity.ScheduledEndAt.Value < DateTime.UtcNow
                && entity.Status != RecruitmentLiveStatus.Ended && entity.Status != RecruitmentLiveStatus.Cancelled)
            {
                throw new UserFriendlyException("该直播已过期，无法进入。");
            }

            var currentUserId = _currentUser.GetId();
            var dto = MapToDto(entity);
            dto.IsParticipant = entity.TeacherId == currentUserId 
                || entity.StudentId == currentUserId 
                || await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.RecruitmentLive.Manage);
            return dto;
        }
    }

    [Authorize(KnowledgeHubPermissions.RecruitmentLive.Create)]
    public async Task<List<UserBriefDto>> GetTenantStudentsAsync(string? filter)
    {
        const string studentRoleName = "Student";
        var currentTenantId = CurrentTenant.Id;

        var query = await _userRepository.GetQueryableAsync();
        query = query.Where(u => u.TenantId == currentTenantId);

        // 通过 AbpUserRoles / AbpRoles 子查询过滤 Student 角色（避免 N+1 GetRolesAsync）
        var dbContext = await _userRepository.GetDbContextAsync();
        var userRoles = dbContext.Set<IdentityUserRole>();
        var roles = dbContext.Set<IdentityRole>();

        // AbpRoles 和 AbpUserRoles 都受多租户过滤。当租户用户分配的
        // 是 host 级别的 Student 角色时，AbpUserRoles.TenantId = NULL，
        // 但多租户过滤器会加 WHERE TenantId = 当前租户 ID，导致不匹配。
        // 需要临时禁用多租户过滤来查询角色和用户关联。
        List<Guid> studentRoleIds;
        List<Guid> studentUserIds;
        using (DataFilter.Disable<IMultiTenant>())
        {
            // 收集所有可用的 Student 角色：先查当前租户的，再添加 host 级别的。
            // 租户可能有自己的 Student 角色但无人被分配，需要同时检查 host Student 角色。
            studentRoleIds = await roles
                .Where(r => r.Name == studentRoleName && r.TenantId == currentTenantId)
                .Select(r => r.Id)
                .ToListAsync();

            // 额外加上 host 级别的 Student 角色（host 角色可被租户用户共享）
            var hostStudentRoleIds = await roles
                .Where(r => r.Name == studentRoleName && r.TenantId == null)
                .Select(r => r.Id)
                .ToListAsync();
            studentRoleIds.AddRange(hostStudentRoleIds);

            if (studentRoleIds.Count == 0)
            {
                return [];
            }

            // AbpUserRoles 也有多租户过滤，在同一 using 块内查询
            studentUserIds = await userRoles
                .Where(ur => studentRoleIds.Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .ToListAsync();
        }
        query = query.Where(u => studentUserIds.Contains(u.Id));

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim();
            query = query.Where(u =>
                (u.Name != null && u.Name.Contains(f)) ||
                (u.UserName != null && u.UserName.Contains(f)));
        }

        return await query
            .OrderBy(u => u.Name)
            .Take(50)
            .Select(u => new UserBriefDto
            {
                Id = u.Id,
                UserName = u.UserName ?? string.Empty,
                Name = !string.IsNullOrWhiteSpace(u.Name) ? u.Name : (u.UserName ?? string.Empty)
            })
            .ToListAsync();
    }

    [Authorize]
    public async Task SaveChatMessageAsync(Guid liveId, SaveChatMessageInputDto input)
    {
        var content = input.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length > 500) return;

        var userId = _currentUser.GetId();
        RecruitmentLiveEntity live;
        using (DataFilter.Disable<IMultiTenant>())
        {
            live = await _liveRepository.GetAsync(liveId);
        }
        var role = live.TeacherId == userId ? "teacher" : "student";

        var msg = new RecruitmentLiveChatMessage(
            GuidGenerator.Create(), liveId, role, userId, content);
        await _chatMessageRepository.InsertAsync(msg);
    }

    [Authorize]
    public async Task<List<RecruitmentLiveChatMessageDto>> GetChatMessagesAsync(Guid liveId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var msgs = await _chatMessageRepository.GetListAsync(
                x => x.LiveId == liveId);
            return msgs
                .OrderBy(x => x.SentAt)
                .Select(x => new RecruitmentLiveChatMessageDto
                {
                    SenderRole = x.SenderRole,
                    Content = x.Content,
                    SentAt = x.SentAt,
                })
                .ToList();
        }
    }

    [AllowAnonymous]
    public Task<List<IceServerDto>> GetIceServersAsync()
    {
        var servers = _configuration.GetSection("RecruitmentLive:IceServers").Get<List<IceServerDto>>() ?? [];
        if (servers.Count == 0)
        {
            // 默认 Google STUN
            servers.Add(new IceServerDto { Urls = ["stun:stun.l.google.com:19302"] });
        }

        // TURN 中继（coturn）：解决双方 NAT 不对称/严格 NAT 下 P2P 无法穿透的问题。
        // 使用 coturn 的 time-limited credential 机制：
        //   username = "{过期Unix时间戳}:{随机串}"
        //   credential = base64(HMAC-SHA1(secret, username))
        // coturn 端配置 static-auth-secret = 同一密钥即可校验，无需把固定密码发给浏览器。
        var turnUrl = _configuration["RecruitmentLive:Turn:Url"];
        var turnSecret = _configuration["RecruitmentLive:Turn:Secret"];
        if (!string.IsNullOrWhiteSpace(turnUrl) && !string.IsNullOrWhiteSpace(turnSecret))
        {
            var validitySeconds = _configuration.GetValue<int>("RecruitmentLive:Turn:ValiditySeconds", 6 * 3600);
            var expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + validitySeconds;
            var username = $"{expires}:{Guid.NewGuid():N}";
            using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(turnSecret));
            var credential = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(username)));

            var urls = new List<string> { turnUrl };
            var turnUrlTcp = _configuration["RecruitmentLive:Turn:UrlTcp"];
            if (!string.IsNullOrWhiteSpace(turnUrlTcp))
            {
                urls.Add(turnUrlTcp);
            }

            servers.Add(new IceServerDto
            {
                Urls = urls,
                Username = username,
                Credential = credential
            });
        }

        return Task.FromResult(servers);
    }

    // ── 私有方法 ──

    private static void ValidateScheduleRange(DateTime? start, DateTime? end)
    {
        if (start.HasValue && end.HasValue && end.Value < start.Value)
        {
            throw new UserFriendlyException("计划结束时间不能早于计划开始时间。");
        }
    }

    private async Task<RecruitmentLiveEntity> GetOwnedLiveAsync(Guid id)
    {
        var entity = await _liveRepository.GetAsync(id);
        var currentUserId = _currentUser.GetId();

        if (entity.TeacherId != currentUserId)
        {
            // 管理员也能操作
            var canManage = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.RecruitmentLive.Manage);
            if (!canManage)
            {
                throw new UserFriendlyException("您没有权限操作该直播。");
            }
        }

        return entity;
    }

    private async Task<string> GenerateUniqueRoomCodeAsync()
    {
        const int maxAttempts = 20;
        for (var i = 0; i < maxAttempts; i++)
        {
            var code = GenerateRoomCode();
            var exists = await _liveRepository.AnyAsync(x => x.RoomCode == code);
            if (!exists)
            {
                return code;
            }
        }
        throw new BusinessException("RecruitmentLive:RoomCodeGenerationFailed", "无法生成唯一房间码，请重试。");
    }

    private static string GenerateRoomCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(6);
        var sb = new StringBuilder(6);
        for (var i = 0; i < 6; i++)
        {
            sb.Append(RoomCodeChars[bytes[i] % RoomCodeChars.Length]);
        }
        return sb.ToString();
    }

    private string GenerateWsToken(Guid liveId, Guid userId, string role)
    {
        var expirationSeconds = _configuration.GetValue<int>("RecruitmentLive:WsTokenExpirationSeconds", 300);
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expirationSeconds).ToUnixTimeSeconds();
        var payload = $"{liveId}|{userId}|{role}|{expiresAt}";

        // AES 加密
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes("KnowledgeHub-RecruitmentLive-WS-2026"));
        using var aes = Aes.Create();
        aes.Key = keyBytes;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        var plainBytes = Encoding.UTF8.GetBytes(payload);
        using var encryptor = aes.CreateEncryptor();
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // IV + cipher → Base64
        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    private RecruitmentLiveDto MapToDto(RecruitmentLiveEntity entity)
    {
        return new RecruitmentLiveDto
        {
            Id = entity.Id,
            CreationTime = entity.CreationTime,
            LastModificationTime = entity.LastModificationTime,
            Title = entity.Title,
            Description = entity.Description,
            TeacherId = entity.TeacherId,
            TeacherName = entity.TeacherName,
            StudentId = entity.StudentId,
            StudentName = entity.StudentName,
            RoomCode = entity.RoomCode,
            Status = entity.Status,
            StatusText = GetStatusText(entity.Status),
            ScheduledAt = entity.ScheduledAt,
            ScheduledEndAt = entity.ScheduledEndAt,
            StartedAt = entity.StartedAt,
            EndedAt = entity.EndedAt,
            DurationSeconds = entity.GetDurationSeconds(),
            InterviewScheduleId = entity.InterviewScheduleId,
        };
    }

    private static string GetStatusText(RecruitmentLiveStatus status) => status switch
    {
        RecruitmentLiveStatus.Waiting => "等待中",
        RecruitmentLiveStatus.Active => "进行中",
        RecruitmentLiveStatus.Ended => "已结束",
        RecruitmentLiveStatus.Cancelled => "已取消",
        _ => "未知"
    };
}
