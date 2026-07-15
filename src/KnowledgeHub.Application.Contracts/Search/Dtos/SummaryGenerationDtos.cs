using System;
using System.Collections.Generic;
using KnowledgeHub.Resources.Enums;

namespace KnowledgeHub.Application.Contracts.Search.Dtos;

public class GenerateSummaryInputDto
{
    public Guid ResourceId { get; set; }
}

public class GenerateSummaryResultDto
{
    public int EnqueuedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> JobIds { get; set; } = new();
}

public class BatchGenerateSummaryInputDto
{
    public int BatchSize { get; set; } = 20;
    public bool SkipExisting { get; set; } = true;
    public bool OnlyApproved { get; set; } = true;
    public List<Guid>? ResourceIds { get; set; }
    public ResourceType? ResourceType { get; set; }
}

public class BatchGenerateSummaryResultDto
{
    public int EnqueuedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<Guid> EnqueuedResourceIds { get; set; } = new();
    public List<Guid> SkippedResourceIds { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}