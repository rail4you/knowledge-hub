using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Settings;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace KnowledgeHub.Emailing;

/// <summary>SMTP 连接配置（已解析：租户设置 → 全局设置 → appsettings 回退）。</summary>
public class SmtpMailConfig
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 465;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;

    /// <summary>服务器与账号齐全才允许发送。</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(UserName);
}

public class SmtpMailAttachment
{
    public required string FileName { get; init; }

    public required byte[] Content { get; init; }

    public string ContentType { get; init; } = "application/octet-stream";
}

public class SmtpMailMessage
{
    public required string To { get; init; }

    public IReadOnlyList<string>? Cc { get; init; }

    public IReadOnlyList<string>? Bcc { get; init; }

    public required string Subject { get; init; }

    public string? HtmlBody { get; init; }

    public string? TextBody { get; init; }

    public IReadOnlyList<SmtpMailAttachment>? Attachments { get; init; }
}

/// <summary>
/// SMTP 配置解析 + 邮件发送。
/// 业务流程内一律用 <see cref="TrySendAsync"/>（失败只记日志，绝不中断主流程）；
/// 管理页测试发送用 <see cref="SendAsync"/>（抛异常给用户看原因）。
/// </summary>
public interface ISmtpMailSender
{
    /// <summary>解析当前生效的 SMTP 配置（含 appsettings 回退）；未配置也返回对象供 IsConfigured 判断。</summary>
    Task<SmtpMailConfig> ResolveConfigAsync();

    /// <summary>发送邮件；未配置或发送失败抛出异常，调用方负责捕获。config 为空时自动解析。</summary>
    Task SendAsync(SmtpMailMessage message, SmtpMailConfig? config = null, CancellationToken cancellationToken = default);

    /// <summary>业务流程安全发送：失败仅记 Warning 并返回 false，不抛异常。</summary>
    Task<bool> TrySendAsync(SmtpMailMessage message, CancellationToken cancellationToken = default);
}

public class SmtpMailSender : ISmtpMailSender, ITransientDependency
{
    private readonly ISettingProvider _settingProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpMailSender> _logger;

    public SmtpMailSender(
        ISettingProvider settingProvider,
        IConfiguration configuration,
        ILogger<SmtpMailSender> logger)
    {
        _settingProvider = settingProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<SmtpMailConfig> ResolveConfigAsync()
    {
        var host = FirstNonEmpty(
            await _settingProvider.GetOrNullAsync(KnowledgeHubSettings.SmtpHost),
            _configuration["Smtp:Host"]);

        var userName = FirstNonEmpty(
            await _settingProvider.GetOrNullAsync(KnowledgeHubSettings.SmtpUserName),
            _configuration["Smtp:UserName"]);

        var portText = FirstNonEmpty(
            await _settingProvider.GetOrNullAsync(KnowledgeHubSettings.SmtpPort),
            _configuration["Smtp:Port"],
            "465");

        return new SmtpMailConfig
        {
            Host = host?.Trim() ?? string.Empty,
            Port = int.TryParse(portText, out var port) ? port : 465,
            UserName = userName?.Trim() ?? string.Empty,
            Password = FirstNonEmpty(
                await _settingProvider.GetOrNullAsync(KnowledgeHubSettings.SmtpPassword),
                _configuration["Smtp:Password"]) ?? string.Empty,
            FromAddress = FirstNonEmpty(
                await _settingProvider.GetOrNullAsync(KnowledgeHubSettings.SmtpFromAddress),
                _configuration["Smtp:FromAddress"])?.Trim() ?? string.Empty,
            FromName = FirstNonEmpty(
                await _settingProvider.GetOrNullAsync(KnowledgeHubSettings.SmtpFromName),
                _configuration["Smtp:FromName"])?.Trim() ?? string.Empty,
        };
    }

    public async Task SendAsync(
        SmtpMailMessage message,
        SmtpMailConfig? config = null,
        CancellationToken cancellationToken = default)
    {
        config ??= await ResolveConfigAsync();
        if (!config.IsConfigured)
        {
            throw new InvalidOperationException("SMTP 未配置，请先在 SMTP 设置中填写服务器与账号。");
        }

        var mime = BuildMimeMessage(config, message);

        using var client = new SmtpClient { Timeout = 15000 };
        try
        {
            // 465 走隐式 SSL；587 走 STARTTLS。
            var socketOptions = config.Port == 587
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.SslOnConnect;

            await client.ConnectAsync(config.Host, config.Port, socketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(config.UserName))
            {
                await client.AuthenticateAsync(config.UserName, config.Password, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("邮件已发送：To={To} Subject={Subject}", message.To, message.Subject);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "邮件发送失败：To={To} Subject={Subject}", message.To, message.Subject);
            throw;
        }
        finally
        {
            if (client.IsConnected)
            {
                try
                {
                    await client.DisconnectAsync(true, CancellationToken.None);
                }
                catch
                {
                    // 断开失败不影响调用方
                }
            }
        }
    }

    public async Task<bool> TrySendAsync(SmtpMailMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            await SendAsync(message, cancellationToken: cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "业务邮件发送失败（不中断主流程）：To={To} Subject={Subject}",
                message.To, message.Subject);
            return false;
        }
    }

    private static MimeMessage BuildMimeMessage(SmtpMailConfig config, SmtpMailMessage message)
    {
        var mime = new MimeMessage();

        var fromAddress = string.IsNullOrWhiteSpace(config.FromAddress)
            ? config.UserName
            : config.FromAddress;
        mime.From.Add(string.IsNullOrWhiteSpace(config.FromName)
            ? new MailboxAddress("", fromAddress)
            : new MailboxAddress(config.FromName, fromAddress));

        mime.To.Add(MailboxAddress.Parse(message.To));
        foreach (var cc in message.Cc ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(cc))
            {
                mime.Cc.Add(MailboxAddress.Parse(cc));
            }
        }

        foreach (var bcc in message.Bcc ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(bcc))
            {
                mime.Bcc.Add(MailboxAddress.Parse(bcc));
            }
        }

        mime.Subject = message.Subject;

        var builder = new BodyBuilder
        {
            TextBody = message.TextBody ?? StripHtml(message.HtmlBody),
            HtmlBody = message.HtmlBody,
        };

        foreach (var attachment in message.Attachments ?? Array.Empty<SmtpMailAttachment>())
        {
            builder.Attachments.Add(attachment.FileName, attachment.Content,
                ContentType.Parse(attachment.ContentType));
        }

        mime.Body = builder.ToMessageBody();
        return mime;
    }

    private static string StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ").Trim();
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
