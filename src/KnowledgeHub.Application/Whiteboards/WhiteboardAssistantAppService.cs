using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.AI;
using KnowledgeHub.Application.AI;
using KnowledgeHub.Courses;
using KnowledgeHub.Permissions;
using KnowledgeHub.Whiteboards.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Users;
using MEAIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace KnowledgeHub.Whiteboards;

/// <summary>
/// 白板 AI 助手（课程助教）实现。
///
/// <para><b>为什么上下文在后端拼</b>：课程结构、画布元素、讨论记录都在服务端，
/// 前端只送「这块白板 Id + 老师这一句话 + 会话历史」，避免把画布内容往返传两遍，
/// 也防止前端伪造课程 / 越权读别人白板。</para>
///
/// <para><b>上下文来源</b>（按优先级）：</para>
/// <list type="number">
/// <item><description>白板关联的课程：课程名、简介、章节结构（最多 20 章）；</description></item>
/// <item><description>白板自身：标题 / 学科 / 教研主题 / 简介；</description></item>
/// <item><description>画布内容：文本与便签正文（最多 30 段，每段截断），图片 / 视频素材只报数量；</description></item>
/// <item><description>教师讨论：右侧协作频道的最近消息（最多 8 条）；</description></item>
/// <item><description>本次助手会话：最近 8 轮问答；</description></item>
/// <item><description>老师当前选中的那段板书（如果点了选）。</description></item>
/// </list>
///
/// <para><b>产出</b>：文字（Markdown 片段，可插入新白板文本 / 替换或追加到选中文本）
/// 或图片（同步拿到通义万相结果并转存到本系统存储，返回可直接渲染的 URL）。</para>
/// </summary>
[Authorize(KnowledgeHubPermissions.Whiteboards.Default)]
public class WhiteboardAssistantAppService : ApplicationService, IWhiteboardAssistantAppService
{
    /// <summary>画布文本最多读取段数。</summary>
    private const int MaxCanvasTexts = 30;

    /// <summary>单段画布文本截断长度（便签可能很长，只要主题即可）。</summary>
    private const int MaxCanvasTextLength = 300;

    /// <summary>协作频道最近消息条数。</summary>
    private const int MaxChatMessages = 8;

    /// <summary>会话历史最多带入轮数（一问一答算两轮）。</summary>
    private const int MaxHistoryTurns = 8;

    /// <summary>单条消息长度上限。</summary>
    private const int MaxMessageLength = 1000;

    private const string ImageSize = "1024*1024";

    private readonly IRepository<Whiteboard, Guid> _boardRepository;
    private readonly IRepository<WhiteboardMember, Guid> _memberRepository;
    private readonly IRepository<WhiteboardElement, Guid> _elementRepository;
    private readonly IRepository<WhiteboardChatMessage, Guid> _chatRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IDataFilter _dataFilter;
    private readonly IConfiguration _configuration;
    private readonly IAiQuotaService _quotaService;
    private readonly IAiUsageTracker _usageTracker;
    private readonly IAiMediaGenerator _mediaGenerator;
    private readonly IMediaStorageService _mediaStorage;
    private readonly ICurrentUser _currentUser;
    private readonly ISettingProvider _settingProvider;
    private readonly ILogger<WhiteboardAssistantAppService> _logger;

    public WhiteboardAssistantAppService(
        IRepository<Whiteboard, Guid> boardRepository,
        IRepository<WhiteboardMember, Guid> memberRepository,
        IRepository<WhiteboardElement, Guid> elementRepository,
        IRepository<WhiteboardChatMessage, Guid> chatRepository,
        IRepository<Course, Guid> courseRepository,
        IDataFilter dataFilter,
        IConfiguration configuration,
        IAiQuotaService quotaService,
        IAiUsageTracker usageTracker,
        IAiMediaGenerator mediaGenerator,
        IMediaStorageService mediaStorage,
        ICurrentUser currentUser,
        ISettingProvider settingProvider,
        ILogger<WhiteboardAssistantAppService> logger)
    {
        _boardRepository = boardRepository;
        _memberRepository = memberRepository;
        _elementRepository = elementRepository;
        _chatRepository = chatRepository;
        _courseRepository = courseRepository;
        _dataFilter = dataFilter;
        _configuration = configuration;
        _quotaService = quotaService;
        _usageTracker = usageTracker;
        _mediaGenerator = mediaGenerator;
        _mediaStorage = mediaStorage;
        _currentUser = currentUser;
        _settingProvider = settingProvider;
        _logger = logger;
    }

