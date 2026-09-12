using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Edition;
using KnowledgeHub.Features;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources;
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

[Authorize(KnowledgeHubPermissions.SpecialEducation.TeachingDesign)]
public class SpecialTeachingDesignAppService : KnowledgeHubAppService, ISpecialTeachingDesignAppService
{
    private readonly IRepository<SpecialTeachingDesign, Guid> _repository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IEditionConfigService _editionConfig;
    private readonly IConfiguration _configuration;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionChecker _permissionChecker;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IRepository<SpecialEduContentVersion, Guid> _versionRepository;

    public SpecialTeachingDesignAppService(
        IRepository<SpecialTeachingDesign, Guid> repository,
        IRepository<Course, Guid> courseRepository,
        IRepository<Resource, Guid> resourceRepository,
        IEditionConfigService editionConfig,
        IConfiguration configuration,
        ICurrentUser currentUser,
        IPermissionChecker permissionChecker,
        IRepository<IdentityUser, Guid> userRepository,
        IRepository<SpecialEduContentVersion, Guid> versionRepository)
    {
        _repository = repository;
        _courseRepository = courseRepository;
        _resourceRepository = resourceRepository;
        _editionConfig = editionConfig;
        _configuration = configuration;
        _currentUser = currentUser;
        _permissionChecker = permissionChecker;
        _userRepository = userRepository;
        _versionRepository = versionRepository;
    }
    protected async Task EnsureEnabledAsync()
    {
        if (!await _editionConfig.IsSpecialEducationEnabledAsync())
        {
            throw new UserFriendlyException("本租户未开通特殊教育模块，请联系全局管理员开通。");
        }
    }

    public async Task<PagedResultDto<SpecialTeachingDesignDto>> GetListAsync(GetTeachingDesignListInputDto input)
    {
        await EnsureEnabledAsync();
        var query = await _repository.GetQueryableAsync();
        if (input.Category.HasValue) query = query.Where(x => x.Category == input.Category.Value);
        if (input.Status.HasValue) query = query.Where(x => x.Status == input.Status.Value);
        if (!input.Keyword.IsNullOrWhiteSpace()) query = query.Where(x => x.Title.Contains(input.Keyword!));
        var total = query.Count();
        var items = query.OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        var dtos = new List<SpecialTeachingDesignDto>();
        foreach (var item in items) dtos.Add(await ToDtoAsync(item));
        return new PagedResultDto<SpecialTeachingDesignDto>(total, dtos);
    }

    public async Task<SpecialTeachingDesignDto> GetAsync(Guid id)
    {
        await EnsureEnabledAsync();
        var entity = await _repository.GetAsync(id);
        return await ToDtoAsync(entity);
    }

    public async Task<SpecialTeachingDesignDto> SaveAsync(SaveTeachingDesignInputDto input)
    {
        await EnsureEnabledAsync();
        var doc = ParseResult(input.ResultJson);
        SpecialTeachingDesign entity;
        bool isNew = !input.Id.HasValue;
        if (input.Id.HasValue)
        {
            entity = await _repository.GetAsync(input.Id.Value);
        }
        else
        {
            entity = new SpecialTeachingDesign(Guid.NewGuid(), CurrentTenant.Id, _currentUser.GetId(), input.Category);
            entity.VersionNumber = 1;
            await _repository.InsertAsync(entity);
        }
        entity.Category = input.Category;
        entity.CourseId = input.CourseId;
        entity.ResourceId = input.ResourceId;
        entity.SourceInputJson = input.SourceInputJson ?? "{}";
        ApplyDoc(entity, doc, input.ResultJson);
        if (entity.Status != SpecialEduPlanStatus.Draft) entity.Status = SpecialEduPlanStatus.Draft;
        await _repository.UpdateAsync(entity);
        if (isNew) await SnapshotAsync(entity, BuildSnapshotJson(entity));
        return await ToDtoAsync(entity);
    }

