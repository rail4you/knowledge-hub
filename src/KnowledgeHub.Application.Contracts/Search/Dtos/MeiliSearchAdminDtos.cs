using System;
using System.Collections.Generic;

namespace KnowledgeHub.Application.Contracts.Search.Dtos;

public class MeiliDashboardDto
{
    public MeiliHealthDto Health { get; set; } = new();
    public MeiliVersionDto Version { get; set; } = new();
    public MeiliStatsDto Stats { get; set; } = new();
    public List<MeiliIndexDto> Indexes { get; set; } = new();
    public Dictionary<string, MeiliEmbedderDto> Embedders { get; set; } = new();
    public List<MeiliTaskDto> RecentTasks { get; set; } = new();
}

public class MeiliHealthDto
{
    public string Status { get; set; } = "unknown";
}

public class MeiliVersionDto
{
    public string CommitSha { get; set; } = string.Empty;
    public string CommitDate { get; set; } = string.Empty;
    public string PkgVersion { get; set; } = string.Empty;
}

public class MeiliStatsDto
{
    public long DatabaseSize { get; set; }
    public long UsedDatabaseSize { get; set; }
    public DateTime? LastUpdate { get; set; }
    public Dictionary<string, MeiliIndexStatsDto> Indexes { get; set; } = new();
}

public class MeiliIndexStatsDto
{
    public long NumberOfDocuments { get; set; }
    public bool IsIndexing { get; set; }
    public Dictionary<string, long> FieldDistribution { get; set; } = new();
}

public class MeiliIndexDto
{
    public string Uid { get; set; } = string.Empty;
    public string? PrimaryKey { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MeiliEmbedderDto
{
    public string Source { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? Model { get; set; }
    public int? Dimensions { get; set; }
    public string? DocumentTemplate { get; set; }
}

public class MeiliTaskDto
{
    public long Uid { get; set; }
    public string? IndexUid { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime? EnqueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

public class MeiliDocumentGroupDto
{
    public string ResourceName { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string? FileExtension { get; set; }
    public int PageCount { get; set; }
    public List<MeiliDocumentPageDto> Pages { get; set; } = new();

    /// <summary>"document" | "video"</summary>
    public string ResourceType { get; set; } = "document";
    public string? VideoUrl { get; set; }
}

public class MeiliDocumentPageDto
{
    public string Id { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string? PageTitle { get; set; }
    public string? EventDescription { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
}
