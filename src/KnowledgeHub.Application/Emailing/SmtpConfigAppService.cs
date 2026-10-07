using System.Threading.Tasks;
using KnowledgeHub.Permissions;
using KnowledgeHub.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;

namespace KnowledgeHub.Emailing;

/// <summary>
/// SMTP 邮件设置：读写当前租户（host 上下文写全局）的 SMTP 配置，支持测试发送。
/// 密码经 SettingDefinition(isEncrypted) 加密存储，从不回传明文。
/// </summary>
[IgnoreAntiforgeryToken]
public class SmtpConfigAppService : KnowledgeHubAppService, ISmtpConfigAppService
{
    private readonly ISettingProvider _settingProvider;
    private readonly ISettingManager _settingManager;
    private readonly ISmtpMailSender _mailSender;

    public SmtpConfigAppService(
        ISettingProvider settingProvider,
        ISettingManager settingManager,
        ISmtpMailSender mailSender)
    {
        _settingProvider = settingProvider;
        _settingManager = settingManager;
        _mailSender = mailSender;
    }

    [Authorize(KnowledgeHubPermissions.Smtp.Default)]
    public async Task<SmtpConfigDto> GetAsync()
    {
        // ISettingProvider 按 用户→租户→全局→默认值 回退：
        // 租户管理员看到"本租户配置（或全局回退）"，host admin 看到全局配置。
        var password = await _settingProvider.GetOrNullAsync(KnowledgeHubSettings.SmtpPassword);

        return new SmtpConfigDto
        {
            Host = await GetStringAsync(KnowledgeHubSettings.SmtpHost),
            Port = await GetPortAsync(),
            UserName = await GetStringAsync(KnowledgeHubSettings.SmtpUserName),
            HasPassword = !string.IsNullOrWhiteSpace(password),
            FromAddress = await GetStringAsync(KnowledgeHubSettings.SmtpFromAddress),
            FromName = await GetStringAsync(KnowledgeHubSettings.SmtpFromName),
            IsConfigured = (await _mailSender.ResolveConfigAsync()).IsConfigured,
        };
    }

    [Authorize(KnowledgeHubPermissions.Smtp.Default)]
    public async Task<SmtpConfigDto> UpdateAsync(UpdateSmtpConfigDto input)
    {
        var host = (input.Host ?? "").Trim();
        var userName = (input.UserName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(userName))
        {
            throw new UserFriendlyException("SMTP 服务器与账号不能为空。");
        }

        if (input.Port is < 1 or > 65535)
        {
            throw new UserFriendlyException("SMTP 端口必须在 1-65535 之间。");
        }

        // host 上下文写全局（所有租户的回退配置），租户上下文只写本租户。
        await SetAsync(KnowledgeHubSettings.SmtpHost, host);
        await SetAsync(KnowledgeHubSettings.SmtpPort, input.Port.ToString());
        await SetAsync(KnowledgeHubSettings.SmtpUserName, userName);
        await SetAsync(KnowledgeHubSettings.SmtpFromAddress, (input.FromAddress ?? "").Trim());
        await SetAsync(KnowledgeHubSettings.SmtpFromName, (input.FromName ?? "").Trim());

        // 密码：为空表示保持不变（改密码才传），ClearPassword 显式清空。
        var password = (input.Password ?? "").Trim();
        if (input.ClearPassword)
        {
            await SetAsync(KnowledgeHubSettings.SmtpPassword, "");
        }
        else if (!string.IsNullOrEmpty(password))
        {
            await SetAsync(KnowledgeHubSettings.SmtpPassword, password);
        }

        return await GetAsync();
    }

    [Authorize(KnowledgeHubPermissions.Smtp.Default)]
    public async Task<SmtpTestResultDto> SendTestEmailAsync(SendSmtpTestEmailDto input)
    {
        var to = (input.To ?? "").Trim();
        if (string.IsNullOrWhiteSpace(to) || !to.Contains('@'))
        {
            return new SmtpTestResultDto { Success = false, Error = "请输入有效的收件邮箱地址。" };
        }

        try
        {
            await _mailSender.SendAsync(new SmtpMailMessage
            {
                To = to,
                Subject = "【知识资源库】SMTP 邮件发送测试",
                HtmlBody =
                    "<p>您好，</p>" +
                    "<p>这是一封来自 <b>知识资源库</b> 的 SMTP 测试邮件，收到即说明邮件发送配置正确。</p>" +
                    "<p>时间：" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "</p>",
            });
            return new SmtpTestResultDto { Success = true };
        }
        catch (System.Exception ex)
        {
            return new SmtpTestResultDto { Success = false, Error = ex.Message };
        }
    }

    private async Task<string> GetStringAsync(string name)
        => (await _settingProvider.GetOrNullAsync(name))?.Trim() ?? "";

    private async Task<int> GetPortAsync()
    {
        var text = await GetStringAsync(KnowledgeHubSettings.SmtpPort);
        return int.TryParse(text, out var port) ? port : 465;
    }

    private Task SetAsync(string name, string value)
    {
        return CurrentTenant.Id.HasValue
            ? _settingManager.SetForTenantAsync(CurrentTenant.Id.Value, name, value)
            : _settingManager.SetGlobalAsync(name, value);
    }
}
