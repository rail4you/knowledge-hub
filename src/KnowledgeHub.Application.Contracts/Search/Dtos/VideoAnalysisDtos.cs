using System;
using System.Collections.Generic;

namespace KnowledgeHub.Application.Contracts.Search.Dtos;

public class VideoAnalysisRequestDto
{
    public string? FilePath { get; set; }
    public string? VideoUrl { get; set; }
    public string? CustomPrompt { get; set; }
}

public class VideoAnalysisResultDto
{
    public string RawContent { get; set; } = string.Empty;
    public List<VideoTimelineEventDto> Events { get; set; } = new();
    public VideoAnalysisUsageDto? Usage { get; set; }
}

public class VideoTimelineEventDto
{
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Event { get; set; } = string.Empty;
}

public class VideoAnalysisUsageDto
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}
