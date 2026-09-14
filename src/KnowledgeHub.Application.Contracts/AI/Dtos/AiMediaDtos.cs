using System.ComponentModel.DataAnnotations;

namespace KnowledgeHub.Application.AI.Dtos;

/// <summary>教学图片生成入参（通义万相 text2image）。</summary>
public class ImageGenerationInputDto
{
    /// <summary>提示词，建议描述教学场景。最长 500 字符。</summary>
    [Required]
    [StringLength(500)]
    public string Prompt { get; set; } = string.Empty;

    /// <summary>输出尺寸，如 1024*1024 / 1280*720 / 720*1280，默认 1024*1024。</summary>
    [StringLength(20)]
    public string? Size { get; set; }

    /// <summary>负向提示词（可选）。</summary>
    [StringLength(500)]
    public string? NegativePrompt { get; set; }
}

/// <summary>教学短视频生成入参（通义万相 image2video：以图片为首帧 + 提示词）。</summary>
public class VideoGenerationInputDto
{
    /// <summary>首帧图片地址（通常是本系统 AI 生成的图片 URL）。</summary>
    [Required]
    [StringLength(2000)]
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>运镜/动作提示词，建议描述教学动作。最长 500 字符。</summary>
    [Required]
    [StringLength(500)]
    public string Prompt { get; set; } = string.Empty;

    /// <summary>视频时长（秒），最长 5 秒。</summary>
    public int Duration { get; set; } = 5;
}

/// <summary>异步媒体生成任务状态（前端轮询）。</summary>
public class MediaGenerationTaskDto
{
    public string TaskId { get; set; } = string.Empty;

    /// <summary>PENDING / RUNNING / SUCCEEDED / FAILED。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>图片生成成功后的地址。</summary>
    public string? ImageUrl { get; set; }

    /// <summary>视频生成成功后的地址。</summary>
    public string? VideoUrl { get; set; }

    /// <summary>失败原因。</summary>
    public string? Error { get; set; }
}
