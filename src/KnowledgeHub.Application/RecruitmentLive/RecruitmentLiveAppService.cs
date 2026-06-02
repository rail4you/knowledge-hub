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
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ICurrentUser _currentUser;
    private static readonly char[] RoomCodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray(); // 去掉易混淆的 0/O/1/I

    public RecruitmentLiveAppService(
        IRepository<RecruitmentLiveEntity, Guid> liveRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IConfiguration configuration,
        ICurrentUser currentUser)
    {
        _liveRepository = liveRepository;
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
    public async Task<RecruitmentLiveDto> CreateLiveAsync(CreateRecruitmentLiveDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("直播标题不能为空。");
        }

        var currentUserId = _currentUser.GetId();
        var currentUser = await _userRepository.GetAsync(currentUserId);
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
        };

        if (input.StudentId.HasValue)
        {
            var student = await _userRepository.GetAsync(input.StudentId.Value);
            entity.AssignStudent(student.Id, student.Name ?? student.UserName ?? "未知");
        }

        await _liveRepository.InsertAsync(entity, autoSave: true);
        return MapToDto(entity);
    }

    [Authorize(KnowledgeHubPermissions.RecruitmentLive.Create)]
    public async Task<RecruitmentLiveDto> UpdateLiveAsync(Guid id, UpdateRecruitmentLiveDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("直播标题不能为空。");
        }

        var entity = await GetOwnedLiveAsync(id);

        if (entity.Status == RecruitmentLiveStatus.Active)
        {
            throw new UserFriendlyException("正在进行的直播不能编辑。");
        }

        entity.Title = input.Title.Trim();
        entity.Description = input.Description?.Trim();
        entity.ScheduledAt = input.ScheduledAt?.ToUniversalTime();

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

    [Authorize(KnowledgeHubPermissions.RecruitmentLive.Create)]
    public async Task DeleteLiveAsync(Guid id)
    {
        var entity = await GetOwnedLiveAsync(id);
        await _liveRepository.DeleteAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.RecruitmentLive.Create)]
    public async Task<WsTokenDto> GetWebSocketTokenAsync(Guid liveId)
    {
        var entity = await GetOwnedLiveAsync(liveId);

        if (entity.Status == RecruitmentLiveStatus.Ended || entity.Status == RecruitmentLiveStatus.Cancelled)
        {
            throw new UserFriendlyException("该直播已结束或已取消。");
        }

        var token = GenerateWsToken(liveId, _currentUser.GetId(), "teacher");
        var wsBase = _configuration["App:SelfUrl"] ?? "https://localhost:44305";
        var wsUrl = wsBase.Replace("https://", "wss://").Replace("http://", "ws://").TrimEnd('/');

        return new WsTokenDto
        {
            Token = token,
            WsUrl = $"{wsUrl}/api/recruitment-live/ws"
        };
    }

    // ── 学生端 ──

    [Authorize(KnowledgeHubPermissions.RecruitmentLive.Default)]
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

            var currentUserId = _currentUser.GetId();
            var dto = MapToDto(entity);
            dto.IsParticipant = entity.TeacherId == currentUserId || entity.StudentId == currentUserId;
            return dto;
        }
    }

    [Authorize(KnowledgeHubPermissions.RecruitmentLive.Create)]
    public async Task<List<UserBriefDto>> GetTenantStudentsAsync(string? filter)
    {
        var users = await _userRepository.GetListAsync();
        var studentRoleName = "Student";

        var query = users.AsEnumerable();
        
        // 过滤当前租户的用户（通过 TenantId 属性）
        var currentTenantId = CurrentTenant.Id;
        query = query.Where(u => u.TenantId == currentTenantId);

        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim();
            query = query.Where(u =>
                (u.Name != null && u.Name.Contains(f, StringComparison.OrdinalIgnoreCase)) ||
                (u.UserName != null && u.UserName.Contains(f, StringComparison.OrdinalIgnoreCase)));
        }

        return query
            .OrderBy(u => u.Name)
            .Take(50)
            .Select(u => new UserBriefDto
            {
                Id = u.Id,
                UserName = u.UserName ?? string.Empty,
                Name = u.Name ?? u.UserName ?? string.Empty
            })
            .ToList();
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
        return Task.FromResult(servers);
    }

    // ── 私有方法 ──

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
        var expirationSeconds = _configuration.GetValue<int>("RecruitmentLive:WsTokenExpirationSeconds", 30);
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