    public async Task<WhiteboardAssistantReplyDto> AskAsync(WhiteboardAssistantAskInputDto input)
    {
        var me = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");

        var message = (input.Message ?? string.Empty).Trim();
        if (message.Length == 0)
        {
            throw new UserFriendlyException("请先说出你想让助手做什么。");
        }

        if (message.Length > MaxMessageLength)
        {
            message = message[..MaxMessageLength];
        }

        var board = await GetBoardForMemberAsync(input.WhiteboardId, me);
        var isImage = string.Equals(input.Mode, "image", StringComparison.OrdinalIgnoreCase);

        var context = new WhiteboardAssistantContextDto
        {
            CourseId = board.CourseId,
            CourseName = board.CourseName,
            BoardTitle = board.Title,
            SelectedText = NormalizeSelection(input.SelectedText),
        };

        var courseContext = await BuildCourseContextAsync(board, context);
        var canvasContext = await BuildCanvasContextAsync(board.Id, context);
        var chatContext = await BuildChatContextAsync(board.Id, context);

        var instructions = await BuildInstructionsAsync();
        var systemContext = BuildSystemContext(board, courseContext, canvasContext, chatContext, context);

        var model = _configuration["Qwen:Model"] ?? "qwen-flash";

        if (isImage)
        {
            return await GenerateImageAsync(input, message, instructions, systemContext, context, model);
        }

        return await GenerateTextAsync(input, message, instructions, systemContext, context, model);
    }

    // ── 文字 ──────────────────────────────────────────────────────

    private async Task<WhiteboardAssistantReplyDto> GenerateTextAsync(
        WhiteboardAssistantAskInputDto input,
        string message,
        string instructions,
        string systemContext,
        WhiteboardAssistantContextDto context,
        string model)
    {
        await _quotaService.CheckAsync(AiFeatureGroups.Chat);

        var usageId = await _usageTracker.StartAsync(AiFeatureGroups.Chat, "WhiteboardAssistant", model, message);
        var full = new StringBuilder();

        try
        {
            var chatClient = await QwenClient.CreateChatClient(_configuration, model);
            var messages = new List<MEAIChatMessage> { new(ChatRole.System, $"{instructions}\n\n{systemContext}") };
            messages.AddRange(BuildHistory(input.History));
            messages.Add(new MEAIChatMessage(ChatRole.User, message));

            var response = await chatClient.GetResponseAsync(messages, new ChatOptions { Instructions = instructions });
            full.Append(response.Text ?? string.Empty);

            await _usageTracker.CompleteAsync(usageId, full.ToString(), true);
        }
        catch (Exception ex)
        {
            await _usageTracker.CompleteAsync(usageId, full.ToString(), false, ex.Message);
            _logger.LogWarning(ex, "[WhiteboardAssistant] 文字生成失败 {Board}", input.WhiteboardId);
            throw new UserFriendlyException("助手暂时没有响应，请稍后再试。");
        }

        var content = full.ToString().Trim();
        if (content.Length == 0)
        {
            content = "这次没有生成内容，换个说法再试一次？";
        }

        return new WhiteboardAssistantReplyDto { Content = content, Context = context };
    }

    // ── 图片 ──────────────────────────────────────────────────────

