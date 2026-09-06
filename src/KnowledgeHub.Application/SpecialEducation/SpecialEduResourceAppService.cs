using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Edition;
using KnowledgeHub.Permissions;
using KnowledgeHub.SpecialEducation;
using KnowledgeHub.SpecialEducation.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace KnowledgeHub.Application.SpecialEducation;

[Authorize(KnowledgeHubPermissions.SpecialEducation.Resource)]
public class SpecialEduResourceAppService : KnowledgeHubAppService, ISpecialEduResourceAppService
{
    private readonly IRepository<SpecialEduResource, Guid> _repository;
    private readonly IEditionConfigService _editionConfig;
    private readonly IConfiguration _configuration;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionChecker _permissionChecker;
    private readonly IRepository<IdentityUser, Guid> _userRepository;

    public SpecialEduResourceAppService(
        IRepository<SpecialEduResource, Guid> repository,
        IEditionConfigService editionConfig,
        IConfiguration configuration,
        ICurrentUser currentUser,
        IPermissionChecker permissionChecker,
        IRepository<IdentityUser, Guid> userRepository)
    {
        _repository = repository;
        _editionConfig = editionConfig;
        _configuration = configuration;
        _currentUser = currentUser;
        _permissionChecker = permissionChecker;
        _userRepository = userRepository;
    }

    protected async Task EnsureEnabledAsync()
    {
        if (!await _editionConfig.IsSpecialEducationEnabledAsync())
            throw new UserFriendlyException("本租户未开通特殊教育模块，请联系全局管理员开通。");
    }

    public async Task<PagedResultDto<SpecialEduResourceDto>> GetListAsync(GetSpecialResourceListInputDto input)
    {
        await EnsureEnabledAsync();
        var query = await _repository.GetQueryableAsync();
        if (input.Category.HasValue) query = query.Where(x => x.Category == input.Category.Value);
        if (!input.Modality.IsNullOrWhiteSpace()) query = query.Where(x => x.Modality == input.Modality);
        if (input.Status.HasValue) query = query.Where(x => x.Status == input.Status.Value);
        if (!input.Keyword.IsNullOrWhiteSpace()) query = query.Where(x => x.Title.Contains(input.Keyword!));
        var total = query.Count();
        var items = query.OrderByDescending(x => x.CreationTime).Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        var dtos = new List<SpecialEduResourceDto>();
        foreach (var item in items) dtos.Add(await ToDtoAsync(item));
        return new PagedResultDto<SpecialEduResourceDto>(total, dtos);
    }

    public async Task<SpecialEduResourceDto> GetAsync(Guid id)
    {
        await EnsureEnabledAsync();
        return await ToDtoAsync(await _repository.GetAsync(id));
    }

    public async Task<SpecialEduResourceDto> SaveAsync(SaveSpecialResourceInputDto input)
    {
        await EnsureEnabledAsync();
        if (!SpecialEduResourceModality.All.Contains(input.Modality))
            throw new UserFriendlyException($"不支持的资源类型：{input.Modality}");
        var doc = ParseResult(input.ResultJson);
        SpecialEduResource entity;
        if (input.Id.HasValue) entity = await _repository.GetAsync(input.Id.Value);
        else
        {
            entity = new SpecialEduResource(Guid.NewGuid(), CurrentTenant.Id, _currentUser.GetId(), input.Category, input.Modality);
            await _repository.InsertAsync(entity);
        }
        entity.Title = string.IsNullOrWhiteSpace(input.Title) ? doc.Title : input.Title;
        entity.Category = input.Category;
        entity.Modality = input.Modality;
        entity.TeachingDesignId = input.TeachingDesignId;
        entity.IepPlanId = input.IepPlanId;
        entity.CourseId = input.CourseId;
        entity.ContentJson = JsonSerializer.Serialize(doc.Content);
        entity.RawJson = input.ResultJson;
        entity.SourceInputJson = input.SourceInputJson ?? "{}";
        if (entity.Status != SpecialEduPlanStatus.Draft) entity.Status = SpecialEduPlanStatus.Draft;
        await _repository.UpdateAsync(entity);
        return await ToDtoAsync(entity);
    }

