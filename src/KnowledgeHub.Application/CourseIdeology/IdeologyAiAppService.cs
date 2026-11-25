using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI;
using KnowledgeHub.CourseIdeology.Dtos;
using KnowledgeHub.CourseIdeology.Enums;
using KnowledgeHub.Courses;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.CourseIdeology;

/// <summary>
/// 课程思政 · AI 生成案例草稿（**同步**返回，教师确认后才落库）。
///
/// <para>为什么不用异步 AI 任务：单个案例是一次短调用（一次 LLM 请求），
/// 老师等 5~15 秒就能拿到草稿继续编辑，比"建任务 → 轮询 → 回列表找结果"体验直接得多；
/// 留痕通过案例上的 <c>AiGenerated</c> + <c>AiMaterial</c>（素材快照）完成。</para>
///
/// <para>合规底线：**必须由教师提供素材**（AI 不许凭空编事实/编数据），
/// 输出统一打「待核验」提醒，且生成的案例只会进入表单供教师核对后再保存。</para>
/// </summary>
[Authorize(KnowledgeHubPermissions.CourseIdeology.AiGenerate)]
public class IdeologyAiAppService : KnowledgeHubAppService, IIdeologyAiAppService
{
    private const int MaxMaterialChars = 3000;
    private const int MaxOutputTokens = 2048;

    private const string Instructions = """
你是中国高校「课程思政」教学案例设计的资深教研员。你的任务是：**根据教师提供的素材**，
为指定的课程章节写一小段可直接进课堂的思政案例文本，输出严格 JSON（不要 markdown、不要解释）。

硬性要求：
1. 只能使用教师提供的素材与可公开核查的常识性事实；**不得虚构人名、机构、时间、数字、政策条文**。
   素材里没有的信息，宁可不写，也不许编。
2. 案例文本要写成**可以直接讲给学生听/读的连贯段落**（300~500 字），
   先讲清楚事情本身，再自然落到专业与价值层面，**避免生硬说教与口号**。
3. 必须与本章节的知识点自然衔接：让老师能一句话接上"我们这门课正好在这讲……"。
4. 语言面向中国大学生，具体、可感、有细节。
5. sourceText 回填教师给出的出处；教师没给就留空字符串（出处是选填项）。

输出 JSON 结构（字段名必须完全一致）：
{
  "title": "案例标题（≤40 字，不要带书名号）",
  "contentJson": { "brief": "案例文本（300~500 字，连贯段落）" },
  "sourceText": "出处（可空）"
}
""";

    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<IdeologyElement, Guid> _elementRepository;
    private readonly IConfiguration _configuration;
    private readonly IAiQuotaService _quotaService;
    private readonly IAiUsageTracker _usageTracker;

    public IdeologyAiAppService(
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<IdeologyElement, Guid> elementRepository,
        IConfiguration configuration,
        IAiQuotaService quotaService,
        IAiUsageTracker usageTracker)
    {
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
        _elementRepository = elementRepository;
        _configuration = configuration;
        _quotaService = quotaService;
        _usageTracker = usageTracker;
    }

