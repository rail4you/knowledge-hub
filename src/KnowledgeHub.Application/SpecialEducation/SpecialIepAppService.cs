using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Edition;
using KnowledgeHub.Learning;
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

[Authorize(KnowledgeHubPermissions.SpecialEducation.IEP)]
public class SpecialIepAppService : KnowledgeHubAppService, ISpecialIepAppService
{
    private readonly IRepository<IepPlan, Guid> _repository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<StudentCourse, Guid> _enrollmentRepository;
    private readonly IEditionConfigService _editionConfig;
    private readonly IConfiguration _configuration;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionChecker _permissionChecker;
    private readonly IRepository<IdentityUser, Guid> _userRepository;

    public SpecialIepAppService(
        IRepository<IepPlan, Guid> repository,
        IRepository<Course, Guid> courseRepository,
        IRepository<StudentCourse, Guid> enrollmentRepository,
        IEditionConfigService editionConfig,
        IConfiguration configuration,
        ICurrentUser currentUser,
        IPermissionChecker permissionChecker,
        IRepository<IdentityUser, Guid> userRepository)
    {
        _repository = repository;
        _courseRepository = courseRepository;
        _enrollmentRepository = enrollmentRepository;
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

    public async Task<PagedResultDto<IepPlanDto>> GetListAsync(GetIepListInputDto input)
    {
        await EnsureEnabledAsync();
        var query = await _repository.GetQueryableAsync();
        if (input.Category.HasValue) query = query.Where(x => x.Category == input.Category.Value);
        if (input.Status.HasValue) query = query.Where(x => x.Status == input.Status.Value);
        if (input.StudentUserId.HasValue) query = query.Where(x => x.StudentUserId == input.StudentUserId.Value);
        if (!input.Keyword.IsNullOrWhiteSpace()) query = query.Where(x => x.StudentName.Contains(input.Keyword!));
        var total = query.Count();
        var items = query.OrderByDescending(x => x.CreationTime).Skip(input.SkipCount).Take(input.MaxResultCount).ToList();
        await FillCourseTitlesAsync(items);
        var dtos = new List<IepPlanDto>();
        foreach (var item in items) dtos.Add(await ToDtoAsync(item));
        return new PagedResultDto<IepPlanDto>(total, dtos);
    }

    /// <summary>学生端：只返回当前登录学生的 IEP。</summary>
    public async Task<PagedResultDto<IepPlanDto>> GetMyIepListAsync(GetIepListInputDto input)
    {
        await EnsureEnabledAsync();
        var myId = _currentUser.GetId();
        input.StudentUserId = myId;
        return await GetListAsync(input);
    }

    public async Task<IepPlanDto> GetAsync(Guid id)
    {
        await EnsureEnabledAsync();
        var entity = await _repository.GetAsync(id);
        if (IsStudent() && entity.StudentUserId != _currentUser.GetId())
            throw new UserFriendlyException("只能查看自己的 IEP。");
        return await ToDtoAsync(entity);
    }

    public async Task<IepPlanDto> SaveAsync(SaveIepInputDto input)
    {
        await EnsureEnabledAsync();
        var doc = ParseResult(input.ResultJson);
        IepPlan entity;
        if (input.Id.HasValue)
        {
            entity = await _repository.GetAsync(input.Id.Value);
        }
        else
        {
            entity = new IepPlan(Guid.NewGuid(), CurrentTenant.Id, input.StudentUserId, input.Category);
            await _repository.InsertAsync(entity);
        }
        entity.StudentUserId = input.StudentUserId;
        entity.StudentName = input.StudentName;
        entity.Category = input.Category;
        entity.CourseId = input.CourseId;
        entity.SourceInputJson = input.SourceInputJson ?? "{}";
        ApplyDoc(entity, doc, input.ResultJson);
        if (entity.Status != SpecialEduPlanStatus.Draft) entity.Status = SpecialEduPlanStatus.Draft;
        await _repository.UpdateAsync(entity);
        return await ToDtoAsync(entity);
    }

    public async Task<IepPlanDto> CreateRevisionAsync(Guid id)
    {
        await EnsureEnabledAsync();
        var src = await _repository.GetAsync(id);
        var revision = new IepPlan(Guid.NewGuid(), src.TenantId, src.StudentUserId, src.Category)
        {
            StudentName = src.StudentName,
            CourseId = src.CourseId,
            ProfileJson = src.ProfileJson,
            LongTermGoalsJson = src.LongTermGoalsJson,
            ShortTermGoalsJson = src.ShortTermGoalsJson,
            StrategiesJson = src.StrategiesJson,
            EvaluationJson = src.EvaluationJson,
            HomeSchoolJson = src.HomeSchoolJson,
            LegalBasis = src.LegalBasis,
            RawJson = src.RawJson,
            SourceInputJson = src.SourceInputJson,
            VersionNumber = src.VersionNumber + 1,
            ParentVersionId = src.Id,
            Status = SpecialEduPlanStatus.Draft
        };
        await _repository.InsertAsync(revision);
        return await ToDtoAsync(revision);
    }

    public async Task<IepPlanDto> SubmitForReviewAsync(SubmitIepForReviewInputDto input)
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
    public async Task<IepPlanDto> ReviewAsync(ReviewIepInputDto input)
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

    /// <summary>流式生成 IEP：手动评估表单 + 选课关联快照作为输入。</summary>
    public async Task GenerateStreamingAsync(GenerateIepInputDto input, Func<ChatMessageChunkDto, Task> onChunk)
    {
        var threadId = Guid.NewGuid().ToString();
        if (!await _editionConfig.IsSpecialEducationEnabledAsync())
        {
            await EmitErrorAsync(onChunk, threadId, "本租户未开通特殊教育模块。");
            return;
        }
        var apiKey = _configuration["Qwen:ApiKey"];
        if (apiKey.IsNullOrWhiteSpace()) { await EmitErrorAsync(onChunk, threadId, "Qwen:ApiKey 未配置。"); return; }
        var baseUrl = _configuration["Qwen:BaseUrl"] ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:Model"] ?? "qwen-plus";

        string enrollmentSnapshot = "无选课记录";
        string? courseTitle = null;
        if (input.CourseId.HasValue)
        {
            var course = await _courseRepository.FindAsync(input.CourseId.Value);
            if (course != null) courseTitle = course.Title;
            var enrollments = await _enrollmentRepository.GetListAsync(x => x.CourseId == input.CourseId.Value && x.StudentId == input.StudentUserId);
            var en = enrollments.FirstOrDefault();
            if (en != null) enrollmentSnapshot = $"已选课：{courseTitle}，状态={en.Status}，进度={en.Progress}%";
            else if (courseTitle != null) enrollmentSnapshot = $"关联课程：{courseTitle}（该生尚未选课）";
        }

        var userPrompt = $@"## 学生：{input.StudentName}
## 特殊教育类别：{SpecialEduCategoryNames.ToDisplayName(input.Category)}
{SpecialEduPromptBuilder.CategoryAdaptation(input.Category)}

## 评估数据（手动表单）：{input.AssessmentData}
## 当前发展水平：{input.CurrentLevel}
## 家庭需求：{input.FamilyNeeds}
## 选课关联：{enrollmentSnapshot}
{(input.CustomPrompt.IsNullOrWhiteSpace() ? "" : $"## 教师附加要求：\n{input.CustomPrompt}\n")}
请按 SystemPrompt JSON 结构输出 IEP。";

        var openaiClient = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });
        IChatClient chatClient = openaiClient.GetChatClient(model).AsIChatClient();
        var messages = new List<ChatMessage> { new(ChatRole.User, userPrompt) };
        var options = new ChatOptions { Instructions = SpecialEduPromptBuilder.IepInstructions };
        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, options, CancellationToken.None))
        {
            if (!update.Text.IsNullOrEmpty())
                await onChunk(new ChatMessageChunkDto { Content = update.Text, ThreadId = threadId, IsComplete = false });
        }
        await onChunk(new ChatMessageChunkDto { Content = "", ThreadId = threadId, IsComplete = true });
    }

    public byte[] ExportDocx(string resultJson, string studentName)
    {
        var doc = ParseResult(resultJson);
        return SpecialEduDocxGenerator.GenerateIep(doc, studentName);
    }

    private bool IsStudent() => _currentUser.Roles?.Contains("Student") == true;

    private async Task FillCourseTitlesAsync(List<IepPlan> items) => await Task.CompletedTask;

    private static async Task EmitErrorAsync(Func<ChatMessageChunkDto, Task> onChunk, string threadId, string message)
    {
        await onChunk(new ChatMessageChunkDto { Content = JsonSerializer.Serialize(new { error = message }), ThreadId = threadId, IsComplete = false });
        await onChunk(new ChatMessageChunkDto { Content = "", ThreadId = threadId, IsComplete = true });
    }

    public static IepParseResult ParseResult(string json)
    {
        var clean = json.Trim();
        if (clean.StartsWith("```")) { var idx = clean.IndexOf('\n'); if (idx >= 0) clean = clean[(idx + 1)..]; if (clean.EndsWith("```")) clean = clean[..^3].TrimEnd(); }
        using var doc = JsonDocument.Parse(clean);
        var root = doc.RootElement;
        static List<string> Arr(JsonElement r, string name)
        {
            if (!r.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array) return new List<string>();
            return el.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()! : e.ToString()).ToList();
        }
        static string Str(JsonElement r, string name) => r.TryGetProperty(name, out var e) ? e.GetString() ?? e.ToString() : "";
        return new IepParseResult
        {
            ProfileSummary = Str(root, "profileSummary"), LongTermGoals = Arr(root, "longTermGoals"),
            ShortTermGoals = Arr(root, "shortTermGoals"), Strategies = Arr(root, "strategies"),
            Evaluation = Arr(root, "evaluation"), HomeSchool = Arr(root, "homeSchool"), LegalBasis = Str(root, "legalBasis")
        };
    }

    private static void ApplyDoc(IepPlan entity, IepParseResult doc, string rawJson)
    {
        entity.ProfileJson = JsonSerializer.Serialize(new { summary = doc.ProfileSummary });
        entity.LongTermGoalsJson = JsonSerializer.Serialize(doc.LongTermGoals);
        entity.ShortTermGoalsJson = JsonSerializer.Serialize(doc.ShortTermGoals);
        entity.StrategiesJson = JsonSerializer.Serialize(doc.Strategies);
        entity.EvaluationJson = JsonSerializer.Serialize(doc.Evaluation);
        entity.HomeSchoolJson = JsonSerializer.Serialize(doc.HomeSchool);
        entity.LegalBasis = doc.LegalBasis;
        entity.RawJson = rawJson;
    }

    private async Task<IepPlanDto> ToDtoAsync(IepPlan e)
    {
        static List<string> J(string json) { try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); } catch { return new(); } }
        string profile = e.ProfileJson;
        try { using var d = JsonDocument.Parse(e.ProfileJson); if (d.RootElement.TryGetProperty("summary", out var s)) profile = s.GetString() ?? profile; } catch { }
        string? reviewerName = null;
        if (e.ReviewerUserId.HasValue)
        {
            var reviewer = await _userRepository.FindAsync(e.ReviewerUserId.Value);
            if (reviewer != null) reviewerName = !reviewer.Name.IsNullOrEmpty() ? reviewer.Name : reviewer.UserName;
        }
        return new IepPlanDto
        {
            Id = e.Id, StudentUserId = e.StudentUserId, StudentName = e.StudentName,
            Category = e.Category, CategoryName = SpecialEduCategoryNames.ToDisplayName(e.Category),
            CourseId = e.CourseId, ProfileSummary = profile,
            LongTermGoals = J(e.LongTermGoalsJson), ShortTermGoals = J(e.ShortTermGoalsJson),
            Strategies = J(e.StrategiesJson), Evaluation = J(e.EvaluationJson), HomeSchool = J(e.HomeSchoolJson),
            LegalBasis = e.LegalBasis, RawJson = e.RawJson, VersionNumber = e.VersionNumber,
            ParentVersionId = e.ParentVersionId, Status = e.Status, ReviewComment = e.ReviewComment,
            ReviewerUserId = e.ReviewerUserId, ReviewerName = reviewerName,
            CreationTime = e.CreationTime, CreatorId = e.CreatorId
        };
    }

    public class IepParseResult
    {
        public string ProfileSummary { get; set; } = "";
        public List<string> LongTermGoals { get; set; } = new();
        public List<string> ShortTermGoals { get; set; } = new();
        public List<string> Strategies { get; set; } = new();
        public List<string> Evaluation { get; set; } = new();
        public List<string> HomeSchool { get; set; } = new();
        public string LegalBasis { get; set; } = "";
    }
}
