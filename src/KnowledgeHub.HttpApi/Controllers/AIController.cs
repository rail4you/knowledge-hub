using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI;
using KnowledgeHub.Application.AI.Dtos;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
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
[Authorize]
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

    [HttpPost("chat")]
    [IgnoreAntiforgeryToken]
    public async Task Chat([FromBody] ChatInputDto input)
    {
        var httpContext = HttpContext;

        // 在设置 SSE 响应头之前先验证用户登录状态和关键配置，
        // 避免 ChatStreamingAsync 在 StartAsync() 之后同步抛出异常，
        // 导致 ABP 异常拦截器无法正确协商响应格式而返回 406。
        if (!CurrentUser.IsAuthenticated)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await httpContext.Response.WriteAsJsonAsync(new { error = "User not logged in" });
            return;
        }

        httpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache";
        httpContext.Response.Headers["Connection"] = "keep-alive";
        httpContext.Response.Headers["X-Accel-Buffering"] = "no";

        await httpContext.Response.StartAsync();

        try
        {
            await _chatAppService.ChatStreamingAsync(input, async chunk =>
            {
                var json = JsonSerializer.Serialize(chunk, JsonOptions);
                await httpContext.Response.WriteAsync($"data: {json}\n\n");
                await httpContext.Response.Body.FlushAsync();
            });
        }
        catch (Exception ex)
        {
            // 流式响应已启动，无法更改 HTTP 状态码，
            // 向客户端发送 SSE 错误事件以正常结束连接
            try
            {
                var errorJson = JsonSerializer.Serialize(new
                {
                    content = $"抱歉，发生了错误：{ex.Message}",
                    threadId = "",
                    isComplete = true,
                    isError = true
                }, JsonOptions);
                await httpContext.Response.WriteAsync($"data: {errorJson}\n\n");
                await httpContext.Response.Body.FlushAsync();
            }
            catch
            {
                // 忽略写入错误事件时的异常
            }
        }
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
    [Authorize(KnowledgeHubPermissions.AI.LessonPlan)]
    [IgnoreAntiforgeryToken]
    public async Task GenerateLessonPlan([FromBody] LessonPlanGenerationInputDto input)
    {
        var httpContext = HttpContext;

        if (!CurrentUser.IsAuthenticated)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await httpContext.Response.WriteAsJsonAsync(new { error = "User not logged in" });
            return;
        }

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
    [Authorize(KnowledgeHubPermissions.AI.LessonPlan)]
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
    [Authorize(KnowledgeHubPermissions.AI.CaseAnalysis)]
    [IgnoreAntiforgeryToken]
    public async Task GenerateCaseAnalysis([FromBody] CaseAnalysisGenerationInputDto input)
    {
        var httpContext = HttpContext;

        if (!CurrentUser.IsAuthenticated)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await httpContext.Response.WriteAsJsonAsync(new { error = "User not logged in" });
            return;
        }

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
    [Authorize(KnowledgeHubPermissions.AI.CaseAnalysis)]
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
    [Authorize(KnowledgeHubPermissions.AI.CareerGuidance)]
    [IgnoreAntiforgeryToken]
    public async Task GenerateCareerGuidance([FromBody] CareerGuidanceGenerationInputDto input)
    {
        var httpContext = HttpContext;

        if (!CurrentUser.IsAuthenticated)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await httpContext.Response.WriteAsJsonAsync(new { error = "User not logged in" });
            return;
        }

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
    [Authorize(KnowledgeHubPermissions.AI.CareerGuidance)]
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
    [IgnoreAntiforgeryToken]
    public async Task DeleteThread(string threadId)
    {
        await _chatAppService.DeleteThreadAsync(Guid.Parse(threadId));
    }

    /// <summary>
    /// 清空当前用户所有线程。
    /// </summary>
    [HttpDelete("threads")]
    [IgnoreAntiforgeryToken]
    public async Task ClearAllThreads()
    {
        await _chatAppService.ClearAllThreadsAsync();
    }
}
