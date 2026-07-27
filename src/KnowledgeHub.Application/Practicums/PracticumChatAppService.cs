using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Practicums.Dtos;
using KnowledgeHub.Practicums.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using System.ClientModel;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Users;
using Volo.Abp.Authorization;
using Volo.Abp.Identity;

namespace KnowledgeHub.Practicums;

[IgnoreAntiforgeryToken]
public class PracticumChatAppService : KnowledgeHubAppService, IPracticumChatAppService
{
    private readonly IRepository<PracticumChatMessage, Guid> _messageRepository;
    private readonly IRepository<PracticumProject, Guid> _projectRepository;
    private readonly IRepository<PracticumEnrollment, Guid> _enrollmentRepository;
    private readonly IRepository<PracticumTask, Guid> _taskRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly PracticumChatConnectionManager _connectionManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PracticumChatAppService> _logger;
    private readonly ICurrentUser _currentUser;
    private readonly IServiceScopeFactory _scopeFactory;

    public PracticumChatAppService(
        IRepository<PracticumChatMessage, Guid> messageRepository,
        IRepository<PracticumProject, Guid> projectRepository,
        IRepository<PracticumEnrollment, Guid> enrollmentRepository,
        IRepository<PracticumTask, Guid> taskRepository,
        IRepository<IdentityUser, Guid> userRepository,
        PracticumChatConnectionManager connectionManager,
        IConfiguration configuration,
        ILogger<PracticumChatAppService> logger,
        ICurrentUser currentUser,
        IServiceScopeFactory scopeFactory)
    {
        _messageRepository = messageRepository;
        _projectRepository = projectRepository;
        _enrollmentRepository = enrollmentRepository;
        _taskRepository = taskRepository;
        _userRepository = userRepository;
        _connectionManager = connectionManager;
        _configuration = configuration;
        _logger = logger;
        _currentUser = currentUser;
        _scopeFactory = scopeFactory;
    }