    /// <summary>
    /// 结构化编辑：先留档当前版本（老数据补 v1），再应用新内容并自动 +1，新状态记为草稿待审。
    /// </summary>
    public async Task<SpecialTeachingDesignDto> UpdateContentAsync(UpdateTeachingDesignContentDto input)
    {
        await EnsureEnabledAsync();
        var entity = await _repository.GetAsync(input.Id);
        if (entity.VersionNumber < 1) entity.VersionNumber = 1;
        await EnsureVersionSnapshotAsync(entity);
        static string SJ(List<string> v) => JsonSerializer.Serialize(v ?? new List<string>());
        entity.Title = input.Title;
        entity.Subject = input.Subject;
        entity.Grade = input.Grade;
        entity.Duration = input.Duration;
        entity.ObjectivesJson = SJ(input.Objectives);
        entity.KeyPointsJson = SJ(input.KeyPoints);
        entity.DifficultiesJson = SJ(input.Difficulties);
        entity.SectionsJson = JsonSerializer.Serialize((input.Sections ?? new List<TeachingSectionInputDto>())
            .Select(s => new TeachingSectionItemDto { Name = s.Name ?? "", Duration = s.Duration, Content = s.Content ?? "" }).ToList());
        entity.MethodsJson = SJ(input.Methods);
        entity.ResourcesJson = SJ(input.Resources);
        entity.AssessmentJson = SJ(input.Assessment);
        entity.HomeworkJson = SJ(input.Homework);
        entity.BoardDesignJson = SJ(input.BoardDesign);
        entity.SlidesOutlineJson = SJ(input.SlidesOutline);
        entity.ActivitiesJson = SJ(input.Activities);
        entity.AssessmentToolsJson = SJ(input.AssessmentTools);
        entity.StandardBasis = input.StandardBasis ?? "";
        entity.RawJson = string.IsNullOrWhiteSpace(input.ResultJson) ? entity.RawJson : input.ResultJson;
        entity.VersionNumber++;
        if (entity.Status != SpecialEduPlanStatus.Draft) entity.Status = SpecialEduPlanStatus.Draft;
        await _repository.UpdateAsync(entity);
        await SnapshotAsync(entity, entity.RawJson);
        return await ToDtoAsync(entity);
    }

    public async Task<List<SpecialEduContentVersionDto>> GetVersionsAsync(Guid id)
    {
        await EnsureEnabledAsync();
        var query = await _versionRepository.GetQueryableAsync();
        var items = query.Where(v => v.ContentType == SpecialEduContentType.TeachingDesign && v.EntityId == id)
            .OrderByDescending(v => v.VersionNumber).ToList();
        var dtos = new List<SpecialEduContentVersionDto>();
        foreach (var v in items) dtos.Add(await ToVersionDtoAsync(v));
        return dtos;
    }

    private async Task EnsureVersionSnapshotAsync(SpecialTeachingDesign entity)
    {
        var exists = await _versionRepository.FirstOrDefaultAsync(
            v => v.ContentType == SpecialEduContentType.TeachingDesign && v.EntityId == entity.Id && v.VersionNumber == entity.VersionNumber);
        if (exists == null) await SnapshotAsync(entity, BuildSnapshotJson(entity));
    }

