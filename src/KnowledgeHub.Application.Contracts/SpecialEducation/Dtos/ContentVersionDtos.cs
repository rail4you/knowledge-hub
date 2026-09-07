using System;
using System.Collections.Generic;
using KnowledgeHub.SpecialEducation;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.SpecialEducation.Dtos;

/// <summary>版本快照行：CreationTime 即该版本时间，CreatorName 为操作人。</summary>
public class SpecialEduContentVersionDto : CreationAuditedEntityDto<Guid>
{
    public SpecialEduContentType ContentType { get; set; }
    public Guid EntityId { get; set; }
    public int VersionNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>canonical 结构化 JSON（camelCase），前端按字段取历史值。</summary>
    public string SnapshotJson { get; set; } = "{}";
    public string? CreatorName { get; set; }
}

public class TeachingSectionInputDto
{
    public string Name { get; set; } = string.Empty;
    public int Duration { get; set; }
    public string Content { get; set; } = string.Empty;
}

/// <summary>教学设计结构化编辑：ResultJson 为前端合并后的 canonical JSON（用于快照/RawJson/导出）。</summary>
public class UpdateTeachingDesignContentDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public int Duration { get; set; } = 45;
    public List<string> Objectives { get; set; } = new();
    public List<string> KeyPoints { get; set; } = new();
    public List<string> Difficulties { get; set; } = new();
    public List<TeachingSectionInputDto> Sections { get; set; } = new();
    public List<string> Methods { get; set; } = new();
    public List<string> Resources { get; set; } = new();
    public List<string> Assessment { get; set; } = new();
    public List<string> Homework { get; set; } = new();
    public List<string> BoardDesign { get; set; } = new();
    public List<string> SlidesOutline { get; set; } = new();
    public List<string> Activities { get; set; } = new();
    public List<string> AssessmentTools { get; set; } = new();
    public string StandardBasis { get; set; } = string.Empty;
    public string ResultJson { get; set; } = "{}";
}

/// <summary>IEP 结构化编辑。</summary>
public class UpdateIepContentDto
{
    public Guid Id { get; set; }
    public string ProfileSummary { get; set; } = string.Empty;
    public List<string> LongTermGoals { get; set; } = new();
    public List<string> ShortTermGoals { get; set; } = new();
    public List<string> Strategies { get; set; } = new();
    public List<string> Evaluation { get; set; } = new();
    public List<string> HomeSchool { get; set; } = new();
    public string LegalBasis { get; set; } = string.Empty;
    public string ResultJson { get; set; } = "{}";
}

public class ResourcePairInputDto
{
    public string Text { get; set; } = string.Empty;
    public string Pinyin { get; set; } = string.Empty;
    public string Braille { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

/// <summary>多模态资源/盲文对照结构化编辑。</summary>
public class UpdateResourceContentDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<string> Content { get; set; } = new();
    public List<ResourcePairInputDto> Pairs { get; set; } = new();
    public string ResultJson { get; set; } = "{}";
}