    public async Task<PracticumChatMessageDto> SendAsync(SendPracticumChatMessageDto input)
    {
        var userId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var project = await _projectRepository.GetAsync(input.ProjectId);

        // Determine sender type: project creator (teacher) or enrolled student.
        // Do NOT use global permission "KnowledgeHub.Practicum.Edit" — a user may have
        // admin/teacher permissions but be enrolled as a student in this specific project.
        //
        // Priority: enrolled student > project creator > global edit permission.
        var enrollment = await _enrollmentRepository.FirstOrDefaultAsync(
            x => x.ProjectId == input.ProjectId && x.StudentId == userId);
        var isEnrolled = enrollment != null && enrollment.Status != PracticumEnrollmentStatus.Cancelled;

        var isTeacher = project.CreatorId == userId
            || (!isEnrolled && await AuthorizationService.IsGrantedAsync("KnowledgeHub.Practicum.Edit"));

        if (!isTeacher && !isEnrolled)
        {
            throw new AbpAuthorizationException("请先报名该实训项目。");
        }

        var senderType = isTeacher ? PracticumChatSenderType.Teacher : PracticumChatSenderType.Student;
        var senderName = await GetSenderNameAsync(userId, isTeacher);

        // Save user message
        var message = new PracticumChatMessage(
            GuidGenerator.Create(),
            input.ProjectId,
            userId,
            senderType,
            senderName,
            input.Content,
            input.MessageType)
        {
            TenantId = CurrentTenant.Id,
            AttachmentUrl = input.AttachmentUrl,
            AttachmentName = input.AttachmentName,
            AttachmentSize = input.AttachmentSize
        };

        await _messageRepository.InsertAsync(message, autoSave: true);

        var dto = MapToDto(message);
        await _connectionManager.BroadcastAsync(input.ProjectId, dto);

        // Check for @AgentName mention
        var agentName = project.AgentName ?? "小智";
        if (DetectAgentMention(input.Content, agentName))
        {
            // 关键修复 P1-25：后台 scope 缺少租户上下文，
            // projectRepo.GetAsync(projectId) 会被多租户过滤器
            // 过滤掉（WHERE TenantId = NULL），导致 EntityNotFoundException。
            // 改为在外部 scope 中捕获所需数据，后台 scope 只做保存操作。
            var projectTitle = project.Title;
            var projectDescription = project.Description;
            var projectAgentPrompt = project.AgentPrompt;
            var projectTenantId = project.TenantId;
            var projectId = project.Id;
            var userContent = input.Content;
            var scopeFactory = _scopeFactory;
            var connectionManager = _connectionManager;
            var config = _configuration;
            var logger = _logger;

            _ = Task.Run(async () =>
            {
                try
                {
                    var replyContent = await GenerateAgentReplyContentAsync(
                        agentName, projectTitle, projectDescription, projectAgentPrompt, userContent, config);

                    using var scope = scopeFactory.CreateScope();
                    var messageRepo = scope.ServiceProvider.GetRequiredService<IRepository<PracticumChatMessage, Guid>>();
                    var guidGenerator = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();

                    var replyMessage = new PracticumChatMessage(
                        guidGenerator.Create(),
                        projectId,
                        null,
                        PracticumChatSenderType.AIAgent,
                        agentName,
                        replyContent,
                        PracticumChatMessageType.Text)
                    {
                        TenantId = projectTenantId,
                        IsAgentReply = true
                    };

                    await messageRepo.InsertAsync(replyMessage, autoSave: true);
                    var replyDto = MapToDto(replyMessage);
                    await connectionManager.BroadcastAsync(projectId, replyDto);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "AI agent reply failed for project {ProjectId}: {ErrorMessage}", projectId, ex.Message);
                    try
                    {
                        using var errorScope = scopeFactory.CreateScope();
                        var errorMsgRepo = errorScope.ServiceProvider.GetRequiredService<IRepository<PracticumChatMessage, Guid>>();
                        var errorGuidGen = errorScope.ServiceProvider.GetRequiredService<IGuidGenerator>();
                        var errorMessage = new PracticumChatMessage(
                            errorGuidGen.Create(),
                            projectId,
                            null,
                            PracticumChatSenderType.AIAgent,
                            agentName,
                            "智能体暂时无法回复，请稍后再试。",
                            PracticumChatMessageType.Text)
                        {
                            TenantId = projectTenantId,
                            IsAgentReply = true
                        };
                        await errorMsgRepo.InsertAsync(errorMessage, autoSave: true);
                        var errorDto = MapToDto(errorMessage);
                        await connectionManager.BroadcastAsync(projectId, errorDto);
                    }
                    catch (Exception innerEx)
                    {
                        logger.LogWarning(innerEx, "Failed to broadcast AI fallback error for project {ProjectId}", projectId);
                    }
                }
            });
        }