    /// <summary>用结构化列拼 canonical 快照（camelCase），不依赖 RawJson 原文，保证按字段取历史一定有值。</summary>
    private static string BuildSnapshotJson(SpecialTeachingDesign e)
    {
        static List<string> J(string json) { try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); } catch { return new(); } }
        static List<TeachingSectionItemDto> JS(string json)
        {
            try { return JsonSerializer.Deserialize<List<TeachingSectionItemDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new(); }
            catch { return new(); }
        }
        return JsonSerializer.Serialize(new
        {
            title = e.Title, subject = e.Subject, grade = e.Grade, duration = e.Duration,
            objectives = J(e.ObjectivesJson), keyPoints = J(e.KeyPointsJson), difficulties = J(e.DifficultiesJson),
            sections = JS(e.SectionsJson).Select(s => new { name = s.Name, duration = s.Duration, content = s.Content, activities = s.Activities }).ToList(),
            methods = J(e.MethodsJson), resources = J(e.ResourcesJson), assessment = J(e.AssessmentJson),
            homework = J(e.HomeworkJson), boardDesign = J(e.BoardDesignJson), slidesOutline = J(e.SlidesOutlineJson),
            activities = J(e.ActivitiesJson), assessmentTools = J(e.AssessmentToolsJson), standardBasis = e.StandardBasis
        });
    }

    private async Task SnapshotAsync(SpecialTeachingDesign entity, string snapshotJson)
    {
        await _versionRepository.InsertAsync(new SpecialEduContentVersion(
            Guid.NewGuid(), entity.TenantId, SpecialEduContentType.TeachingDesign, entity.Id, entity.VersionNumber)
        {
            Title = entity.Title,
            SnapshotJson = string.IsNullOrWhiteSpace(snapshotJson) ? "{}" : snapshotJson
        });
    }

    private async Task<SpecialEduContentVersionDto> ToVersionDtoAsync(SpecialEduContentVersion v)
    {
        string? creatorName = null;
        if (v.CreatorId.HasValue)
        {
            var u = await _userRepository.FindAsync(v.CreatorId.Value);
            if (u != null) creatorName = !u.Name.IsNullOrEmpty() ? u.Name : u.UserName;
        }
        return new SpecialEduContentVersionDto
        {
            Id = v.Id, ContentType = v.ContentType, EntityId = v.EntityId,
            VersionNumber = v.VersionNumber, Title = v.Title, SnapshotJson = v.SnapshotJson,
            CreatorId = v.CreatorId, CreatorName = creatorName, CreationTime = v.CreationTime
        };
    }

    public async Task<SpecialTeachingDesignDto> SubmitForReviewAsync(SubmitTeachingDesignForReviewInputDto input)
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
    public async Task<SpecialTeachingDesignDto> ReviewAsync(ReviewTeachingDesignInputDto input)
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

    /// <summary>流式生成：参考 LessonPlanAppService，Controller 直接注入本类（不经接口代理）。</summary>
    // Func 回调无法绑定为 HTTP 参数，必须对 Conventional Controller 隐藏，否则 /Abp/ServiceProxyScript 全站 500。
    [Volo.Abp.RemoteService(false)]
    public async Task GenerateStreamingAsync(GenerateTeachingDesignInputDto input, Func<ChatMessageChunkDto, Task> onChunk)
    {
        var threadId = Guid.NewGuid().ToString();
        if (!await _editionConfig.IsSpecialEducationEnabledAsync())
        {
            await EmitErrorAsync(onChunk, threadId, "本租户未开通特殊教育模块。");
            return;
        }

        string? resourceSummary = null;
        string? resourceName = null;
        if (input.ResourceId.HasValue)
        {
            var resource = await _resourceRepository.FindAsync(input.ResourceId.Value);
            if (resource != null) { resourceSummary = resource.Summary; resourceName = resource.Name; }
        }
        string? courseTitle = null;
        if (input.CourseId.HasValue)
        {
            var course = await _courseRepository.FindAsync(input.CourseId.Value);
            if (course != null) courseTitle = course.Title;
        }

        var apiKey = _configuration["Qwen:ApiKey"];
        if (apiKey.IsNullOrWhiteSpace())
        {
            await EmitErrorAsync(onChunk, threadId, "Qwen:ApiKey 未配置。");
            return;
        }
        var baseUrl = _configuration["Qwen:BaseUrl"] ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:Model"] ?? "qwen-flash";

        var userPrompt = $@"## 特殊教育类别：{SpecialEduCategoryNames.ToDisplayName(input.Category)}
{SpecialEduPromptBuilder.CategoryAdaptation(input.Category)}

## 教学目标：{input.Objectives}
## 学生特点：{input.StudentTraits}
## 教学条件：{input.Conditions}
## 课程主题：{input.Topic}
## 学科：{input.Subject} / 学段：{input.Grade} / 课时：{input.Duration} 分钟
{(courseTitle != null ? $"## 关联课程：{courseTitle}\n" : "")}
{(resourceSummary != null ? $"## 课程资源摘要（{resourceName}）：\n{resourceSummary}\n" : "")}
{(input.CustomPrompt.IsNullOrWhiteSpace() ? "" : $"## 教师附加要求：\n{input.CustomPrompt}\n")}
请按 SystemPrompt JSON 结构输出。";

        IChatClient chatClient = QwenClient.CreateChatClient(_configuration, model);
        var messages = new List<ChatMessage> { new(ChatRole.User, userPrompt) };
        var options = new ChatOptions { Instructions = SpecialEduPromptBuilder.TeachingDesignInstructions };
        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, options, CancellationToken.None))
        {
            if (!update.Text.IsNullOrEmpty())
            {
                await onChunk(new ChatMessageChunkDto { Content = update.Text, ThreadId = threadId, IsComplete = false });
            }
        }
        await onChunk(new ChatMessageChunkDto { Content = "", ThreadId = threadId, IsComplete = true });
    }

    public byte[] ExportDocx(string resultJson)
    {
        var doc = ParseResult(resultJson);
        return SpecialEduDocxGenerator.GenerateTeachingDesign(doc);
    }

    private static async Task EmitErrorAsync(Func<ChatMessageChunkDto, Task> onChunk, string threadId, string message)
    {
        await onChunk(new ChatMessageChunkDto { Content = JsonSerializer.Serialize(new { error = message }), ThreadId = threadId, IsComplete = false });
        await onChunk(new ChatMessageChunkDto { Content = "", ThreadId = threadId, IsComplete = true });
    }

    private static SpecialTeachingDesignParseResult ParseResult(string json)
    {
        var clean = ExtractJson(json);
        using var doc = JsonDocument.Parse(clean);
        var root = doc.RootElement;
        static List<string> StrArr(JsonElement r, string name)
        {
            if (!r.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array) return new List<string>();
            return el.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()! : e.ToString()).ToList();
        }
        var sections = new List<TeachingSectionItemDto>();
        if (root.TryGetProperty("sections", out var sEl) && sEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in sEl.EnumerateArray())
            {
                sections.Add(new TeachingSectionItemDto
                {
                    Name = s.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    Duration = s.TryGetProperty("duration", out var d) && d.TryGetInt32(out var di) ? di : 0,
                    Content = s.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "",
                    Activities = s.TryGetProperty("activities", out var a) && a.ValueKind == JsonValueKind.Array
                        ? a.EnumerateArray().Select(e => e.GetString() ?? "").ToList() : new List<string>()
                });
            }
        }
        static string Str(JsonElement r, string name) => r.TryGetProperty(name, out var e) ? e.GetString() ?? e.ToString() : "";
        static int Int(JsonElement r, string name, int def) => r.TryGetProperty(name, out var e) && e.TryGetInt32(out var v) ? v : def;
        return new SpecialTeachingDesignParseResult
        {
            Title = Str(root, "title"), Subject = Str(root, "subject"), Grade = Str(root, "grade"),
            Duration = Int(root, "duration", 45),
            Objectives = StrArr(root, "objectives"), KeyPoints = StrArr(root, "keyPoints"),
            Difficulties = StrArr(root, "difficulties"), Methods = StrArr(root, "methods"),
            Resources = StrArr(root, "resources"), Assessment = StrArr(root, "assessment"),
            Homework = StrArr(root, "homework"), BoardDesign = StrArr(root, "boardDesign"),
            SlidesOutline = StrArr(root, "slidesOutline"), Activities = StrArr(root, "activities"),
            AssessmentTools = StrArr(root, "assessmentTools"),
            StandardBasis = Str(root, "standardBasis"), Sections = sections
        };
    }

    /// <summary>AI 输出常带开场白/代码围栏，提取最外层 {…} 再解析。</summary>
    internal static string ExtractJson(string json)
    {
        var clean = (json ?? "").Trim();
        if (clean.StartsWith("```"))
        {
            var idx = clean.IndexOf('\n');
            if (idx >= 0) clean = clean[(idx + 1)..];
            if (clean.EndsWith("```")) clean = clean[..^3].TrimEnd();
        }
        var start = clean.IndexOf('{');
        var end = clean.LastIndexOf('}');
        if (start >= 0 && end > start) clean = clean[start..(end + 1)];
        return clean;
    }

    private static void ApplyDoc(SpecialTeachingDesign entity, SpecialTeachingDesignParseResult doc, string rawJson)
    {
        entity.Title = doc.Title; entity.Subject = doc.Subject; entity.Grade = doc.Grade; entity.Duration = doc.Duration;
        entity.ObjectivesJson = JsonSerializer.Serialize(doc.Objectives);
        entity.KeyPointsJson = JsonSerializer.Serialize(doc.KeyPoints);
        entity.DifficultiesJson = JsonSerializer.Serialize(doc.Difficulties);
        entity.SectionsJson = JsonSerializer.Serialize(doc.Sections);
        entity.MethodsJson = JsonSerializer.Serialize(doc.Methods);
        entity.ResourcesJson = JsonSerializer.Serialize(doc.Resources);
        entity.AssessmentJson = JsonSerializer.Serialize(doc.Assessment);
        entity.HomeworkJson = JsonSerializer.Serialize(doc.Homework);
        entity.BoardDesignJson = JsonSerializer.Serialize(doc.BoardDesign);
        entity.SlidesOutlineJson = JsonSerializer.Serialize(doc.SlidesOutline);
        entity.ActivitiesJson = JsonSerializer.Serialize(doc.Activities);
        entity.AssessmentToolsJson = JsonSerializer.Serialize(doc.AssessmentTools);
        entity.StandardBasis = doc.StandardBasis;
        entity.RawJson = rawJson;
    }

    private async Task<SpecialTeachingDesignDto> ToDtoAsync(SpecialTeachingDesign e)
    {
        static List<string> J(string json) { try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); } catch { return new(); } }
        static List<TeachingSectionItemDto> JS(string json) { try { return JsonSerializer.Deserialize<List<TeachingSectionItemDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new(); } catch { return new(); } }
        string? reviewerName = null;
        if (e.ReviewerUserId.HasValue)
        {
            var reviewer = await _userRepository.FindAsync(e.ReviewerUserId.Value);
            if (reviewer != null) reviewerName = !reviewer.Name.IsNullOrEmpty() ? reviewer.Name : reviewer.UserName;
        }
        return new SpecialTeachingDesignDto
        {
            Id = e.Id, Category = e.Category, CategoryName = SpecialEduCategoryNames.ToDisplayName(e.Category),
            CourseId = e.CourseId, ResourceId = e.ResourceId,
            Title = e.Title, Subject = e.Subject, Grade = e.Grade, Duration = e.Duration,
            Objectives = J(e.ObjectivesJson), KeyPoints = J(e.KeyPointsJson), Difficulties = J(e.DifficultiesJson),
            Sections = JS(e.SectionsJson), Methods = J(e.MethodsJson), Resources = J(e.ResourcesJson),
            Assessment = J(e.AssessmentJson), Homework = J(e.HomeworkJson), BoardDesign = J(e.BoardDesignJson),
            SlidesOutline = J(e.SlidesOutlineJson), Activities = J(e.ActivitiesJson), AssessmentTools = J(e.AssessmentToolsJson),
            StandardBasis = e.StandardBasis, RawJson = e.RawJson, Status = e.Status, ReviewComment = e.ReviewComment,
            ReviewerUserId = e.ReviewerUserId, ReviewerName = reviewerName, VersionNumber = e.VersionNumber,
            CreationTime = e.CreationTime, CreatorId = e.CreatorId
        };
    }

    public class SpecialTeachingDesignParseResult
    {
        public string Title { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Grade { get; set; } = "";
        public int Duration { get; set; } = 45;
        public List<string> Objectives { get; set; } = new();
        public List<string> KeyPoints { get; set; } = new();
        public List<string> Difficulties { get; set; } = new();
        public List<TeachingSectionItemDto> Sections { get; set; } = new();
        public List<string> Methods { get; set; } = new();
        public List<string> Resources { get; set; } = new();
        public List<string> Assessment { get; set; } = new();
        public List<string> Homework { get; set; } = new();
        public List<string> BoardDesign { get; set; } = new();
        public List<string> SlidesOutline { get; set; } = new();
        public List<string> Activities { get; set; } = new();
        public List<string> AssessmentTools { get; set; } = new();
        public string StandardBasis { get; set; } = "";
    }
}
