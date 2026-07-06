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

    private const string LessonPlanInstructions = @"你是一个专业的教案生成助手。你需要根据提供的文档内容和教学参数，生成一份结构化的教案。

严格要求：
1. 必须输出有效的 JSON 格式
2. 不要输出 JSON 以外的任何内容（不要用 markdown 代码块包裹）
3. 教案内容必须基于提供的文档内容，不要编造
4. 教学环节要合理分配时间，各环节时间总和应等于总课时

JSON 结构：
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
}";

    public LessonPlanAppService(
        IConfiguration configuration,
        ILogger<LessonPlanAppService> logger,
        IRepository<Resource, Guid> resourceRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _resourceRepository = resourceRepository;
    }

    // TODO: PageIndex 已移除，教案生成功能暂时下线。后续用 MeiliSearch/页面内容重新实现。
    public async Task GenerateStreamingAsync(LessonPlanGenerationInputDto input, Func<ChatMessageChunkDto, Task> onChunk)
    {
        var threadId = Guid.NewGuid().ToString();
        _logger.LogWarning("LessonPlan feature is disabled. PageIndex has been removed; will be reimplemented.");

        await onChunk(new ChatMessageChunkDto
        {
            Content = JsonSerializer.Serialize(new { error = "教案生成功能暂时下线维护中，请稍后再试。" }),
            ThreadId = threadId,
            IsComplete = false
        });
        await onChunk(new ChatMessageChunkDto { Content = "", ThreadId = threadId, IsComplete = true });
        await Task.CompletedTask;
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
