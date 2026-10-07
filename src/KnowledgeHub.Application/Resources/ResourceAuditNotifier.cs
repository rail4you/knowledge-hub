using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Emailing;
using KnowledgeHub.EntityFrameworkCore;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace KnowledgeHub.Resources;

/// <summary>
/// 资源审核邮件通知（全部 try/catch，绝不阻断审核主流程）：
/// 1. 提交审核 → 提醒资源所属租户内持有 Resources.SchoolAudit 权限的用户；
/// 2. 校审/联盟终审 通过或驳回 → 通知资源创建者。
/// SMTP 配置始终在 resource.TenantId 租户上下文中解析（联盟审核员在 host 操作时
/// 也使用创建者所在租户的发信配置）。
/// </summary>
public interface IResourceAuditNotifier
{
    /// <summary>资源提交审核后，通知本租户的校审权限用户。</summary>
    Task NotifySchoolReviewersAsync(Resource resource);

    /// <summary>校审/联盟终审结果（通过或驳回）通知创建者。</summary>
    /// <param name="stageLabel">阶段名，如 "校审"、"联盟终审"</param>
    Task NotifyCreatorAuditResultAsync(Resource resource, bool approved, string? comment, string stageLabel);
}

public class ResourceAuditNotifier : IResourceAuditNotifier, ITransientDependency
{
    private readonly ISmtpMailSender _mailSender;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<PermissionGrant, Guid> _permissionGrantRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly KnowledgeHubDbContext _dbContext;
    private readonly IDataFilter _dataFilter;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<ResourceAuditNotifier> _logger;

    public ResourceAuditNotifier(
        ISmtpMailSender mailSender,
        ICurrentTenant currentTenant,
        IRepository<PermissionGrant, Guid> permissionGrantRepository,
        IRepository<IdentityUser, Guid> userRepository,
        KnowledgeHubDbContext dbContext,
        IDataFilter dataFilter,
        IConfiguration configuration,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<ResourceAuditNotifier> logger)
    {
        _mailSender = mailSender;
        _currentTenant = currentTenant;
        _permissionGrantRepository = permissionGrantRepository;
        _userRepository = userRepository;
        _dbContext = dbContext;
        _dataFilter = dataFilter;
        _configuration = configuration;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    /// <summary>
    /// 有活跃 UoW 时把发信推迟到事务提交之后（OnCompleted 仅在 SaveChanges 成功后执行），
    /// 避免请求最终失败（如 409 并发冲突）却已经把邮件发出去、重试时重复投递。
    /// 无 UoW（后台任务等）则立即发送。
    /// </summary>
    private void SendAfterCommit(Func<Task> send)
    {
        var uow = _unitOfWorkManager.Current;
        if (uow == null)
        {
            _ = send();
            return;
        }
        uow.OnCompleted(async () =>
        {
            try
            {
                await send();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "提交后发送审核通知邮件失败");
            }
        });
    }

