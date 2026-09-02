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
    private readonly IRepository<RecruitmentLiveParticipant, Guid> _participantRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ICurrentUser _currentUser;
    private static readonly char[] RoomCodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    public RecruitmentLiveAppService(
        IRepository<RecruitmentLiveEntity, Guid> liveRepository,
        IRepository<RecruitmentLiveChatMessage, Guid> chatMessageRepository,
        IRepository<RecruitmentLiveParticipant, Guid> participantRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IConfiguration configuration,
        ICurrentUser currentUser)
    {
        _liveRepository = liveRepository;
        _chatMessageRepository = chatMessageRepository;
        _participantRepository = participantRepository;
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
            // 不支持直接在 teacher lives 查询中过滤学生姓名，简化只过滤标题
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

        var liveIds = items.Select(x => x.Id).ToList();
        var participants = await _participantRepository.GetListAsync(p => liveIds.Contains(p.LiveId));

        return new PagedResultDto<RecruitmentLiveDto>(totalCount, items.Select(e => MapToDto(e, participants.Where(p => p.LiveId == e.Id).ToList())).ToList());
    }

    [Authorize(KnowledgeHubPermissions.RecruitmentLive.Create)]
    public async Task<List<RecruitmentLiveDto>> CreateLiveAsync(CreateRecruitmentLiveDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("直播标题不能为空。");
        }

        ValidateScheduleRange(input.ScheduledAt, input.ScheduledEndAt);

        var studentIds = input.StudentIds?.Distinct().ToList() ?? new List<Guid>();
        var currentUserId = _currentUser.GetId();
        var currentUser = await _userRepository.GetAsync(currentUserId);

        var roomCode = await GenerateUniqueRoomCodeAsync();
        var liveId = GuidGenerator.Create();

        // 创建直播间（设置第一个学生为主学生兼容旧代码）
        var primaryStudentName = (string?)null;
        if (studentIds.Count > 0)
        {
            var firstStudent = await _userRepository.GetAsync(studentIds[0]);
            primaryStudentName = firstStudent.Name ?? firstStudent.UserName ?? "未知";
        }

        var entity = new RecruitmentLiveEntity(
            liveId,
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

        if (studentIds.Count > 0)
        {
            var firstStudent = await _userRepository.GetAsync(studentIds[0]);
            entity.AssignStudent(firstStudent.Id, firstStudent.Name ?? firstStudent.UserName ?? "未知");
        }

        await _liveRepository.InsertAsync(entity, autoSave: true);

        // 添加参与者记录
        var participants = new List<RecruitmentLiveParticipant>
        {
            new(GuidGenerator.Create(), liveId, currentUserId, currentUser.Name ?? currentUser.UserName ?? "未知", "teacher")
            {
                TenantId = CurrentTenant.Id,
            }
        };

        foreach (var studentId in studentIds)
        {
            var student = await _userRepository.GetAsync(studentId);
            participants.Add(new RecruitmentLiveParticipant(
                GuidGenerator.Create(), liveId, student.Id, student.Name ?? student.UserName ?? "未知", "student")
            {
                TenantId = CurrentTenant.Id,
            });
        }

        await _participantRepository.InsertManyAsync(participants, autoSave: true);

        return new List<RecruitmentLiveDto> { MapToDto(entity, participants) };
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
        var participants = await _participantRepository.GetListAsync(p => p.LiveId == id);
        return MapToDto(entity, participants);
    }

    [Authorize(KnowledgeHubPermissions.RecruitmentLive.Create)]
    public async Task CancelLiveAsync(Guid id)
    {
        var entity = await GetOwnedLiveAsync(id);
        entity.Cancel();
        await _liveRepository.UpdateAsync(entity, autoSave: true);
    }

    [Authorize]
    public async Task EndLiveAsync(Guid id)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var entity = await _liveRepository.GetAsync(id);
            var currentUserId = _currentUser.GetId();

            var isTeacherOrAdmin = entity.TeacherId == currentUserId
                || await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.RecruitmentLive.Manage);
            if (!isTeacherOrAdmin)
            {
                throw new UserFriendlyException("只有教师可以结束直播。");
            }

            if (entity.Status == RecruitmentLiveStatus.Active)
            {
                entity.End();
            }
            else if (entity.Status == RecruitmentLiveStatus.Waiting)
            {
                entity.Cancel();
            }

            await _liveRepository.UpdateAsync(entity, autoSave: true);
        }
    }

    [Authorize(KnowledgeHubPermissions.RecruitmentLive.Create)]
    public async Task DeleteLiveAsync(Guid id)
    {
        var entity = await GetOwnedLiveAsync(id);
        // 清理关联数据
        await _participantRepository.DeleteAsync(p => p.LiveId == id);
        await _chatMessageRepository.DeleteAsync(m => m.LiveId == id);
        await _liveRepository.DeleteAsync(entity);
    }

    [Authorize]
    public async Task<WsTokenDto> GetWebSocketTokenAsync(Guid liveId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var entity = await _liveRepository.GetAsync(liveId);
            var currentUserId = _currentUser.GetId();

            // 通过参与者表验证，兼容旧版（只有 TeacherId/StudentId 的旧记录）
            var allParticipants = await _participantRepository.GetListAsync(p => p.LiveId == liveId);
            var isParticipant = allParticipants.Any(p => p.UserId == currentUserId)
                || entity.TeacherId == currentUserId    // 兼容旧记录
                || entity.StudentId == currentUserId;    // 兼容旧记录
            var isTeacher = allParticipants.Any(p => p.UserId == currentUserId && p.Role == "teacher")
                || entity.TeacherId == currentUserId;   // 兼容旧记录

            if (!isParticipant)
            {
                var canManage = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.RecruitmentLive.Manage);
                if (!canManage)
                {
                    throw new UserFriendlyException("您不是该直播的参与者。");
                }
                isTeacher = true;
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
            // 通过参与者表查询分配给当前学生的直播，兼容旧记录（仅 StudentId）
            var participantQuery = await _participantRepository.GetQueryableAsync();
            var participantLiveIds = await participantQuery
                .Where(p => p.UserId == currentUserId && p.Role == "student")
                .Select(p => p.LiveId)
                .ToListAsync();

            var query = (await _liveRepository.GetQueryableAsync())
                .Where(x => participantLiveIds.Contains(x.Id) || x.StudentId == currentUserId);

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

            var allParticipants = await _participantRepository.GetListAsync(p => items.Select(i => i.Id).Contains(p.LiveId));

            return new PagedResultDto<RecruitmentLiveDto>(totalCount,
                items.Select(e => MapToDto(e, allParticipants.Where(p => p.LiveId == e.Id).ToList())).ToList());
        }
    }

    // ── 通用 ──

    [Authorize]
    public async Task<RecruitmentLiveDto> GetLiveAsync(Guid id)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var entity = await _liveRepository.GetAsync(id);

            if (entity.ScheduledEndAt.HasValue && entity.ScheduledEndAt.Value < DateTime.UtcNow
                && entity.Status != RecruitmentLiveStatus.Ended && entity.Status != RecruitmentLiveStatus.Cancelled)
            {
                throw new UserFriendlyException("该直播已过期，无法进入。");
            }

            var currentUserId = _currentUser.GetId();
            var participants = await _participantRepository.GetListAsync(p => p.LiveId == id);
            var dto = MapToDto(entity, participants);
            dto.IsParticipant = participants.Any(p => p.UserId == currentUserId)
                || entity.TeacherId == currentUserId     // 兼容旧记录
                || entity.StudentId == currentUserId     // 兼容旧记录
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

        var dbContext = await _userRepository.GetDbContextAsync();
        var userRoles = dbContext.Set<IdentityUserRole>();
        var roles = dbContext.Set<IdentityRole>();

        List<Guid> studentRoleIds;
        List<Guid> studentUserIds;
        using (DataFilter.Disable<IMultiTenant>())
        {
            studentRoleIds = await roles
                .Where(r => r.Name == studentRoleName && r.TenantId == currentTenantId)
                .Select(r => r.Id)
                .ToListAsync();

            var hostStudentRoleIds = await roles
                .Where(r => r.Name == studentRoleName && r.TenantId == null)
                .Select(r => r.Id)
                .ToListAsync();
            studentRoleIds.AddRange(hostStudentRoleIds);

            if (studentRoleIds.Count == 0) return [];

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
        var role = "student";
        using (DataFilter.Disable<IMultiTenant>())
        {
            var live = await _liveRepository.GetAsync(liveId);
            role = live.TeacherId == userId ? "teacher" : "student";

            // 尝试通过参与者表获取更准确的角色
            var participants = await _participantRepository.GetListAsync(p => p.LiveId == liveId);
            var participant = participants.FirstOrDefault(p => p.UserId == userId);
            if (participant != null) role = participant.Role;
        }

        var msg = new RecruitmentLiveChatMessage(
            GuidGenerator.Create(), liveId, role, userId, content);
        await _chatMessageRepository.InsertAsync(msg);
    }

    [Authorize]
    public async Task<List<RecruitmentLiveChatMessageDto>> GetChatMessagesAsync(Guid liveId)
    {
        using (DataFilter.Disable<IMultiTenant>())
        {
            var msgs = await _chatMessageRepository.GetListAsync(x => x.LiveId == liveId);
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
            servers.Add(new IceServerDto { Urls = ["stun:stun.l.google.com:19302"] });
        }

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
            if (!string.IsNullOrWhiteSpace(turnUrlTcp)) urls.Add(turnUrlTcp);

            servers.Add(new IceServerDto { Urls = urls, Username = username, Credential = credential });
        }

        return Task.FromResult(servers);
    }

    // ── 私有方法 ──

    private static void ValidateScheduleRange(DateTime? start, DateTime? end)
    {
        if (start.HasValue && end.HasValue && end.Value < start.Value)
            throw new UserFriendlyException("计划结束时间不能早于计划开始时间。");
    }

    private async Task<RecruitmentLiveEntity> GetOwnedLiveAsync(Guid id)
    {
        var entity = await _liveRepository.GetAsync(id);
        var currentUserId = _currentUser.GetId();

        if (entity.TeacherId != currentUserId)
        {
            var canManage = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.RecruitmentLive.Manage);
            if (!canManage)
                throw new UserFriendlyException("您没有权限操作该直播。");
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
            if (!exists) return code;
        }
        throw new BusinessException("RecruitmentLive:RoomCodeGenerationFailed", "无法生成唯一房间码，请重试。");
    }

    private static string GenerateRoomCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(6);
        var sb = new StringBuilder(6);
        for (var i = 0; i < 6; i++)
            sb.Append(RoomCodeChars[bytes[i] % RoomCodeChars.Length]);
        return sb.ToString();
    }

    private string GenerateWsToken(Guid liveId, Guid userId, string role)
    {
        var expirationSeconds = _configuration.GetValue<int>("RecruitmentLive:WsTokenExpirationSeconds", 300);
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expirationSeconds).ToUnixTimeSeconds();
        var payload = $"{liveId}|{userId}|{role}|{expiresAt}";

        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes("KnowledgeHub-RecruitmentLive-WS-2026"));
        using var aes = Aes.Create();
        aes.Key = keyBytes;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.GenerateIV();

        var plainBytes = Encoding.UTF8.GetBytes(payload);
        using var encryptor = aes.CreateEncryptor();
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(result);
    }

    private RecruitmentLiveDto MapToDto(RecruitmentLiveEntity entity, List<RecruitmentLiveParticipant>? participants = null)
    {
        var dto = new RecruitmentLiveDto
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

        if (participants != null)
        {
            dto.Participants = participants
                .OrderBy(p => p.Role == "teacher" ? 0 : 1)
                .ThenBy(p => p.UserName)
                .Select(p => new ParticipantBriefDto
                {
                    UserId = p.UserId,
                    UserName = p.UserName,
                    Role = p.Role,
                })
                .ToList();
        }

        return dto;
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