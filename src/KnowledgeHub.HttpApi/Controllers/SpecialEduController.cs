using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using KnowledgeHub.Application.SpecialEducation;
using KnowledgeHub.Permissions;
using KnowledgeHub.SpecialEducation;
using KnowledgeHub.SpecialEducation.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace KnowledgeHub.Controllers;

[Area("learning")]
[Route("api/learning/special-edu")]
[Authorize]
public class SpecialEduController : AbpControllerBase
{
    private readonly SpecialTeachingDesignAppService _designService;
    private readonly SpecialIepAppService _iepService;
    private readonly SpecialEduResourceAppService _resourceService;
    private readonly SpecialEducationAdminAppService _adminService;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public SpecialEduController(
        SpecialTeachingDesignAppService designService,
        SpecialIepAppService iepService,
        SpecialEduResourceAppService resourceService,
        SpecialEducationAdminAppService adminService)
    {
        _designService = designService;
        _iepService = iepService;
        _resourceService = resourceService;
        _adminService = adminService;
    }

    // ── 教学设计 ──
    [HttpGet("teaching-designs")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.TeachingDesign)]
    public Task<PagedResultDto<SpecialTeachingDesignDto>> GetDesigns([FromQuery] GetTeachingDesignListInputDto input)
        => _designService.GetListAsync(input);

    [HttpGet("teaching-designs/{id}")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.TeachingDesign)]
    public Task<SpecialTeachingDesignDto> GetDesign(Guid id) => _designService.GetAsync(id);

    [HttpPost("teaching-designs")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.TeachingDesign)]
    [IgnoreAntiforgeryToken]
    public Task<SpecialTeachingDesignDto> SaveDesign([FromBody] SaveTeachingDesignInputDto input) => _designService.SaveAsync(input);

    [HttpPost("teaching-designs/{id}/submit")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.TeachingDesign)]
    [IgnoreAntiforgeryToken]
    public Task<SpecialTeachingDesignDto> SubmitDesign(Guid id, [FromBody] SubmitTeachingDesignForReviewInputDto? input)
        => _designService.SubmitForReviewAsync(new SubmitTeachingDesignForReviewInputDto { Id = id, ReviewerUserId = input?.ReviewerUserId });

    [HttpPost("teaching-designs/review")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.Default)]
    [IgnoreAntiforgeryToken]
    public Task<SpecialTeachingDesignDto> ReviewDesign([FromBody] ReviewTeachingDesignInputDto input) => _designService.ReviewAsync(input);

    [HttpDelete("teaching-designs/{id}")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.TeachingDesign)]
    [IgnoreAntiforgeryToken]
    public Task DeleteDesign(Guid id) => _designService.DeleteAsync(id);

    [HttpPost("generate-teaching-design")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.TeachingDesign)]
    [IgnoreAntiforgeryToken]
    public async Task GenerateDesign([FromBody] GenerateTeachingDesignInputDto input)
        => await WriteSseAsync(chunk => _designService.GenerateStreamingAsync(input, chunk));

    [HttpPost("export-teaching-design-docx")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.TeachingDesign)]
    [IgnoreAntiforgeryToken]
    public IActionResult ExportDesignDocx([FromBody] ExportTeachingDesignInputDto input)
    {
        var bytes = _designService.ExportDocx(input.ResultJson);
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"特教教案_{DateTime.Now:yyyyMMdd_HHmmss}.docx");
    }

    // ── IEP ──
    [HttpGet("ieps")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.IEP)]
    public Task<PagedResultDto<IepPlanDto>> GetIeps([FromQuery] GetIepListInputDto input) => _iepService.GetListAsync(input);

    [HttpGet("ieps/mine")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.Default)]
    public Task<PagedResultDto<IepPlanDto>> GetMyIeps([FromQuery] GetIepListInputDto input) => _iepService.GetMyIepListAsync(input);

    [HttpGet("ieps/{id}")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.Default)]
    public Task<IepPlanDto> GetIep(Guid id) => _iepService.GetAsync(id);

    [HttpPost("ieps")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.IEP)]
    [IgnoreAntiforgeryToken]
    public Task<IepPlanDto> SaveIep([FromBody] SaveIepInputDto input) => _iepService.SaveAsync(input);

    [HttpPost("ieps/{id}/revision")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.IEP)]
    [IgnoreAntiforgeryToken]
    public Task<IepPlanDto> CreateIepRevision(Guid id) => _iepService.CreateRevisionAsync(id);

    [HttpPost("ieps/{id}/submit")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.IEP)]
    [IgnoreAntiforgeryToken]
    public Task<IepPlanDto> SubmitIep(Guid id, [FromBody] SubmitIepForReviewInputDto? input)
        => _iepService.SubmitForReviewAsync(new SubmitIepForReviewInputDto { Id = id, ReviewerUserId = input?.ReviewerUserId });

    [HttpPost("ieps/review")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.Default)]
    [IgnoreAntiforgeryToken]
    public Task<IepPlanDto> ReviewIep([FromBody] ReviewIepInputDto input) => _iepService.ReviewAsync(input);

    [HttpDelete("ieps/{id}")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.IEP)]
    [IgnoreAntiforgeryToken]
    public Task DeleteIep(Guid id) => _iepService.DeleteAsync(id);

    [HttpPost("generate-iep")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.IEP)]
    [IgnoreAntiforgeryToken]
    public async Task GenerateIep([FromBody] GenerateIepInputDto input)
        => await WriteSseAsync(chunk => _iepService.GenerateStreamingAsync(input, chunk));

    [HttpPost("export-iep-docx")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.IEP)]
    [IgnoreAntiforgeryToken]
    public IActionResult ExportIepDocx([FromBody] ExportIepInputDto input)
    {
        var doc = SpecialIepAppService.ParseResult(input.ResultJson);
        var bytes = SpecialEduDocxGenerator.GenerateIep(doc, "");
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"IEP方案_{DateTime.Now:yyyyMMdd_HHmmss}.docx");
    }

    // ── 多模态资源 ──
    [HttpGet("resources")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.Default)]
    public Task<PagedResultDto<SpecialEduResourceDto>> GetResources([FromQuery] GetSpecialResourceListInputDto input)
        => _resourceService.GetListAsync(input);

    [HttpGet("resources/{id}")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.Default)]
    public Task<SpecialEduResourceDto> GetResource(Guid id) => _resourceService.GetAsync(id);

    [HttpPost("resources")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.Resource)]
    [IgnoreAntiforgeryToken]
    public Task<SpecialEduResourceDto> SaveResource([FromBody] SaveSpecialResourceInputDto input) => _resourceService.SaveAsync(input);

    [HttpDelete("resources/{id}")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.Resource)]
    [IgnoreAntiforgeryToken]
    public Task DeleteResource(Guid id) => _resourceService.DeleteAsync(id);

    [HttpPost("resources/{id}/submit")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.Resource)]
    [IgnoreAntiforgeryToken]
    public Task<SpecialEduResourceDto> SubmitResource(Guid id, [FromBody] SubmitSpecialResourceForReviewInputDto? input)
        => _resourceService.SubmitForReviewAsync(new SubmitSpecialResourceForReviewInputDto { Id = id, ReviewerUserId = input?.ReviewerUserId });

    [HttpPost("resources/review")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.Default)]
    [IgnoreAntiforgeryToken]
    public Task<SpecialEduResourceDto> ReviewResource([FromBody] ReviewSpecialResourceInputDto input) => _resourceService.ReviewAsync(input);

    [HttpPost("generate-resource")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.Resource)]
    [IgnoreAntiforgeryToken]
    public async Task GenerateResource([FromBody] GenerateSpecialResourceInputDto input)
        => await WriteSseAsync(chunk => _resourceService.GenerateStreamingAsync(input, chunk));

    [HttpPost("export-resource-docx")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.Default)]
    [IgnoreAntiforgeryToken]
    public IActionResult ExportResourceDocx([FromBody] SaveSpecialResourceInputDto input)
    {
        var bytes = _resourceService.ExportDocx(input.ResultJson, input.Modality);
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"特教资源_{DateTime.Now:yyyyMMdd_HHmmss}.docx");
    }

    [HttpPost("batch-export-resources")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.Resource)]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> BatchExport([FromBody] BatchExportSpecialResourceInputDto input)
    {
        var bytes = await _resourceService.BatchExportAsync(input.Ids);
        return File(bytes, "application/zip", $"特教资源批量_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
    }

    // ── 全局管理（host） ──
    [HttpGet("admin/tenants")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.Manage)]
    public Task<List<SpecialEduTenantStateDto>> GetTenantStates() => _adminService.GetTenantStatesAsync();

    [HttpPost("admin/tenants/enabled")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.Manage)]
    [IgnoreAntiforgeryToken]
    public Task SetTenantEnabled([FromBody] SetSpecialEduTenantEnabledDto input) => _adminService.SetTenantEnabledAsync(input);

    [HttpPost("admin/seed-mock-data")]
    [Authorize(KnowledgeHubPermissions.SpecialEducation.Manage)]
    [IgnoreAntiforgeryToken]
    public Task<SeedMockDataResultDto> SeedMockData([FromBody] SeedMockDataInputDto input) => _adminService.SeedMockDataAsync(input ?? new SeedMockDataInputDto());

    private async Task WriteSseAsync(Func<Func<ChatMessageChunkDto, Task>, Task> generate)
    {
        var ctx = HttpContext;
        ctx.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers["Cache-Control"] = "no-cache";
        ctx.Response.Headers["Connection"] = "keep-alive";
        ctx.Response.Headers["X-Accel-Buffering"] = "no";
        await ctx.Response.StartAsync();
        await generate(async chunk =>
        {
            var json = JsonSerializer.Serialize(chunk, JsonOptions);
            await ctx.Response.WriteAsync($"data: {json}\n\n");
            await ctx.Response.Body.FlushAsync();
        });
    }
}