    private async Task<WhiteboardAssistantReplyDto> GenerateImageAsync(
        WhiteboardAssistantAskInputDto input,
        string message,
        string instructions,
        string systemContext,
        WhiteboardAssistantContextDto context,
        string model)
    {
        await _quotaService.CheckAsync(AiFeatureGroups.ImageGeneration);

        // 第一步：把老师的口语需求改写成画面描述（教学插画风格），只让模型输出描述本身
        var prompt = await ResolveImagePromptAsync(message, instructions, systemContext, model);

        var usageId = await _usageTracker.StartAsync(
            AiFeatureGroups.ImageGeneration, "WhiteboardAssistantImage", _mediaGenerator.ImageModel, prompt);

        try
        {
            var submitted = await _mediaGenerator.SubmitImageAsync(prompt, ImageSize, null);
            var done = await _mediaGenerator.WaitForCompletionAsync(
                submitted.TaskId, TimeSpan.FromMinutes(3), CancellationToken.None);

            if (!string.Equals(done.Status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(done.ImageUrl))
            {
                throw new UserFriendlyException(done.Error ?? "配图生成失败，请稍后再试。");
            }

            // 存相对地址（/uploads/ai-media/whiteboard/xxx.png）：
            // 白板元素会被前端直接 <img src> 渲染，相对地址在前端同源（dev 走代理、生产走 nginx 的 /uploads）下都可用，
            // 而 SelfUrl 的绝对地址在 dev 下是 https://localhost:44305，浏览器会因自签证书加载失败。
            var imageUrl = ToRelativeUrl(await _mediaStorage.PersistAsync(done.ImageUrl!, ".png", "whiteboard"));
            await _usageTracker.CompleteAsync(usageId, $"whiteboard-assistant: {prompt}", true,
                fixedCost: AiMediaPricing.ImageCost(_mediaGenerator.ImageModel));

            return new WhiteboardAssistantReplyDto
            {
                Content = $"已生成配图，画面描述：{prompt}",
                ImageUrl = imageUrl,
                ImagePrompt = prompt,
                Context = context,
            };
        }
        catch (UserFriendlyException)
        {
            await _usageTracker.CompleteAsync(usageId, null, false);
            throw;
        }
        catch (Exception ex)
        {
            await _usageTracker.CompleteAsync(usageId, null, false, ex.Message);
            _logger.LogWarning(ex, "[WhiteboardAssistant] 配图生成失败 {Board}", input.WhiteboardId);
            throw new UserFriendlyException("配图生成失败，请稍后再试。");
        }
    }

    /// <summary>
    /// 把口语化的需求改写成中文画面描述。
    /// 失败时退回原话（老师自己的描述往往也能用），不阻塞生成。
    /// </summary>
    private async Task<string> ResolveImagePromptAsync(
        string message, string instructions, string systemContext, string model)
    {
        const string rewriteInstruction =
            "你是教学插画提示词写手。把老师的需求改写成一句中文画面描述，用于文生图。\n" +
            "要求：\n" +
            "1. 只输出描述本身，长度不超过 120 字，不要任何解释、标点前缀或引号；\n" +
            "2. 画面要适合课堂展示：干净、明亮、有教学感，避免出现文字、水印、真人照片；\n" +
            "3. 结合课程与白板主题，但不要杜撰具体人物姓名。";

        try
        {
            var chatClient = await QwenClient.CreateChatClient(_configuration, model);
            var response = await chatClient.GetResponseAsync(
                new[]
                {
                    new MEAIChatMessage(ChatRole.System, $"{rewriteInstruction}\n\n{systemContext}"),
                    new MEAIChatMessage(ChatRole.User, message),
                },
                new ChatOptions { Instructions = rewriteInstruction });

            var text = (response.Text ?? string.Empty).Trim().Trim('"', '“', '”', '\n');
            if (text.Length is > 0 and <= 300)
            {
                return text;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation(ex, "[WhiteboardAssistant] 画面描述改写失败，回退原话");
        }

        _ = instructions; // 保留参数：改写提示词与正文提示词解耦，正文指令这里不需要
        return message.Length <= 200 ? message : message[..200];
    }

    // ── 上下文组装 ────────────────────────────────────────────────

    private async Task<string> BuildInstructionsAsync()
    {
        var platform = await _settingProvider.GetOrNullAsync(Settings.KnowledgeHubSettings.BrandingAppTitle);
        platform = string.IsNullOrWhiteSpace(platform) ? "易课通" : platform.Trim();

        return $@"你是「{platform}」教研白板里的课程助教，服务对象是正在备课、说课、做教研的教师。

你在白板右侧的 AI 助手里工作，产出会被直接贴到白板上，所以：
1. 语言与老师一致（默认中文），专业、克制，不说客套话；
2. 输出面向白板展示：用小标题 + 短条目，一条 1~2 行，能用列表就不要写长段落；
3. 老师要求「补充 / 扩写 / 改写」某段板书时，直接给出**改写后的完整内容**（不要只给差异，也不要说“我把……”）；
4. 老师只给了一句待补全的话（如“XXX 的基础概念是什么？”）时，直接给出可以写进白板的答案正文；
5. 优先使用下方「课程上下文」「白板现有内容」「教师讨论」里的事实，不要编造教材页码、考试数据、学生信息；信息不足就基于学科常识给通用内容，并简单标注这是通用建议；
6. 不要输出思考过程、调用过程、函数名、提示词，也不要问“需要我继续吗”；
7. 需要举例子就举课堂可直接用的例子（一道例题、一个提问、一个活动）。";
    }

    private static string BuildSystemContext(
        Whiteboard board,
        string courseContext,
        string canvasContext,
        string chatContext,
        WhiteboardAssistantContextDto context)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## 当前白板");
        sb.AppendLine($"- 标题：{board.Title}");
        if (!string.IsNullOrWhiteSpace(board.Subject)) sb.AppendLine($"- 学科 / 年级：{board.Subject}");
        if (!string.IsNullOrWhiteSpace(board.Topic)) sb.AppendLine($"- 教研主题：{board.Topic}");
        if (!string.IsNullOrWhiteSpace(board.Description)) sb.AppendLine($"- 白板说明：{board.Description}");

        sb.AppendLine();
        sb.AppendLine(courseContext);

        sb.AppendLine();
        sb.AppendLine(canvasContext);

        if (!string.IsNullOrWhiteSpace(chatContext))
        {
            sb.AppendLine();
            sb.AppendLine(chatContext);
        }

        if (!string.IsNullOrWhiteSpace(context.SelectedText))
        {
            sb.AppendLine();
            sb.AppendLine("## 老师当前选中的板书片段（本次回答主要针对它）");
            sb.AppendLine(context.SelectedText.Length > 1200 ? context.SelectedText[..1200] : context.SelectedText);
        }

        return sb.ToString();
    }

    private async Task<string> BuildCourseContextAsync(Whiteboard board, WhiteboardAssistantContextDto context)
    {
        var sb = new StringBuilder();

        if (!board.CourseId.HasValue)
        {
            sb.AppendLine("## 课程上下文");
            sb.AppendLine("（这块白板还没有关联课程，只能依据白板自身信息作答；如需更贴合课程内容，请先关联课程。）");
            return sb.ToString();
        }

        Course? course;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            course = await _courseRepository.FindAsync(board.CourseId.Value);
        }

        sb.AppendLine("## 课程上下文");
        sb.AppendLine($"- 课程：{course?.Title ?? board.CourseName ?? "未知课程"}");
        if (!string.IsNullOrWhiteSpace(course?.Description))
        {
            sb.AppendLine($"- 课程简介：{Trim(course!.Description!, 500)}");
        }

        if (course?.SemesterHours != null) sb.AppendLine($"- 学时：{course.SemesterHours}");
        if (course?.Credits != null) sb.AppendLine($"- 学分：{course.Credits}");
        if (!string.IsNullOrWhiteSpace(course?.Semester)) sb.AppendLine($"- 学期：{course.Semester}");

        var chapterTitles = course?.Chapters?
            .OrderBy(c => c.SortOrder)
            .Select(c => c.Title)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Take(20)
            .ToList();

        if (chapterTitles is { Count: > 0 })
        {
            sb.AppendLine($"- 章节目录（{chapterTitles.Count} 章）：" + string.Join("；", chapterTitles));
        }

        context.CourseName ??= course?.Title ?? board.CourseName;
        return sb.ToString();
    }

