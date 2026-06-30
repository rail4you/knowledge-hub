using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        // Check access: teacher who has edit permission, or enrolled student
        var canEdit = await AuthorizationService.IsGrantedAsync("KnowledgeHub.Practicum.Edit");
        if (!canEdit)
        {
            var enrollment = await _enrollmentRepository.FirstOrDefaultAsync(
                x => x.ProjectId == input.ProjectId && x.StudentId == userId);
            if (enrollment == null || enrollment.Status == PracticumEnrollmentStatus.Cancelled)
            {
                throw new AbpAuthorizationException("请先报名该实训项目。");
            }
        }

        var senderName = await GetSenderNameAsync(userId, canEdit);
        var senderType = canEdit ? PracticumChatSenderType.Teacher : PracticumChatSenderType.Student;

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
            // Fire-and-forget AI response (we don't wait for it to return in the POST)
            // Background AI response using a fresh scope (scoped services are disposed after the HTTP request)
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
                    using var scope = scopeFactory.CreateScope();
                    var messageRepo = scope.ServiceProvider.GetRequiredService<IRepository<PracticumChatMessage, Guid>>();
                    var projectRepo = scope.ServiceProvider.GetRequiredService<IRepository<PracticumProject, Guid>>();
                    var guidGenerator = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();

                    var project = await projectRepo.GetAsync(projectId);
                    var replyContent = await GenerateAgentReplyContentAsync(project, userContent, agentName, config);

                    var replyMessage = new PracticumChatMessage(
                        guidGenerator.Create(),
                        projectId,
                        null,
                        PracticumChatSenderType.AIAgent,
                        agentName,
                        replyContent,
                        PracticumChatMessageType.Text)
                    {
                        TenantId = project.TenantId,
                        IsAgentReply = true
                    };

                    await messageRepo.InsertAsync(replyMessage, autoSave: true);
                    var replyDto = MapToDto(replyMessage);
                    await connectionManager.BroadcastAsync(projectId, replyDto);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "AI agent reply failed for project {ProjectId}", projectId);
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
                            IsAgentReply = true
                        };
                        await errorMsgRepo.InsertAsync(errorMessage, autoSave: true);
                        var errorDto = MapToDto(errorMessage);
                        await connectionManager.BroadcastAsync(projectId, errorDto);
                    }
                    catch { /* best-effort error notification */ }
                }
            });
        }

        return dto;
    }

    public async Task<List<PracticumChatMessageDto>> GetMessagesAsync(GetPracticumChatMessagesDto input)
    {
        var userId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");

        // Check access
        var canEdit = await AuthorizationService.IsGrantedAsync("KnowledgeHub.Practicum.Edit");
        if (!canEdit)
        {
            var enrollment = await _enrollmentRepository.FirstOrDefaultAsync(
                x => x.ProjectId == input.ProjectId && x.StudentId == userId);
            if (enrollment == null || enrollment.Status == PracticumEnrollmentStatus.Cancelled)
            {
                throw new AbpAuthorizationException("请先报名该实训项目。");
            }
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
        PracticumProject project, string userMessage, string agentName, IConfiguration config)
    {
        var systemPrompt = BuildAgentSystemPrompt(project, agentName);

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

        var response = await chatClient.GetResponseAsync(messages, options);
        var replyContent = response.Text.Trim();

        if (string.IsNullOrWhiteSpace(replyContent))
        {
            replyContent = "抱歉，我没有理解您的问题，请换个方式提问。";
        }

        return replyContent;
    }

    private static string BuildAgentSystemPrompt(PracticumProject project, string agentName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"你是实训项目《{project.Title}》的智能助手「{agentName}」。");
        sb.AppendLine("你的职责是帮助学生理解实训内容、解答技术问题。");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(project.Description))
        {
            sb.AppendLine($"## 实训项目描述");
            sb.AppendLine(project.Description);
            sb.AppendLine();
        }

        // Append custom teacher prompt
        if (!string.IsNullOrWhiteSpace(project.AgentPrompt))
        {
            sb.AppendLine("## 教师附加提示");
            sb.AppendLine(project.AgentPrompt);
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