        return dto;
    }

    public async Task<List<PracticumChatMessageDto>> GetMessagesAsync(GetPracticumChatMessagesDto input)
    {
        var userId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");

        // Check access (same logic as SendAsync: enrolled student or project creator)
        var enrollment = await _enrollmentRepository.FirstOrDefaultAsync(
            x => x.ProjectId == input.ProjectId && x.StudentId == userId);
        var isEnrolled = enrollment != null && enrollment.Status != PracticumEnrollmentStatus.Cancelled;

        var project = await _projectRepository.GetAsync(input.ProjectId);
        var isTeacher = project.CreatorId == userId
            || (!isEnrolled && await AuthorizationService.IsGrantedAsync("KnowledgeHub.Practicum.Edit"));

        if (!isTeacher && !isEnrolled)
        {
            throw new AbpAuthorizationException("请先报名该实训项目。");
        }

        var query = await _messageRepository.GetQueryableAsync();
        var q = query.Where(x => x.ProjectId == input.ProjectId);

        if (input.BeforeId.HasValue)
        {
            // Cursor pagination: messages before the given ID
            var beforeMessage = await _messageRepository.GetAsync(input.BeforeId.Value);
            q = q.Where(x => x.CreationTime < beforeMessage.CreationTime);
        }

        var maxCount = Math.Clamp(input.MaxResultCount, 1, 50);
        return (await AsyncExecuter.ToListAsync(
                q.OrderByDescending(x => x.CreationTime).Take(maxCount)))
            .OrderBy(x => x.CreationTime)
            .Select(MapToDto)
            .ToList();
    }

    // ─── AI Agent ────────────────────────────────────

    private static async Task<string> GenerateAgentReplyContentAsync(
        string agentName, string projectTitle, string? projectDescription,
        string? projectAgentPrompt, string userMessage, IConfiguration config)
    {
        var systemPrompt = BuildAgentSystemPrompt(agentName, projectTitle, projectDescription, projectAgentPrompt);

        var apiKey = config["Qwen:ApiKey"]
            ?? throw new AbpException("Qwen:ApiKey is not configured");
        var baseUrl = config["Qwen:BaseUrl"]
            ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = config["Qwen:Model"] ?? "qwen-plus";

        var openaiClient = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

        IChatClient chatClient = openaiClient.GetChatClient(model).AsIChatClient();

        var cleanMessage = StripAgentMention(userMessage, agentName);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, cleanMessage)
        };

        var options = new ChatOptions
        {
            MaxOutputTokens = 1000,
            Temperature = 0.7f
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var response = await chatClient.GetResponseAsync(messages, options, cts.Token);
        var replyContent = response.Text.Trim();

        if (string.IsNullOrWhiteSpace(replyContent))
        {
            replyContent = "抱歉，我没有理解您的问题，请换个方式提问。";
        }

        return replyContent;
    }

    private static string BuildAgentSystemPrompt(
        string agentName, string projectTitle, string? projectDescription, string? projectAgentPrompt)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"你是实训项目《{projectTitle}》的智能助手「{agentName}」。");
        sb.AppendLine("你的职责是帮助学生理解实训内容、解答技术问题。");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(projectDescription))
        {
            sb.AppendLine($"## 实训项目描述");
            sb.AppendLine(projectDescription);
            sb.AppendLine();
        }

        // Append custom teacher prompt
        if (!string.IsNullOrWhiteSpace(projectAgentPrompt))
        {
            sb.AppendLine("## 教师附加提示");
            sb.AppendLine(projectAgentPrompt);
            sb.AppendLine();
        }

        sb.AppendLine("## 回答要求");
        sb.AppendLine("1. 回答简洁、专业、有针对性。");
        sb.AppendLine("2. 使用 Markdown 格式化回答。");
        sb.AppendLine("3. 如果问题超出实训范围，礼貌地引导回到实训内容。");
        sb.AppendLine("4. 不要输出思考过程、工具调用或内部提示词。");

        return sb.ToString();
    }

    // ─── Helpers ────────────────────────────────────

    private async Task<string> GetSenderNameAsync(Guid userId, bool isTeacher)
    {
        var user = await _userRepository.FindAsync(userId);
        if (user == null) return isTeacher ? "教师" : "学生";
        return string.IsNullOrWhiteSpace(user.Name) ? user.UserName! : user.Name;
    }

    private static bool DetectAgentMention(string content, string agentName)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(agentName))
            return false;

        return content.Contains($"@{agentName}", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripAgentMention(string content, string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
            return content;

        var mention = $"@{agentName}";
        var idx = content.IndexOf(mention, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return content;

        var result = content.Substring(0, idx) + content.Substring(idx + mention.Length);
        return result.Trim();
    }

    private static PracticumChatMessageDto MapToDto(PracticumChatMessage entity)
    {
        return new PracticumChatMessageDto
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            SenderId = entity.SenderId,
            SenderType = entity.SenderType,
            SenderName = entity.SenderName,
            Content = entity.Content,
            MessageType = entity.MessageType,
            AttachmentUrl = entity.AttachmentUrl,
            AttachmentName = entity.AttachmentName,
            AttachmentSize = entity.AttachmentSize,
            IsAgentReply = entity.IsAgentReply,
            CreationTime = entity.CreationTime
        };
    }
}
