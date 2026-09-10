using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI.Dtos;
using KnowledgeHub.Resources;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Application.AI;

// Not implementing an ABP interface to avoid Castle DynamicProxy buffering issues with IAsyncEnumerable.
// The controller injects this class directly.
public class LessonPlanAppService : KnowledgeHubAppService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LessonPlanAppService> _logger;
    private readonly IRepository<Resource, Guid> _resourceRepository;

    private const string LessonPlanInstructions = @"你是大学教师/教学设计师，需要根据给定的""文档摘要""和""教学参数""，按中国大学教案（又称""教学设计""）的标准体例，生成一份完整的课堂教案。

严格要求：
1. 必须输出合法 JSON（不要用 markdown 代码块包裹，不要任何多余文字）
2. 教案内容必须基于""文档摘要""，可适度引申但不得编造与摘要无关的具体数据/公式
3. 教学环节时间总和必须等于总课时 duration（分钟）
4. 教学目标按布鲁姆分类法分三层：知识目标 / 能力目标 / 素质目标
5. 教学方法、教学活动、作业均要可操作、可观察
6. 若 ""教师附加要求"" 非空，必须严格遵循其全部约束（如强调课程思政、双语教学、工程实践等）

JSON 结构（沿用现有 schema）：
{
  ""title"": ""教案标题"",
  ""subject"": ""学科"",
  ""grade"": ""年级"",
  ""duration"": 45,
  ""objectives"": [""教学目标1"", ""教学目标2""],
  ""keyPoints"": [""教学重点1""],
  ""difficulties"": [""教学难点1""],
  ""sections"": [
    {
      ""name"": ""环节名称"",
      ""duration"": 10,
      ""content"": ""环节内容描述"",
      ""activities"": [""活动1""]
    }
  ],
  ""methods"": [""教学方法1""],
  ""resources"": [""教学资源1""],
  ""assessment"": [""评估方法1""],
  ""homework"": [""课后作业1""]
}

补充要求：
- title 要形如 ""《xxx》第X章 xxx —— xxx""（明显体现课程与主题）
- objectives 不少于 6 条（每类目标各 2 条以上，知识 / 能力 / 素质三类）
- sections 至少 5 个环节（导入 / 讲授 / 案例或讨论 / 课堂练习 / 总结与答疑）
- 每个 section 的 content 含三部分：教师活动 + 学生活动 + 设计意图
- activities 数组给出可观察的具体动作（提问、分组讨论、上台演算、随堂测验等）
- homework 区分""必做""与""选做/拓展""，不少于 3 条
- methods 选择性采用：讲授法、案例法、讨论法、探究法、演示法、练习法
- 难度与重点须紧扣摘要所揭示的核心概念

## 授课对象差异化要求（严格遵循）
根据「授课对象」参数调整教案的深度、侧重点与风格：

