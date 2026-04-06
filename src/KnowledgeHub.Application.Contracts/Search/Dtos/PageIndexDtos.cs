using System;

namespace KnowledgeHub.Application.Contracts.Search.Dtos;

public class ResourcePageIndexDto
{
    public Guid Id { get; set; }
    public Guid ResourceId { get; set; }
    public Guid ResourceVersionId { get; set; }
    public string PageIndexJson { get; set; } = string.Empty;
    public string? SourceFormat { get; set; }
    public string? Model { get; set; }
    public int NodeCount { get; set; }
}

public class PageIndexSearchResultDto
{
    public Guid ResourceId { get; set; }
    public string? ResourceName { get; set; }
    public string? NodeTitle { get; set; }
    public string? NodeSummary { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public int StartIndex { get; set; }
    public int EndIndex { get; set; }
    public string? DocDescription { get; set; }
}