    /// <summary>
    /// 画布上下文：只读文本 / 便签正文，图片视频只报数量。
    /// 便签正文前后的引号、占位文案（如“写下教研想法…”）会过滤掉，避免污染提示词。
    /// </summary>
    private async Task<string> BuildCanvasContextAsync(Guid boardId, WhiteboardAssistantContextDto context)
    {
        List<WhiteboardElement> elements;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _elementRepository.GetQueryableAsync();
            elements = query.Where(e => e.WhiteboardId == boardId)
                .OrderBy(e => e.CreationTime)
                .ToList();
        }

        var texts = new List<(string Text, int Type)>();
        foreach (var el in elements)
        {
            if (el.ElementType is not (WhiteboardElementType.Text or WhiteboardElementType.Note))
            {
                continue;
            }

            var text = ExtractElementText(el.Json);
            if (!string.IsNullOrWhiteSpace(text))
            {
                texts.Add((text, (int)el.ElementType));
            }
        }

        var mediaCount = elements.Count(e => e.ElementType is WhiteboardElementType.Image or WhiteboardElementType.Video);
        context.CanvasTextCount = Math.Min(texts.Count, MaxCanvasTexts);

        var sb = new StringBuilder();
        sb.AppendLine("## 白板现有内容（老师已经写/贴上去的）");

