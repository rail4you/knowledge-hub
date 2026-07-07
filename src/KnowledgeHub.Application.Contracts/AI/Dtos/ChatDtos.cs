using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Application.AI.Dtos;

public class ChatInputDto
{
    public string Message { get; set; } = string.Empty;
    public string? ThreadId { get; set; }
    public Guid? ResourceId { get; set; }
    public List<FileUrlDto>? FileUrls { get; set; }
}

public class ResourceForChatDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? FileExtension { get; set; }
    public string? SourceFormat { get; set; }
    public int NodeCount { get; set; }
    /// <summary>是否已生成页面索引（支持文档问答）</summary>
    public bool HasPageIndex { get; set; }
    /// <summary>资源所属分类 ID（null 表示未分类）。</summary>
    public Guid? CategoryId { get; set; }
    /// <summary>资源所属分类名称（冗余字段，便于前端树状视图直接展示）。</summary>
    public string? CategoryName { get; set; }
}

public class FileUrlDto
{
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class ChatThreadDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class ChatMessageDto
{
    public string Id { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ChatMessageChunkDto
{
    public string Content { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
}
