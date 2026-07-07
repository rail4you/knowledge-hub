using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KnowledgeHub.Application.Search.LiteParse;

/// <summary>
/// Liteparse /parse 端点返回的顶层结构。
/// 仅用于反序列化 HTTP 响应；不是公开 API 的一部分。
/// </summary>
internal class LiteParseResponseDto
{
    [JsonPropertyName("pages")]
    public List<LiteParsePageDto>? Pages { get; set; }
}

internal class LiteParsePageDto
{
    [JsonPropertyName("pageNum")]
    public int PageNum { get; set; }

    [JsonPropertyName("width")]
    public float Width { get; set; }

    [JsonPropertyName("height")]
    public float Height { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("markdown")]
    public string? Markdown { get; set; }

    [JsonPropertyName("textItems")]
    public List<LiteParseTextItemDto>? TextItems { get; set; }
}