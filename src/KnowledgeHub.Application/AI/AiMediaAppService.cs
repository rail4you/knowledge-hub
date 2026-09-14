using System;
using System.Threading.Tasks;
using KnowledgeHub.Application.AI.Dtos;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;

namespace KnowledgeHub.Application.AI;

/// <summary>
/// 教学图片 / 短视频生成 HTTP 服务：校验入参、检查每日配额、记录用量，然后交给
/// <see cref="IAiMediaGenerator"/> 提交到阿里云百炼（通义万相）。
/// 采用"提交任务 + 前端轮询"模式，避免长时间占用 HTTP 连接。
/// </summary>
[Authorize]
public class AiMediaAppService : KnowledgeHubAppService
{
    private readonly IAiMediaGenerator _generator;
    private readonly IAiQuotaService _quotaService;
    private readonly IAiUsageTracker _usageTracker;

    public AiMediaAppService(
        IAiMediaGenerator generator,
        IAiQuotaService quotaService,
        IAiUsageTracker usageTracker)
    {
        _generator = generator;
        _quotaService = quotaService;
        _usageTracker = usageTracker;
    }

    /// <summary>提交文生图任务，立即返回 DashScope 任务号。</summary>
    public async Task<MediaGenerationTaskDto> GenerateImageAsync(ImageGenerationInputDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Prompt))
        {
            throw new UserFriendlyException("请输入图片提示词");
        }

        await _quotaService.CheckAsync(AiFeatureGroups.ImageGeneration);

        var model = _generator.ImageModel;
        var usageId = await _usageTracker.StartAsync(
            AiFeatureGroups.ImageGeneration, "GenerateImage", model, input.Prompt);
        try
        {
            var task = await _generator.SubmitImageAsync(input.Prompt, input.Size, input.NegativePrompt);
            await _usageTracker.CompleteAsync(
                usageId, task.TaskId, true, fixedCost: AiMediaPricing.ImageCost(model));
            return task;
        }
        catch (Exception ex)
        {
            await _usageTracker.CompleteAsync(usageId, null, false, ex.Message);
            throw;
        }
    }

    /// <summary>提交图生视频任务（首帧图片 + 提示词），立即返回 DashScope 任务号。</summary>
    public async Task<MediaGenerationTaskDto> GenerateVideoAsync(VideoGenerationInputDto input)
    {
        if (string.IsNullOrWhiteSpace(input.ImageUrl))
        {
            throw new UserFriendlyException("请先生成或选择首帧图片");
        }
        if (string.IsNullOrWhiteSpace(input.Prompt))
        {
            throw new UserFriendlyException("请输入视频提示词");
        }

        await _quotaService.CheckAsync(AiFeatureGroups.VideoGeneration);

        var model = _generator.VideoModel;
        var usageId = await _usageTracker.StartAsync(
            AiFeatureGroups.VideoGeneration, "GenerateVideo", model, input.Prompt);
        try
        {
            var task = await _generator.SubmitVideoAsync(input.ImageUrl, input.Prompt, input.Duration);
            await _usageTracker.CompleteAsync(
                usageId, task.TaskId, true, fixedCost: AiMediaPricing.VideoCost(model, input.Duration));
            return task;
        }
        catch (Exception ex)
        {
            await _usageTracker.CompleteAsync(usageId, null, false, ex.Message);
            throw;
        }
    }

    /// <summary>查询媒体生成任务状态与结果（前端轮询）。</summary>
    public Task<MediaGenerationTaskDto> GetMediaTaskAsync(string taskId)
    {
        return _generator.GetTaskAsync(taskId);
    }
}