- **大一**：偏基础入门。教学目标侧重学科认知建立与基础概念掌握；教学活动以讲授、演示、课堂讨论为主；难点不宜过深；案例选取贴近学生日常生活经验；作业以巩固型为主。
- **大二**：基础与能力并重。在概念理解基础上增加分析应用环节；适当引入案例分析与小组讨论；作业兼顾巩固与拓展。
- **大三**：侧重专业能力深化。教学目标增加综合分析与评价层次；教学活动增加探究法、案例法比重；引入行业实际案例或项目模拟；作业强调综合应用。
- **大四**：偏综合与高阶。教学目标应覆盖评价与创造层次；强调学科前沿、行业动态、实践创新；教学活动以研讨法、项目法、案例分析为主；内容要有批判性思维训练和综合问题解决训练；作业以研究性、综合性为主，可包含项目方案或设计报告。
- **研究生**：学术研究导向。强调文献与前沿；以研讨法为主；作业与研究课题相关。
- **博士**：高深学术与创新。强调原创性与前沿探索；以独立研究指导为主。";

    public LessonPlanAppService(
        IConfiguration configuration,
        ILogger<LessonPlanAppService> logger,
        IRepository<Resource, Guid> resourceRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _resourceRepository = resourceRepository;
    }

    // 仅供 SSE Controller 直接调用：Func 回调无法绑定为 HTTP 参数，
    // 必须对 Conventional Controller 隐藏，否则 /Abp/ServiceProxyScript 全站 500。
    [Volo.Abp.RemoteService(false)]
    public async Task GenerateStreamingAsync(LessonPlanGenerationInputDto input, Func<ChatMessageChunkDto, Task> onChunk)
    {
        var threadId = Guid.NewGuid().ToString();

        // 1. 读取资源（包含 AI 摘要）
        var resource = await _resourceRepository.FindAsync(input.ResourceId);
        if (resource == null)
        {
            await EmitErrorAsync(onChunk, threadId, $"未找到资源: {input.ResourceId}");
            return;
        }

        // 2. 摘要缺失校验
        if (string.IsNullOrWhiteSpace(resource.Summary))
        {
            await EmitErrorAsync(onChunk, threadId,
                "该资源尚未生成 AI 摘要，请先在资源详情页生成摘要后再使用教案功能。");
            return;
        }

        var apiKey = _configuration["Qwen:ApiKey"]
            ?? throw new AbpException("Qwen:ApiKey is not configured");
        var baseUrl = _configuration["Qwen:BaseUrl"]
            ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:Model"] ?? "qwen-plus";

        // 3. 组合 User Prompt（以 Resource.Summary 为唯一教学依据）
        var userPrompt = $@"## 文档摘要（唯一教学依据）
{resource.Summary}

## 文档元信息
- 名称：{resource.Name}
{(string.IsNullOrWhiteSpace(resource.Description) ? "" : $"- 描述：{resource.Description}")}

## 教学参数
- 课程主题：{input.Topic}
- 学科：{(string.IsNullOrWhiteSpace(input.Subject) ? "未指定" : input.Subject)}
- 授课对象：{(string.IsNullOrWhiteSpace(input.Grade) ? "未指定" : input.Grade)}（注意：必须严格按 SystemPrompt 中「授课对象差异化要求」调整教学内容深度、风格与侧重点）
- 课时：{input.Duration} 分钟

{(string.IsNullOrWhiteSpace(input.CustomPrompt) ? "" : $@"## 教师附加要求
{input.CustomPrompt}
")}

请按 SystemPrompt 中规定的 JSON 结构输出教案。";

        var openaiClient = new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

        IChatClient chatClient = openaiClient.GetChatClient(model).AsIChatClient();

        var chatOptions = new ChatOptions
        {
            Instructions = LessonPlanInstructions,
        };

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, userPrompt)
        };

        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, chatOptions, CancellationToken.None))
        {
            if (update.Text != null && update.Text.Length > 0)
            {
                await onChunk(new ChatMessageChunkDto
                {
                    Content = update.Text,
                    ThreadId = threadId,
                    IsComplete = false
                });
            }
        }

        await onChunk(new ChatMessageChunkDto
        {
            Content = "",
            ThreadId = threadId,
            IsComplete = true
        });
    }

    /// <summary>
    /// 流式输出一个错误块后立即 complete。
    /// </summary>
    private static async Task EmitErrorAsync(Func<ChatMessageChunkDto, Task> onChunk, string threadId, string message)
    {
        await onChunk(new ChatMessageChunkDto
        {
            Content = JsonSerializer.Serialize(new { error = message }),
            ThreadId = threadId,
            IsComplete = false
        });
        await onChunk(new ChatMessageChunkDto
        {
            Content = "",
            ThreadId = threadId,
            IsComplete = true
        });
    }

    /// <summary>
    /// Export a lesson plan JSON as a DOCX file.
    /// </summary>
    public byte[] ExportDocx(string lessonPlanJson)
    {
        var cleanJson = lessonPlanJson.Trim();
        // Strip markdown code block wrapper if present
        if (cleanJson.StartsWith("```"))
        {
            var firstNewline = cleanJson.IndexOf('\n');
            if (firstNewline >= 0) cleanJson = cleanJson[(firstNewline + 1)..];
            if (cleanJson.EndsWith("```"))
            {
                cleanJson = cleanJson[..^3].TrimEnd();
            }
        }

        var lessonPlan = JsonSerializer.Deserialize<LessonPlanDto>(cleanJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new AbpException("无法解析教案JSON数据。");

        return LessonPlanDocxGenerator.Generate(lessonPlan);
    }
}