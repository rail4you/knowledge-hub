using System;
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
using OpenAI.Chat;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.Application.AI;

// Not implementing an ABP interface to avoid Castle DynamicProxy buffering issues with IAsyncEnumerable.
// The controller injects this class directly.
public class CaseAnalysisAppService : KnowledgeHubAppService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CaseAnalysisAppService> _logger;
    private readonly IRepository<Resource, Guid> _resourceRepository;

    private const string CaseAnalysisInstructions = @"你是一个专业的案例分析助手。你需要根据提供的文档内容，生成一份结构化的多维度案例分析报告。

严格要求：
1. 必须输出有效的 JSON 格式
2. 不要输出 JSON 以外的任何内容（不要用 markdown 代码块包裹）
3. 分析内容必须基于提供的文档内容，不要编造
4. 分析要深入、有洞察力，结合文档中的实际信息
5. 所有 JSON 字段值必须使用简体中文表达
6. 输出内容中禁止出现任何英文单词、英文缩写、拉丁字母、英文标题、英文项目符号或中英混写
7. 文档中的英文术语、英文缩写、产品名、方法名、模型名、框架名、岗位名、部门名等，必须改写为纯中文表达；如果无法直译，也要使用中文释义，不能保留英文原文
8. 时间、数字、百分比、金额可以保留阿拉伯数字，但单位与说明必须使用中文
9. `severity` 字段只能填写 ""高""/""中""/""低""
10. 如果你发现自己将要输出任何英文字符，必须先改写成中文后再输出

JSON 结构：
{
  ""title"": ""案例标题"",
  ""summary"": ""案例摘要(150字以内)"",
  ""background"": {
    ""industry"": ""所属行业"",
    ""timeframe"": ""时间范围"",
    ""context"": ""背景描述"",
    ""stakeholders"": [""相关方1"", ""相关方2""]
  },
  ""keyIssues"": [
    {
      ""id"": ""1"",
      ""title"": ""问题标题"",
      ""description"": ""问题描述"",
      ""impact"": ""影响分析"",
      ""severity"": ""高/中/低""
    }
  ],
  ""solutions"": [
    {
      ""id"": ""1"",
      ""title"": ""方案标题"",
      ""description"": ""方案描述"",
      ""steps"": [""步骤1"", ""步骤2""],
      ""expectedOutcome"": ""预期效果""
    }
  ],
  ""keyInsights"": [""洞察1"", ""洞察2""],
  ""recommendations"": [""建议1"", ""建议2""]
}";

    public CaseAnalysisAppService(
        IConfiguration configuration,
        ILogger<CaseAnalysisAppService> logger,
        IRepository<Resource, Guid> resourceRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _resourceRepository = resourceRepository;
    }

    // TODO: PageIndex 已移除，案例分析功能暂时下线。后续用 MeiliSearch/页面内容重新实现。
    public async Task GenerateStreamingAsync(CaseAnalysisGenerationInputDto input, Func<ChatMessageChunkDto, Task> onChunk)
    {
        var threadId = Guid.NewGuid().ToString();
        _logger.LogWarning("CaseAnalysis feature is disabled. PageIndex has been removed; will be reimplemented.");

        await onChunk(new ChatMessageChunkDto
        {
            Content = JsonSerializer.Serialize(new { error = "案例分析功能暂时下线维护中，请稍后再试。" }),
            ThreadId = threadId,
            IsComplete = false
        });
        await onChunk(new ChatMessageChunkDto { Content = "", ThreadId = threadId, IsComplete = true });
        await Task.CompletedTask;
    }

    public byte[] ExportDocx(string caseAnalysisJson)
    {
        var cleanJson = caseAnalysisJson.Trim();
        if (cleanJson.StartsWith("```"))
        {
            var firstNewline = cleanJson.IndexOf('\n');
            if (firstNewline >= 0) cleanJson = cleanJson[(firstNewline + 1)..];
            if (cleanJson.EndsWith("```"))
            {
                cleanJson = cleanJson[..^3].TrimEnd();
            }
        }

        var caseAnalysis = JsonSerializer.Deserialize<CaseAnalysisDto>(cleanJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new AbpException("无法解析案例分析JSON数据。");

        return CaseAnalysisDocxGenerator.Generate(caseAnalysis);
    }
}