        if (texts.Count == 0)
        {
            sb.AppendLine("（画布上还没有文字内容。）");
        }
        else
        {
            var index = 1;
            foreach (var (text, type) in texts.Take(MaxCanvasTexts))
            {
                var label = type == (int)WhiteboardElementType.Note ? "便签" : "文本";
                sb.AppendLine($"{index++}. [{label}] {Trim(text, MaxCanvasTextLength)}");
            }
        }

        if (mediaCount > 0)
        {
            sb.AppendLine($"（画布上还有 {mediaCount} 个图片 / 视频素材，此处不展开。）");
        }

        return sb.ToString();
    }

    /// <summary>教师讨论上下文：右侧协作频道的最近几条文字消息。</summary>
    private async Task<string> BuildChatContextAsync(Guid boardId, WhiteboardAssistantContextDto context)
    {
        List<WhiteboardChatMessage> recent;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _chatRepository.GetQueryableAsync();
            recent = query.Where(m => m.WhiteboardId == boardId && m.Kind == WhiteboardChatMessageKind.Text)
                .OrderByDescending(m => m.CreationTime)
                .Take(MaxChatMessages)
                .ToList();
        }

        if (recent.Count == 0)
        {
            return string.Empty;
        }

        recent.Reverse();
        context.ChatMessageCount = recent.Count;

        var sb = new StringBuilder();
        sb.AppendLine("## 教师讨论（协作频道最近发言，按时间正序）");
        foreach (var m in recent)
        {
            sb.AppendLine($"- {m.SenderName}：{Trim(m.Text, 200)}");
        }

        return sb.ToString();
    }

    private static List<MEAIChatMessage> BuildHistory(List<WhiteboardAssistantTurnDto>? history)
    {
        if (history is not { Count: > 0 })
        {
            return new List<MEAIChatMessage>();
        }

        return history
            .TakeLast(MaxHistoryTurns)
            .Where(t => !string.IsNullOrWhiteSpace(t.Content))
            .Select(t => new MEAIChatMessage(
                string.Equals(t.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? ChatRole.Assistant : ChatRole.User,
                Trim(t.Content, 2000)))
            .ToList();
    }

    /// <summary>把本系统存储的绝对地址裁成相对地址（保留 /uploads/... 之后的部分）。</summary>
    private string ToRelativeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        var selfUrl = (_configuration["App:SelfUrl"] ?? string.Empty).TrimEnd('/');
        if (selfUrl.Length > 0 && url.StartsWith(selfUrl, StringComparison.OrdinalIgnoreCase))
        {
            var rest = url[selfUrl.Length..];
            return rest.StartsWith('/') ? rest : "/" + rest;
        }

        var uploadsIndex = url.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
        return uploadsIndex > 0 ? url[uploadsIndex..] : url;
    }

    // ── 工具 ──────────────────────────────────────────────────────

    /// <summary>从元素 JSON 里取正文（画布元素是前端定义的自由结构，服务端只认 text 字段）。</summary>
    private static string ExtractElementText(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
            {
                return (textEl.GetString() ?? string.Empty).Trim();
            }
        }
        catch (JsonException)
        {
            // 元素内容损坏时跳过，不影响整轮对话
        }

        return string.Empty;
    }

    private static string Trim(string value, int max)
    {
        var text = (value ?? string.Empty).Replace("\r\n", "\n").Trim();
        return text.Length <= max ? text : text[..max] + "…";
    }

    private static string? NormalizeSelection(string? selected)
    {
        var text = (selected ?? string.Empty).Trim();
        return text.Length == 0 ? null : (text.Length > 1200 ? text[..1200] : text);
    }

    /// <summary>只有白板创建者 / 共创成员（含租户管理员）才能用助手，防止越权读白板内容。</summary>
    private async Task<Whiteboard> GetBoardForMemberAsync(Guid boardId, Guid userId)
    {
        Whiteboard board;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            board = await _boardRepository.FindAsync(boardId)
                    ?? throw new UserFriendlyException("白板不存在或已删除。");
        }

        if (board.CreatorId == userId)
        {
            return board;
        }

        WhiteboardMember? member;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _memberRepository.GetQueryableAsync();
            member = query.FirstOrDefault(m => m.WhiteboardId == boardId && m.UserId == userId);
        }

        if (member != null || await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Whiteboards.Manage))
        {
            return board;
        }

        throw new UserFriendlyException("你不是该白板的共创成员，无法使用助手。");
    }
}