    public async Task<SpecialEduResourceDto> SubmitForReviewAsync(SubmitSpecialResourceForReviewInputDto input)
    {
        await EnsureEnabledAsync();
        var entity = await _repository.GetAsync(input.Id);
        entity.ReviewerUserId = input.ReviewerUserId;
        entity.Status = SpecialEduPlanStatus.PendingReview;
        entity.ReviewComment = null;
        await _repository.UpdateAsync(entity);
        return await ToDtoAsync(entity);
    }

    /// <summary>
    /// 审核：指派的审核教师本人，或持有 Review 权限的账号可审核。审核通过 → 已发布。
    /// </summary>
    public async Task<SpecialEduResourceDto> ReviewAsync(ReviewSpecialResourceInputDto input)
    {
        await EnsureEnabledAsync();
        var entity = await _repository.GetAsync(input.Id);
        var isReviewer = entity.ReviewerUserId.HasValue && entity.ReviewerUserId.Value == _currentUser.GetId();
        if (!isReviewer && !await _permissionChecker.IsGrantedAsync(KnowledgeHubPermissions.SpecialEducation.Review))
        {
            throw new UserFriendlyException("只有指派的审核教师或有审核权限的账号可以审核。");
        }
        entity.Status = input.Approved ? SpecialEduPlanStatus.Published : SpecialEduPlanStatus.Draft;
        entity.ReviewComment = input.Comment;
        await _repository.UpdateAsync(entity);
        return await ToDtoAsync(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        await EnsureEnabledAsync();
        await _repository.DeleteAsync(id);
    }

    public async Task GenerateStreamingAsync(GenerateSpecialResourceInputDto input, Func<ChatMessageChunkDto, Task> onChunk)
    {
        var threadId = Guid.NewGuid().ToString();
        if (!await _editionConfig.IsSpecialEducationEnabledAsync())
        {
            await EmitErrorAsync(onChunk, threadId, "本租户未开通特殊教育模块。");
            return;
        }
        if (!SpecialEduResourceModality.All.Contains(input.Modality))
        {
            await EmitErrorAsync(onChunk, threadId, $"不支持的资源类型：{input.Modality}");
            return;
        }
        var apiKey = _configuration["Qwen:ApiKey"];
        if (apiKey.IsNullOrWhiteSpace()) { await EmitErrorAsync(onChunk, threadId, "Qwen:ApiKey 未配置。"); return; }
        var baseUrl = _configuration["Qwen:BaseUrl"] ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:Model"] ?? "qwen-plus";

        var userPrompt = $@"## 特殊教育类别：{SpecialEduCategoryNames.ToDisplayName(input.Category)}
{SpecialEduPromptBuilder.CategoryAdaptation(input.Category)}
## 资源类型：{input.Modality}
## 主题：{input.Topic}
## 学生特点：{input.StudentTraits}
{(input.CustomPrompt.IsNullOrWhiteSpace() ? "" : $"## 定制要求：\n{input.CustomPrompt}\n")}
请按 SystemPrompt JSON 结构输出。";

        var openaiClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });
        IChatClient chatClient = openaiClient.GetChatClient(model).AsIChatClient();
        var messages = new List<ChatMessage> { new(ChatRole.User, userPrompt) };
        var options = new ChatOptions { Instructions = SpecialEduPromptBuilder.ResourceInstructions(input.Modality) };
        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, options, CancellationToken.None))
        {
            if (!update.Text.IsNullOrEmpty())
                await onChunk(new ChatMessageChunkDto { Content = update.Text, ThreadId = threadId, IsComplete = false });
        }
        await onChunk(new ChatMessageChunkDto { Content = "", ThreadId = threadId, IsComplete = true });
    }

    public byte[] ExportDocx(string resultJson, string modality)
    {
        var doc = ParseResult(resultJson);
        return SpecialEduDocxGenerator.GenerateResource(doc.Title, doc.Content, modality);
    }

    public async Task<byte[]> BatchExportAsync(List<Guid> ids)
    {
        await EnsureEnabledAsync();
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var id in ids.Take(50))
            {
                var entity = await _repository.FindAsync(id);
                if (entity == null) continue;
                var doc = ParseResult(entity.RawJson);
                var bytes = SpecialEduDocxGenerator.GenerateResource(doc.Title, doc.Content, entity.Modality);
                var entry = zip.CreateEntry($"{entity.Modality}_{SafeFileName(entity.Title)}.docx");
                using var s = entry.Open();
                await s.WriteAsync(bytes);
            }
        }
        return ms.ToArray();
    }

    private static string SafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "未命名" : name[..Math.Min(name.Length, 60)];
    }

    private static async Task EmitErrorAsync(Func<ChatMessageChunkDto, Task> onChunk, string threadId, string message)
    {
        await onChunk(new ChatMessageChunkDto { Content = JsonSerializer.Serialize(new { error = message }), ThreadId = threadId, IsComplete = false });
        await onChunk(new ChatMessageChunkDto { Content = "", ThreadId = threadId, IsComplete = true });
    }

    internal static (string Title, List<string> Content) ParseResult(string json)
    {
        var clean = json.Trim();
        if (clean.StartsWith("```")) { var idx = clean.IndexOf('\n'); if (idx >= 0) clean = clean[(idx + 1)..]; if (clean.EndsWith("```")) clean = clean[..^3].TrimEnd(); }
        using var doc = JsonDocument.Parse(clean);
        var root = doc.RootElement;
        var title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        var content = new List<string>();
        if (root.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.Array)
            content = c.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()! : e.ToString()).ToList();
        return (title, content);
    }

    private async Task<SpecialEduResourceDto> ToDtoAsync(SpecialEduResource e)
    {
        List<string> content = new();
        try
        {
            content = JsonSerializer.Deserialize<List<string>>(e.ContentJson) ?? new();
        }
        catch { }
        string? reviewerName = null;
        if (e.ReviewerUserId.HasValue)
        {
            var reviewer = await _userRepository.FindAsync(e.ReviewerUserId.Value);
            if (reviewer != null) reviewerName = !reviewer.Name.IsNullOrEmpty() ? reviewer.Name : reviewer.UserName;
        }
        return new SpecialEduResourceDto
        {
            Id = e.Id, Title = e.Title, Category = e.Category,
            CategoryName = SpecialEduCategoryNames.ToDisplayName(e.Category),
            Modality = e.Modality, ModalityName = ModalityDisplayName(e.Modality),
            TeachingDesignId = e.TeachingDesignId, IepPlanId = e.IepPlanId, CourseId = e.CourseId,
            ContentText = string.Join("\n", content), RawJson = e.RawJson,
            Status = e.Status, ReviewComment = e.ReviewComment,
            ReviewerUserId = e.ReviewerUserId, ReviewerName = reviewerName,
            CreationTime = e.CreationTime, CreatorId = e.CreatorId
        };
    }

    public static string ModalityDisplayName(string modality) => modality switch
    {
        SpecialEduResourceModality.Text => "文本资源",
        SpecialEduResourceModality.ImageDesc => "图片描述",
        SpecialEduResourceModality.AudioScript => "音频脚本",
        SpecialEduResourceModality.VideoScript => "视频脚本",
        SpecialEduResourceModality.SocialStory => "社交故事",
        SpecialEduResourceModality.VisualSupport => "视觉支持材料",
        SpecialEduResourceModality.BehaviorPlan => "行为干预方案",
        _ => modality
    };
}