    public async Task NotifySchoolReviewersAsync(Resource resource)
    {
        try
        {
            using (_currentTenant.Change(resource.TenantId))
            {
                var emails = await GetSchoolReviewerEmailsAsync();
                if (emails.Count == 0)
                {
                    _logger.LogInformation("资源 {ResourceId} 待审核通知：本租户无带邮箱的校审用户", resource.Id);
                    return;
                }

                var submitterName = await GetUserNameAsync(resource.CreatorId);
                var subject = $"【知识资源库】新资源《{resource.Name}》等待您审核";
                var html =
                    $"<p>您好：</p>" +
                    $"<p>用户 <b>{Escape(submitterName)}</b> 提交了新资源，请您及时审核。</p>" +
                    $"<p><b>资源名称：</b>{Escape(resource.Name)}</p>" +
                    $"<p><b>资源类型：</b>{ResourceTypeLabel(resource.ResourceType)}</p>" +
                    $"<p><b>提交时间：</b>{resource.CreationTime:yyyy-MM-dd HH:mm}</p>" +
                    $"<p><a href=\"{AngularUrl()}/resources\">点击进入审核页面</a></p>" +
                    $"<p>（本邮件由知识资源库系统自动发送）</p>";

                // 收件人与正文先算好，真正发信推迟到事务提交后
                var recipients = emails;
                var tenantId = resource.TenantId;
                SendAfterCommit(async () =>
                {
                  using (_currentTenant.Change(tenantId))
                  {
                    foreach (var email in recipients)
                    {
                        await _mailSender.TrySendAsync(new SmtpMailMessage
                        {
                            To = email,
                            Subject = subject,
                            HtmlBody = html,
                        });
                    }
                  }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "资源 {ResourceId} 提交审核通知发送失败（不阻断主流程）", resource.Id);
        }
    }

    public async Task NotifyCreatorAuditResultAsync(Resource resource, bool approved, string? comment, string stageLabel)
    {
        try
        {
            if (!resource.CreatorId.HasValue || resource.CreatorId.Value == Guid.Empty)
            {
                return;
            }

            // 创建人可能已被软删/跨租户：关闭过滤器后查询
            IdentityUser? creator;
            using (_dataFilter.Disable<IMultiTenant>())
            using (_dataFilter.Disable<ISoftDelete>())
            {
                creator = await _userRepository.FindAsync(resource.CreatorId.Value);
            }

            if (creator == null || string.IsNullOrWhiteSpace(creator.Email) || !creator.IsActive)
            {
                _logger.LogInformation("资源 {ResourceId} 审核结果通知：创建者无可用邮箱，跳过", resource.Id);
                return;
            }

            var displayName = ResolveDisplayName(creator);
            var resultLabel = approved ? "通过" : "未通过";
            var subject = $"【知识资源库】资源《{resource.Name}》{stageLabel}{resultLabel}";

            var html =
                $"<p>{Escape(displayName)} 您好：</p>" +
                $"<p>您提交的资源 <b>{Escape(resource.Name)}</b> 经{Escape(stageLabel)}：<b>{resultLabel}</b>。</p>" +
                (!string.IsNullOrWhiteSpace(comment)
                    ? $"<p><b>审核意见：</b>{Escape(comment)}</p>"
                    : "") +
                $"<p><a href=\"{AngularUrl()}/resources\">点击查看资源</a></p>" +
                $"<p>（本邮件由知识资源库系统自动发送）</p>";

            var to = creator.Email!.Trim();
            var tenantId = resource.TenantId;
            // 在资源所属租户上下文中解析 SMTP（联盟审核在 host 操作时也用源租户配置），
            // 且发信推迟到事务提交后，避免请求失败仍已发出邮件。
            SendAfterCommit(async () =>
            {
                using (_currentTenant.Change(tenantId))
                {
                    await _mailSender.TrySendAsync(new SmtpMailMessage
                    {
                        To = to,
                        Subject = subject,
                        HtmlBody = html,
                    });
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "资源 {ResourceId} 审核结果通知发送失败（不阻断主流程）", resource.Id);
        }
    }

    /// <summary>
    /// 资源所属租户内持有 Resources.SchoolAudit 授权（角色或用户直授）的用户邮箱。
    /// 调用方须已切换到 resource.TenantId 上下文（PermissionGrant/Role/User 均按租户过滤）。
    /// </summary>
    private async Task<List<string>> GetSchoolReviewerEmailsAsync()
    {
        var grants = await _permissionGrantRepository.GetListAsync(g =>
            g.Name == KnowledgeHubPermissions.Resources.SchoolAudit);

        var userIds = new HashSet<Guid>();

        var roleNames = grants
            .Where(g => g.ProviderName == "R" && !string.IsNullOrWhiteSpace(g.ProviderKey))
            .Select(g => g.ProviderKey)
            .Distinct()
            .ToList();
        if (roleNames.Count > 0)
        {
            var roleIds = await _dbContext.Roles
                .Where(r => roleNames.Contains(r.Name))
                .Select(r => r.Id)
                .ToListAsync();

            if (roleIds.Count > 0)
            {
                var memberIds = await _dbContext.Set<IdentityUserRole>()
                    .Where(ur => roleIds.Contains(ur.RoleId))
                    .Select(ur => ur.UserId)
                    .ToListAsync();
                foreach (var id in memberIds)
                {
                    userIds.Add(id);
                }
            }
        }

        foreach (var grant in grants.Where(g => g.ProviderName == "U"))
        {
            if (Guid.TryParse(grant.ProviderKey, out var uid))
            {
                userIds.Add(uid);
            }
        }

        if (userIds.Count == 0)
        {
            return new List<string>();
        }

        var users = await _dbContext.Users
            .Where(u => userIds.Contains(u.Id)
                        && u.IsActive
                        && u.Email != null && u.Email != "")
            .ToListAsync();

        return users
            .Select(u => u.Email!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<string> GetUserNameAsync(Guid? userId)
    {
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return "未知用户";
        }

        IdentityUser? user;
        using (_dataFilter.Disable<IMultiTenant>())
        using (_dataFilter.Disable<ISoftDelete>())
        {
            user = await _userRepository.FindAsync(userId.Value);
        }
        return user == null ? "未知用户" : ResolveDisplayName(user);
    }

    private static string ResolveDisplayName(IdentityUser user)
    {
        var displayName = string.Join(" ", new[] { user.Name, user.Surname }
            .Where(x => !string.IsNullOrWhiteSpace(x))).Trim();
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }
        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            return user.UserName!;
        }
        return user.Email ?? user.Id.ToString();
    }

    private string AngularUrl()
        => (_configuration["App:AngularUrl"] ?? "http://localhost:4200").TrimEnd('/');

    private static string ResourceTypeLabel(ResourceType type) => type switch
    {
        ResourceType.Document => "文档",
        ResourceType.Video => "视频",
        ResourceType.Audio => "音频",
        ResourceType.Image => "图片",
        ResourceType.PPT => "PPT",
        _ => type.ToString(),
    };

    private static string Escape(string? text)
        => System.Net.WebUtility.HtmlEncode(text ?? "");
}
