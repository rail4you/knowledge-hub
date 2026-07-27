using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI;
using KnowledgeHub.Application.AI.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Auditing;
using Volo.Abp.Uow;

namespace KnowledgeHub.Controllers;

[Area("learning")]
[Route("api/learning/ai")]
[DisableAuditing]
public class AIController : AbpControllerBase
{
    private readonly ChatAppService _chatAppService;
    private readonly LessonPlanAppService _lessonPlanAppService;
    private readonly CaseAnalysisAppService _caseAnalysisAppService;
    private readonly CareerGuidanceAppService _careerGuidanceAppService;
    private readonly ILogger<AIController> _logger;

    public AIController(ChatAppService chatAppService, LessonPlanAppService lessonPlanAppService, CaseAnalysisAppService caseAnalysisAppService, CareerGuidanceAppService careerGuidanceAppService, ILogger<AIController> logger)
    {
        _chatAppService = chatAppService;
        _lessonPlanAppService = lessonPlanAppService;
        _caseAnalysisAppService = caseAnalysisAppService;
        _careerGuidanceAppService = careerGuidanceAppService;
        _logger = logger;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [HttpGet("resources")]
    public async Task<List<ResourceForChatDto>> GetResources()
    {
        return await _chatAppService.GetResourcesWithPageIndexAsync();
    }

    /// <summary>
    /// P1-13：当前用户已审核通过的"简历"资源（IsResume=true），供职业规划下拉使用。
    /// </summary>
    [HttpGet("resumes")]
    public async Task<List<ResourceForChatDto>> GetResumes()
    {
        return await _chatAppService.GetResumesForUserAsync();
    }

    [HttpPost("chat")]
    [IgnoreAntiforgeryToken]
    public async Task Chat([FromBody] ChatInputDto input)
    {
        var httpContext = HttpContext;
        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache";
        httpContext.Response.Headers["Connection"] = "keep-alive";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        await httpContext.Response.StartAsync();

        await _chatAppService.ChatStreamingAsync(input, async chunk =>
        {
            var json = JsonSerializer.Serialize(chunk, JsonOptions);
            await httpContext.Response.WriteAsync($"data: {json}\n\n");
            await httpContext.Response.Body.FlushAsync();
        });
    }

    [HttpPost("test-stream")]
    [IgnoreAntiforgeryToken]
    public async Task TestStream()
    {
        var httpContext = HttpContext;
        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache";
        httpContext.Response.Headers["Connection"] = "keep-alive";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        await httpContext.Response.StartAsync();

        for (int i = 0; i < 5; i++)
        {
            var json = JsonSerializer.Serialize(new { content = $"chunk {i}", isComplete = false }, JsonOptions);
            await httpContext.Response.WriteAsync($"data: {json}\n\n");
            await httpContext.Response.Body.FlushAsync();
            await Task.Delay(500);
        }

        var doneJson = JsonSerializer.Serialize(new { content = "", isComplete = true }, JsonOptions);
        await httpContext.Response.WriteAsync($"data: {doneJson}\n\n");
        await httpContext.Response.Body.FlushAsync();
    }

    [HttpPost("generate-lesson-plan")]
    [IgnoreAntiforgeryToken]
    public async Task GenerateLessonPlan([FromBody] LessonPlanGenerationInputDto input)
    {
        var httpContext = HttpContext;
        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache";
        httpContext.Response.Headers["Connection"] = "keep-alive";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        await httpContext.Response.StartAsync();

        await _lessonPlanAppService.GenerateStreamingAsync(input, async chunk =>
        {
            var json = JsonSerializer.Serialize(chunk, JsonOptions);
            await httpContext.Response.WriteAsync($"data: {json}\n\n");
            await httpContext.Response.Body.FlushAsync();
        });
    }

    [HttpPost("export-lesson-plan-docx")]
    [IgnoreAntiforgeryToken]
    public IActionResult ExportLessonPlanDocx([FromBody] LessonPlanExportInputDto input)
    {
        var docxBytes = _lessonPlanAppService.ExportDocx(input.LessonPlanJson);
        var fileName = $"教案_{DateTime.Now:yyyyMMdd_HHmmss}.docx";

        return File(docxBytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileName);
    }

    [HttpPost("generate-case-analysis")]
    [IgnoreAntiforgeryToken]
    public async Task GenerateCaseAnalysis([FromBody] CaseAnalysisGenerationInputDto input)
    {
        var httpContext = HttpContext;
        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache";
        httpContext.Response.Headers["Connection"] = "keep-alive";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        await httpContext.Response.StartAsync();

        await _caseAnalysisAppService.GenerateStreamingAsync(input, async chunk =>
        {
            var json = JsonSerializer.Serialize(chunk, JsonOptions);
            await httpContext.Response.WriteAsync($"data: {json}\n\n");
            await httpContext.Response.Body.FlushAsync();
        });
    }

    [HttpPost("export-case-analysis-docx")]
    [IgnoreAntiforgeryToken]
    public IActionResult ExportCaseAnalysisDocx([FromBody] CaseAnalysisExportInputDto input)
    {
        var docxBytes = _caseAnalysisAppService.ExportDocx(input.CaseAnalysisJson);
        var fileName = $"案例分析_{DateTime.Now:yyyyMMdd_HHmmss}.docx";

        return File(docxBytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileName);
    }

    [HttpPost("generate-career-guidance")]
    [IgnoreAntiforgeryToken]
    public async Task GenerateCareerGuidance([FromBody] CareerGuidanceGenerationInputDto input)
    {
        var httpContext = HttpContext;
        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache";
        httpContext.Response.Headers["Connection"] = "keep-alive";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        await httpContext.Response.StartAsync();

        await _careerGuidanceAppService.GenerateStreamingAsync(input, async chunk =>
        {
            var json = JsonSerializer.Serialize(chunk, JsonOptions);
            await httpContext.Response.WriteAsync($"data: {json}\n\n");
            await httpContext.Response.Body.FlushAsync();
        });
    }

    [HttpPost("export-career-guidance-docx")]
    [IgnoreAntiforgeryToken]
    public IActionResult ExportCareerGuidanceDocx([FromBody] CareerGuidanceExportInputDto input)
    {
        var docxBytes = _careerGuidanceAppService.ExportDocx(input.CareerGuidanceJson);
        var fileName = $"职业规划_{DateTime.Now:yyyyMMdd_HHmmss}.docx";

        return File(docxBytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileName);
    }

    // ========== Thread Management ==========

    /// <summary>
    /// 获取当前用户的聊天线程列表（不含消息内容，仅摘要）。
    /// </summary>
    [HttpGet("threads")]
    public async Task<List<ChatThreadDto>> GetThreads()
    {
        return await _chatAppService.GetMyThreadsAsync();
    }

    /// <summary>
    /// 获取指定线程的完整消息列表。
    /// </summary>
    [HttpGet("threads/{threadId}")]
    public async Task<ChatThreadDto> GetThread(string threadId)
    {
        return await _chatAppService.GetThreadAsync(threadId);
    }

    /// <summary>
    /// 删除指定线程及其所有消息。
    /// </summary>
    [HttpDelete("threads/{threadId}")]
    public async Task DeleteThread(string threadId)
    {
        await _chatAppService.DeleteThreadAsync(Guid.Parse(threadId));
    }

    /// <summary>
    /// 清空当前用户所有线程。
    /// </summary>
    [HttpDelete("threads")]
    public async Task ClearAllThreads()
    {
        await _chatAppService.ClearAllThreadsAsync();
    }
}