    public async Task<IdeologyCaseDraftDto> GenerateDraftAsync(GenerateIdeologyCaseInputDto input)
    {
        var material = (input.Material ?? string.Empty).Trim();
        if (material.Length < 20)
        {
            throw new UserFriendlyException("请先提供素材（不少于 20 字，或粘贴权威报道/法规的链接与要点），AI 不会凭空编造事实。");
        }

        var course = await _courseRepository.FindAsync(input.CourseId)
                     ?? throw new UserFriendlyException("课程不存在或无权访问。");

        string? chapterTitle = null;
        string? chapterSummary = null;
        if (input.ChapterId.HasValue)
        {
            var chapter = await _chapterRepository.FindAsync(input.ChapterId.Value);
            if (chapter == null || chapter.CourseId != input.CourseId)
            {
                throw new UserFriendlyException("章节不存在或不属于该课程。");
            }

            chapterTitle = chapter.Title;
            chapterSummary = chapter.Description;
        }

        var elementNames = await ResolveElementNamesAsync(input.ElementIds);

        // 配额：与其它 AI 功能一致的分组校验（超配额直接拒绝，避免"生成到一半被限流"）。
        await _quotaService.CheckAsync(
            CurrentUser.Id ?? Guid.Empty,
            CurrentUser.Roles?.ToArray() ?? Array.Empty<string>(),
            AiFeatureGroups.CaseAnalysis);

        var userPrompt = BuildUserPrompt(course, chapterTitle, chapterSummary, elementNames, material, input);

        var model = _configuration["Qwen:Model"] ?? "qwen-flash";
        var usageId = await _usageTracker.StartAsync(
            AiFeatureGroups.CaseAnalysis, "IdeologyCaseDraft", model, userPrompt);

        string text;
        try
        {
            var chatClient = await QwenClient.CreateChatClient(_configuration, model);
            var options = new ChatOptions
            {
                Instructions = Instructions,
                Temperature = 0.3f,
                MaxOutputTokens = MaxOutputTokens,
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var response = await chatClient.GetResponseAsync(
                new List<ChatMessage> { new(ChatRole.User, userPrompt) },
                options,
                cts.Token);
            text = response.Text ?? string.Empty;

            await _usageTracker.CompleteAsync(usageId, text, true);
        }
        catch (Exception ex)
        {
            await _usageTracker.CompleteAsync(usageId, null, false, ex.Message);
            throw new UserFriendlyException($"AI 生成失败：{ex.Message}");
        }

        var draft = ParseDraft(text, material);

        // 统一提醒项：「案例须人工核验」是本模块的产品底线（原型里也明确了这一点）。
        draft.Warnings.Add("AI 生成内容，事实与数据须人工核验");
        draft.Warnings.Add("时政类内容请确认时效性（发布时间 / 政策是否仍有效）");
        if (string.IsNullOrWhiteSpace(draft.SourceText))
        {
            draft.Warnings.Add("出处为空，保存时会默认取所选章节名（可改为更具体的教材/报道出处）");
        }

        return draft;
    }

    // ── 内部辅助 ────────────────────────────────────────────

    private async Task<List<string>> ResolveElementNamesAsync(List<Guid>? elementIds)
    {
        if (elementIds == null || elementIds.Count == 0)
        {
            return new List<string>();
        }

        var ids = elementIds.Where(x => x != Guid.Empty).Distinct().ToList();
        var query = await _elementRepository.GetQueryableAsync();
        var elements = await AsyncExecuter.ToListAsync(query.Where(x => ids.Contains(x.Id)));
        return elements.Select(x => x.Name).ToList();
    }

    private static string BuildUserPrompt(
        Course course,
        string? chapterTitle,
        string? chapterSummary,
        List<string> elementNames,
        string material,
        GenerateIdeologyCaseInputDto input)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 课程与章节");
        sb.AppendLine($"- 课程：{course.Title}");
        if (!string.IsNullOrWhiteSpace(course.Description))
        {
            sb.AppendLine($"- 课程简介：{Trim(course.Description, 300)}");
        }

        sb.AppendLine($"- 章节：{(string.IsNullOrWhiteSpace(chapterTitle) ? "（整门课程，未指定章节）" : chapterTitle)}");
        if (!string.IsNullOrWhiteSpace(chapterSummary))
        {
            sb.AppendLine($"- 章节要点：{Trim(chapterSummary, 400)}");
        }

        sb.AppendLine();
        sb.AppendLine("## 期望覆盖的思政元素");
        sb.AppendLine(elementNames.Count > 0
            ? string.Join("、", elementNames)
            : "（未指定，请根据素材自行匹配 1~2 个，并在 title/summary 中体现）");

        sb.AppendLine();
        sb.AppendLine($"## 计划的融入方式");
        sb.AppendLine(UsageMethodLabel(input.UsageMethod));

        sb.AppendLine();
        sb.AppendLine("## 教师提供的素材（唯一事实来源，不得超出）");
        sb.AppendLine(Trim(material, MaxMaterialChars));

        if (!string.IsNullOrWhiteSpace(input.ExtraPrompt))
        {
            sb.AppendLine();
            sb.AppendLine("## 附加要求");
            sb.AppendLine(Trim(input.ExtraPrompt, 500));
        }

        sb.AppendLine();
        sb.AppendLine("请按要求输出 JSON（title / summary / contentJson / sourceText）。");
        return sb.ToString();
    }

    private static IdeologyCaseDraftDto ParseDraft(string raw, string material)
    {
        var clean = ExtractJson(raw);
        AiDraftPayload? payload = null;
        if (!string.IsNullOrWhiteSpace(clean))
        {
            try
            {
                payload = JsonSerializer.Deserialize<AiDraftPayload>(clean, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
                });
            }
            catch (JsonException)
            {
                payload = null;
            }
        }

        if (payload == null)
        {
            // 解析失败不把整段回话丢给用户（可能夹带解释文字），给可读提示即可。
            throw new UserFriendlyException("AI 返回内容不是合法 JSON，请重试或换一段素材。");
        }

        var content = payload.ContentJson;
        var contentJson = content == null || string.IsNullOrWhiteSpace(content.Brief)
            ? null
            : JsonSerializer.Serialize(new { brief = content.Brief!.Trim() });

        var sourceText = (payload.SourceText ?? string.Empty).Trim();

        return new IdeologyCaseDraftDto
        {
            Title = string.IsNullOrWhiteSpace(payload.Title) ? "（AI 草稿）思想政治教学案例" : payload.Title!.Trim(),
            Summary = string.IsNullOrWhiteSpace(payload.Summary) ? null : payload.Summary!.Trim(),
            ContentJson = contentJson,
            SourceText = sourceText,
        };
    }

    /// <summary>
    /// 模型实际输出的形状：contentJson 是**嵌套对象**（而不是字符串）。
    /// 这里单独用一个载荷类型承接，避免直接反序列化到对外 DTO 时类型不匹配。
    /// </summary>
    private sealed class AiDraftPayload
    {
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public AiDraftContent? ContentJson { get; set; }
        public string? SourceText { get; set; }
    }

    private sealed class AiDraftContent
    {
        /// <summary>案例文本（正文，300~500 字）。</summary>
        public string? Brief { get; set; }
    }

    private static string ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var text = raw.Trim();
        if (text.StartsWith("```"))
        {
            var firstNewline = text.IndexOf('\n');
            if (firstNewline >= 0)
            {
                text = text[(firstNewline + 1)..];
            }

            if (text.EndsWith("```"))
            {
                text = text[..^3].TrimEnd();
            }
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            text = text.Substring(start, end - start + 1);
        }

        return text.Trim();
    }

    private static string Trim(string? value, int max)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= max ? text : text[..max] + "…";
    }

    private static string UsageMethodLabel(IdeologyUsageMethod method) => method switch
    {
        IdeologyUsageMethod.Import => "课程导入 · 讨论",
        IdeologyUsageMethod.CaseAnalysis => "案例分析",
        IdeologyUsageMethod.Discussion => "课堂讨论",
        IdeologyUsageMethod.PracticeComment => "实训点评",
        IdeologyUsageMethod.ExtendedReading => "拓展阅读",
        _ => method.ToString(),
    };
}
