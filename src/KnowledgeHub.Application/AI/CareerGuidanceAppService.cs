using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI.Dtos;
using KnowledgeHub.Domain.Search;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NPOI.XWPF.UserModel;
using OpenAI;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Application.AI;

// Not implementing an ABP interface to avoid Castle DynamicProxy buffering issues with IAsyncEnumerable.
// The controller injects this class directly.
public class CareerGuidanceAppService : KnowledgeHubAppService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CareerGuidanceAppService> _logger;
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<PageContent, Guid> _pageContentRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICurrentTenant _currentTenant;

    private const string CareerGuidanceInstructions = @"你是一个专业的职业规划顾问。你需要根据提供的简历/个人文档内容，生成一份结构化的职业规划建议报告。

严格要求：
1. 必须输出有效的 JSON 格式
2. 不要输出 JSON 以外的任何内容（不要用 markdown 代码块包裹）
3. 分析内容必须基于提供的文档内容，不要编造
4. 建议要具体、可操作，结合文档中的实际信息
5.【重要】必须从简历中提取教育背景信息和工作/实习经历信息，分别填入 educationBackground 和 workExperience 数组
6. 如果文档中没有教育背景或工作经历信息，对应数组留空即可，不要编造

JSON 结构：
{
  ""title"": ""职业规划建议报告标题"",
  ""assessment"": {
    ""careerMatchScore"": 85,
    ""strengths"": [""优势1"", ""优势2""],
    ""areasForImprovement"": [""待提升1"", ""待提升2""],
    ""summary"": ""综合评估摘要(150字以内)"",
    ""educationBackground"": [
      {
        ""school"": ""学校名称"",
        ""degree"": ""学历（如：硕士研究生、本科、大专）"",
        ""major"": ""专业名称"",
        ""period"": ""就读时间（如：2021.09-2024.06）""
      }
    ],
    ""workExperience"": [
      {
        ""company"": ""公司/单位名称"",
        ""position"": ""职位"",
        ""period"": ""工作时间（如：2024.07-至今）"",
        ""description"": ""工作内容简述""
      }
    ]
  },
  ""recommendedPaths"": [
    {
      ""title"": ""职业方向名称"",
      ""description"": ""方向描述"",
      ""matchScore"": 90,
      ""requiredSkills"": [""技能1"", ""技能2""],
      ""salaryRange"": ""薪资范围"",
      ""growthPotential"": ""发展潜力描述""
    }
  ],
  ""skillGaps"": [
    {
      ""skill"": ""技能名"",
      ""currentLevel"": ""当前水平"",
      ""targetLevel"": ""目标水平"",
      ""priority"": ""高/中/低""
    }
  ],
  ""actionPlan"": [
    {
      ""id"": ""1"",
      ""title"": ""行动项标题"",
      ""description"": ""描述"",
      ""timeline"": ""时间线"",
      ""priority"": ""高/中/低""
    }
  ],
  ""nextSteps"": [""下一步1"", ""下一步2""]
}";

    public CareerGuidanceAppService(
        IConfiguration configuration,
        ILogger<CareerGuidanceAppService> logger,
        IRepository<Resource, Guid> resourceRepository,
        IRepository<PageContent, Guid> pageContentRepository,
        IFileStorageService fileStorageService,
        IHttpClientFactory httpClientFactory,
        ICurrentTenant currentTenant)
    {
        _configuration = configuration;
        _logger = logger;
        _resourceRepository = resourceRepository;
        _pageContentRepository = pageContentRepository;
        _fileStorageService = fileStorageService;
        _httpClientFactory = httpClientFactory;
        _currentTenant = currentTenant;
    }

    // 仅供 SSE Controller 直接调用：Func 回调无法绑定为 HTTP 参数，
    // 必须对 Conventional Controller 隐藏，否则 /Abp/ServiceProxyScript 全站 500。
    [Volo.Abp.RemoteService(false)]
    public async Task GenerateStreamingAsync(CareerGuidanceGenerationInputDto input, Func<ChatMessageChunkDto, Task> onChunk)
    {
        var threadId = Guid.NewGuid().ToString();

        var apiKey = _configuration["Qwen:ApiKey"]
            ?? throw new AbpException("Qwen:ApiKey is not configured");
        var baseUrl = _configuration["Qwen:BaseUrl"]
            ?? "https://dashscope.aliyuncs.com/compatible-mode/v1";
        var model = _configuration["Qwen:Model"] ?? "qwen-flash";

        string userPrompt;

        // 学生端：使用简历文本直接构建提示词
        if (!string.IsNullOrWhiteSpace(input.ResumeContent))
        {
            var titleSection = !string.IsNullOrWhiteSpace(input.ResumeTitle)
                ? $"## 简历标题\n{input.ResumeTitle}\n"
                : "";
            var goalSection = !string.IsNullOrWhiteSpace(input.CareerGoal)
                ? $"\n## 职业目标\n{input.CareerGoal}"
                : "";

            // 如果简历带有 DOCX/PDF 附件，读取全文交给 AI 解析
            string? attachmentText = null;
            if (!string.IsNullOrWhiteSpace(input.AttachmentUrl))
            {
                attachmentText = await TryExtractAttachmentTextAsync(input.AttachmentUrl);
            }

            if (!string.IsNullOrWhiteSpace(attachmentText))
            {
                // 附件全文是主要信息来源，表单字段作为补充标签
                var attachmentSection = attachmentText.Length > 12000
                    ? attachmentText[..12000]
                    : attachmentText;

                userPrompt = $@"请根据以下简历附件全文，生成一份全面的职业规划建议报告。

{titleSection}
## 简历附件原文（DOCX/PDF 全文）
{attachmentSection}

## 简历补充字段
{input.ResumeContent}
{goalSection}

请根据附件原文和补充字段，综合分析并生成职业规划建议JSON。教育背景、工作经历等信息优先从附件原文中提取。";
            }
            else
            {
                userPrompt = $@"请根据以下简历内容，生成一份全面的职业规划建议报告。

{titleSection}
## 简历内容
{input.ResumeContent}
{goalSection}

请生成职业规划建议JSON。";
            }
        }
        else if (input.ResourceId.HasValue)
        {
            // 管理端：从 Resource 的 PageContent 读取全文（与 DocumentSummaryService 同款思路）
            Resource? resource;
            using (DataFilter.Disable<IMultiTenant>())
            {
                resource = await _resourceRepository.FindAsync(input.ResourceId.Value);
            }
            if (resource == null)
            {
                await onChunk(new ChatMessageChunkDto
                {
                    Content = JsonSerializer.Serialize(new { error = $"未找到资源: {input.ResourceId}" }),
                    ThreadId = threadId,
                    IsComplete = false
                });
                await onChunk(new ChatMessageChunkDto { Content = "", ThreadId = threadId, IsComplete = true });
                return;
            }

            // 切到资源的租户上下文，再读 PageContent（绕开 IMultiTenant 过滤）
            List<PageContent> pages;
            using (_currentTenant.Change(resource.TenantId))
            {
                using (DataFilter.Disable<IMultiTenant>())
                {
                    pages = await _pageContentRepository.GetListAsync(p => p.ResourceId == resource.Id);
                }
            }
            pages = pages.OrderBy(p => p.PageNumber).ToList();

            if (pages.Count == 0)
            {
                await onChunk(new ChatMessageChunkDto
                {
                    Content = JsonSerializer.Serialize(new { error = "该资源尚未提取页面内容（请先执行文档索引），无法生成职业规划。" }),
                    ThreadId = threadId,
                    IsComplete = false
                });
                await onChunk(new ChatMessageChunkDto { Content = "", ThreadId = threadId, IsComplete = true });
                return;
            }

            // 采样：头 5 页 + 尾 5 页 + 中间均匀采 5 页（与 DocumentSummaryService 一致）
            var sampled = PickPages(pages);
            var fullText = string.Join("\n", sampled.Select(p => p.Content ?? string.Empty));
            if (fullText.Length > 12000)
            {
                fullText = fullText[..12000];
            }

            var goalSection = !string.IsNullOrWhiteSpace(input.CareerGoal)
                ? $"\n## 职业目标\n{input.CareerGoal}"
                : "";

            userPrompt = $@"请根据以下简历/个人文档内容，生成一份全面的职业规划建议报告。

## 文档名称
{resource.Name}

## 文档内容（采样后）
{fullText}
{goalSection}

请生成职业规划建议JSON。";
        }
        else
        {
            await onChunk(new ChatMessageChunkDto
            {
                Content = JsonSerializer.Serialize(new { error = "请提供简历内容或选择简历资源。" }),
                ThreadId = threadId,
                IsComplete = false
            });
            await onChunk(new ChatMessageChunkDto { Content = "", ThreadId = threadId, IsComplete = true });
            return;
        }

        IChatClient chatClient = QwenClient.CreateChatClient(_configuration, model);

        var chatOptions = new ChatOptions
        {
            Instructions = CareerGuidanceInstructions,
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

    public byte[] ExportDocx(string careerGuidanceJson)
    {
        var cleanJson = careerGuidanceJson.Trim();
        if (cleanJson.StartsWith("```"))
        {
            var firstNewline = cleanJson.IndexOf('\n');
            if (firstNewline >= 0) cleanJson = cleanJson[(firstNewline + 1)..];
            if (cleanJson.EndsWith("```"))
            {
                cleanJson = cleanJson[..^3].TrimEnd();
            }
        }

        var careerGuidance = JsonSerializer.Deserialize<CareerGuidanceDto>(cleanJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new AbpException("无法解析职业规划JSON数据。");

        return CareerGuidanceDocxGenerator.Generate(careerGuidance);
    }

    /// <summary>
    /// 尝试从简历附件（DOCX）中提取纯文本内容。
    /// 支持本地文件路径和 OSS URL（阿里云对象存储）。
    /// 使用 NPOI 读取 DOCX，不进行硬编码字段解析——全文交给 AI 自行理解。
    /// </summary>
    private async Task<string?> TryExtractAttachmentTextAsync(string attachmentUrl)
    {
        try
        {
            // Strip query string for extension detection
            var urlWithoutQuery = attachmentUrl;
            var qIdx = urlWithoutQuery.IndexOf('?');
            if (qIdx >= 0) urlWithoutQuery = urlWithoutQuery[..qIdx];

            var ext = Path.GetExtension(urlWithoutQuery).ToLowerInvariant();

            if (ext != ".docx")
            {
                _logger.LogInformation("Unsupported resume attachment format: {Ext} (url: {Url})", ext, attachmentUrl);
                return null;
            }

            Stream stream;

            if (attachmentUrl.StartsWith("http://") || attachmentUrl.StartsWith("https://"))
            {
                // OSS / remote URL — download into memory
                var httpClient = _httpClientFactory.CreateClient("CareerGuidance");
                var response = await httpClient.GetAsync(attachmentUrl);
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync();
                _logger.LogInformation("Downloaded DOCX from OSS: {Length} bytes", bytes.Length);
                stream = new MemoryStream(bytes);
            }
            else
            {
                // Local file path
                var fullPath = Path.Combine(_fileStorageService.RootPath, attachmentUrl);
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("Attachment file not found: {Path}", fullPath);
                    return null;
                }
                stream = File.OpenRead(fullPath);
            }

            await using (stream)
            {
                var doc = new XWPFDocument(stream);
                var sb = new StringBuilder();

                foreach (var para in doc.Paragraphs)
                {
                    var text = para.Text?.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        sb.AppendLine(text);
                    }
                }

                // Also extract table content (common in resumes)
                foreach (var table in doc.Tables)
                {
                    foreach (var row in table.Rows)
                    {
                        var cells = row.GetTableCells()
                            .Select(c => string.Join(" ", c.Paragraphs.Select(p => p.Text)))
                            .Select(t => t?.Trim())
                            .Where(t => !string.IsNullOrEmpty(t));
                        var rowText = string.Join(" | ", cells);
                        if (!string.IsNullOrEmpty(rowText))
                        {
                            sb.AppendLine(rowText);
                        }
                    }
                }

                var result = sb.ToString().Trim();
                _logger.LogInformation("Extracted {Length} chars from DOCX attachment", result.Length);
                return result.Length > 0 ? result : null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract text from attachment: {Url}", attachmentUrl);
            return null;
        }
    }

    /// <summary>
    /// 采样：头 5 页 + 尾 5 页 + 中间均匀采 5 页。
    /// 当文档总页数 ≤ 15 时全部返回。
    /// </summary>
    private static List<PageContent> PickPages(List<PageContent> pages)
    {
        const int Head = 5, Tail = 5, Mid = 5;
        if (pages.Count <= Head + Tail + Mid)
        {
            return pages.ToList();
        }

        var picked = new List<PageContent>(Head + Tail + Mid);
        picked.AddRange(pages.Take(Head));
        picked.AddRange(pages.TakeLast(Tail));

        var midStart = Head;
        var midEnd = pages.Count - Tail;
        var midSpan = midEnd - midStart;
        if (midSpan > 0)
        {
            var step = (double)midSpan / Mid;
            for (int i = 0; i < Mid; i++)
            {
                var idx = midStart + (int)Math.Round(i * step);
                if (idx < midEnd && !picked.Contains(pages[idx]))
                {
                    picked.Add(pages[idx]);
                }
            }
        }

        return picked.OrderBy(p => p.PageNumber).ToList();
    }
}
