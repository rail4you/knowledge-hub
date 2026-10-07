using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Emailing;

/// <summary>SMTP 配置（租户级；host 上下文读写全局）。</summary>
public class SmtpConfigDto
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 465;

    public string UserName { get; set; } = string.Empty;

    /// <summary>是否已配置密码（密码从不回传明文）。</summary>
    public bool HasPassword { get; set; }

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;

    /// <summary>服务器 + 账号齐全。</summary>
    public bool IsConfigured { get; set; }
}

public class UpdateSmtpConfigDto
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 465;

    public string UserName { get; set; } = string.Empty;

    /// <summary>密码/授权码；为空表示保持原密码不变。</summary>
    public string? Password { get; set; }

    /// <summary>为 true 时清空密码（与改密码互斥）。</summary>
    public bool ClearPassword { get; set; }

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;
}

public class SendSmtpTestEmailDto
{
    public string To { get; set; } = string.Empty;
}

public class SmtpTestResultDto
{
    public bool Success { get; set; }

    public string? Error { get; set; }
}

/// <summary>
/// SMTP 邮件设置：读写当前租户（host 为全局）的 SMTP 配置并支持测试发送。
/// 密码加密存储（SettingDefinition isEncrypted），从不回传。
/// </summary>
public interface ISmtpConfigAppService : IApplicationService
{
    /// <summary>获取当前生效的 SMTP 配置。</summary>
    Task<SmtpConfigDto> GetAsync();

    /// <summary>保存 SMTP 配置（写当前租户；host 上下文写全局）。</summary>
    Task<SmtpConfigDto> UpdateAsync(UpdateSmtpConfigDto input);

    /// <summary>发送一封测试邮件（使用当前已保存的配置）。</summary>
    Task<SmtpTestResultDto> SendTestEmailAsync(SendSmtpTestEmailDto input);
}
