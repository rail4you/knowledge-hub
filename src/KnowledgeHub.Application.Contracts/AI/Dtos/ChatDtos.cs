using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace KnowledgeHub.Application.AI.Dtos;

public class ChatInputDto
{
    public string Message { get; set; } = string.Empty;
    public string? ThreadId { get; set; }
    public List<FileUrlDto>? FileUrls { get; set; }
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
