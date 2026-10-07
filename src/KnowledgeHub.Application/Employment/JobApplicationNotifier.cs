using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using KnowledgeHub.Emailing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace KnowledgeHub.Employment;

/// <summary>
/// 招聘投递邮件通知（全部 try/catch，绝不阻断投递主流程）：
/// 1. 新投递 → 邮件通知 HR（岗位联系邮箱，回退发布者邮箱），简历附件尽力附上；
/// 2. 同一封投递摘要发一份副本给学生。
/// </summary>
public interface IJobApplicationNotifier
{
    Task NotifyApplicationSubmittedAsync(
        JobPosting job,
        StudentResume resume,
        string? coverLetter,
        Guid studentId);
}

public class JobApplicationNotifier : IJobApplicationNotifier, ITransientDependency
{
    private readonly ISmtpMailSender _mailSender;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<JobApplicationNotifier> _logger;

    public JobApplicationNotifier(
        ISmtpMailSender mailSender,
        IRepository<IdentityUser, Guid> userRepository,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<JobApplicationNotifier> logger)
    {
        _mailSender = mailSender;
        _userRepository = userRepository;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    /// <summary>
    /// 有活跃 UoW 时推迟到事务提交后再发信（OnCompleted 仅在 SaveChanges 成功后执行），
    /// 避免投递记录保存失败（如 409 并发冲突）却已经给 HR/学生发了邮件、重试时重复投递。
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
                _logger.LogWarning(ex, "提交后发送投递通知邮件失败");
            }
        });
    }

    public async Task NotifyApplicationSubmittedAsync(
        JobPosting job,
        StudentResume resume,
        string? coverLetter,
        Guid studentId)
    {
        try
        {
            // HR 收件人：岗位联系邮箱优先，回退岗位发布者账号邮箱
            var hrEmail = FirstValidEmail(job.ContactEmail);
            if (hrEmail == null)
            {
                var employer = await FindUserAsync(job.EmployerUserId);
                hrEmail = FirstValidEmail(employer?.Email);
            }

            // 学生副本：当前投递人账号邮箱，回退简历上的邮箱
            var student = await FindUserAsync(studentId);
            var studentEmail = FirstValidEmail(student?.Email) ?? FirstValidEmail(resume.Email);

            if (hrEmail == null && studentEmail == null)
            {
                _logger.LogInformation("岗位 {JobId} 投递通知：HR 与学生均无可用邮箱，跳过", job.Id);
                return;
            }

            var subject = $"【知识资源库】收到新简历投递：{resume.FullName} 应聘《{job.Title}》";
            var html = BuildBody(job, resume, coverLetter);

            SmtpMailAttachment? attachment = null;
            if (hrEmail != null)
            {
                attachment = await TryLoadResumeAttachmentAsync(resume);
            }

            var attachments = attachment == null ? null : new[] { attachment };
            // 收件人、正文、附件先备好，真正发信推迟到事务提交后
            SendAfterCommit(async () =>
            {
                if (hrEmail != null)
                {
                    await _mailSender.TrySendAsync(new SmtpMailMessage
                    {
                        To = hrEmail,
                        Subject = subject,
                        HtmlBody = html,
                        Attachments = attachments,
                    });
                }

                if (studentEmail != null && !string.Equals(studentEmail, hrEmail, StringComparison.OrdinalIgnoreCase))
                {
                    await _mailSender.TrySendAsync(new SmtpMailMessage
                    {
                        To = studentEmail,
                        Subject = $"【知识资源库】投递回执：您已应聘《{job.Title}》",
                        HtmlBody = html,
                        Attachments = attachments,
                    });
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "岗位 {JobId} 投递邮件通知失败（不阻断主流程）", job.Id);
        }
    }

    private static string? FirstValidEmail(string? email)
    {
        email = email?.Trim();
        return string.IsNullOrWhiteSpace(email) || !email.Contains('@') ? null : email;
    }

    private async Task<IdentityUser?> FindUserAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }
        try
        {
            return await _userRepository.FindAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "查找用户 {UserId} 失败", userId);
            return null;
        }
    }

    private string BuildBody(JobPosting job, StudentResume resume, string? coverLetter)
    {
        var selfUrl = (_configuration["App:SelfUrl"] ?? "").TrimEnd('/');
        var jobLink = string.IsNullOrEmpty(selfUrl)
            ? $"/employment/jobs/{job.Id}"
            : $"{selfUrl}/employment/jobs/{job.Id}";

        var rows = new System.Text.StringBuilder();
        void Row(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                rows.Append("<tr>")
                    .Append($"<td style=\"padding:6px 12px;border:1px solid #ddd;background:#f7f7f7;white-space:nowrap;\"><b>{System.Net.WebUtility.HtmlEncode(label)}</b></td>")
                    .Append($"<td style=\"padding:6px 12px;border:1px solid #ddd;\">{System.Net.WebUtility.HtmlEncode(value)}</td>")
                    .Append("</tr>");
            }
        }

        Row("姓名", resume.FullName);
        Row("联系电话", resume.PhoneNumber);
        Row("邮箱", resume.Email);
        Row("学校", resume.SchoolName);
        Row("专业", resume.Major);
        Row("年级", resume.Grade);
        Row("求职意向", resume.Title);
        Row("个人简介", resume.Summary);
        Row("技能标签", resume.Skills);
        Row("教育经历", resume.EducationExperience);
        Row("实习经历", resume.InternshipExperience);
        Row("项目经历", resume.ProjectExperience);
        Row("证书", resume.CertificateText);

        var html =
            "<p>您好：</p>" +
            $"<p>收到一份新的简历投递，详情如下：</p>" +
            $"<p><b>应聘岗位：</b>{System.Net.WebUtility.HtmlEncode(job.Title)}（{System.Net.WebUtility.HtmlEncode(job.CompanyName)}）</p>" +
            $"<p><b>投递时间：</b>{DateTime.Now:yyyy-MM-dd HH:mm}</p>" +
            (string.IsNullOrWhiteSpace(coverLetter) ? "" :
                $"<p><b>求职信：</b><br/>{System.Net.WebUtility.HtmlEncode(coverLetter).Replace("\n", "<br/>")}</p>") +
            $"<table style=\"border-collapse:collapse;\">{rows}</table>" +
            $"<p><a href=\"{jobLink}\">查看岗位详情</a></p>" +
            "<p>（附件为简历原件，若未附上请在系统内查看）</p>" +
            "<p>（本邮件由知识资源库系统自动发送）</p>";
        return html;
    }

    /// <summary>尽力下载/读取简历附件；任何失败都返回 null（邮件照发，正文带链接）。</summary>
    private async Task<SmtpMailAttachment?> TryLoadResumeAttachmentAsync(StudentResume resume)
    {
        var url = resume.AttachmentUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            byte[] bytes;
            string fileName;

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                using var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("简历附件下载失败 {Url}，状态 {Status}", url, (int)response.StatusCode);
                    return null;
                }
                bytes = await response.Content.ReadAsByteArrayAsync();
                fileName = Path.GetFileName(new Uri(url).AbsolutePath);
            }
            else
            {
                // 本地相对路径（/uploads/... 或存储相对路径）
                var relative = url.StartsWith('/') ? url.TrimStart('/') : url;
                var root = _configuration["Storage:RootPath"] ?? "uploads";
                var fullPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, root, relative));
                var rootFull = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, root));
                if (!fullPath.StartsWith(rootFull, StringComparison.Ordinal) || !File.Exists(fullPath))
                {
                    return null;
                }
                bytes = await File.ReadAllBytesAsync(fullPath);
                fileName = Path.GetFileName(fullPath);
            }

            if (bytes.Length == 0)
            {
                return null;
            }
            if (bytes.Length > 15 * 1024 * 1024)
            {
                _logger.LogInformation("简历附件过大（{Size} 字节），不作为附件发送", bytes.Length);
                return null;
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "resume.pdf";
            }

            return new SmtpMailAttachment
            {
                FileName = fileName,
                Content = bytes,
                ContentType = GuessContentType(fileName),
            };
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "读取简历附件失败，跳过附件发送");
            return null;
        }
    }

    private static string GuessContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".rtf" => "application/rtf",
            _ => "application/octet-stream",
        };
    }
}
