using System;
using System.Collections.Generic;

namespace KnowledgeHub.Application.AI.Dtos;

public class CaseAnalysisGenerationInputDto
{
    public Guid ResourceId { get; set; }
    public string? FocusArea { get; set; }
}

public class CaseAnalysisExportInputDto
{
    public string CaseAnalysisJson { get; set; } = string.Empty;
}

public class CaseAnalysisDto
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public CaseBackgroundDto Background { get; set; } = new();
    public List<CaseKeyIssueDto> KeyIssues { get; set; } = new();
    public List<CaseSolutionDto> Solutions { get; set; } = new();
    public List<string> KeyInsights { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class CaseBackgroundDto
{
    public string Industry { get; set; } = string.Empty;
    public string Timeframe { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public List<string> Stakeholders { get; set; } = new();
}

public class CaseKeyIssueDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}

public class CaseSolutionDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Steps { get; set; } = new();
    public string ExpectedOutcome { get; set; } = string.Empty;
}
