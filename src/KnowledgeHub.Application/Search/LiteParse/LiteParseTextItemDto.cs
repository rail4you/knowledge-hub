using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KnowledgeHub.Application.Search.LiteParse;

/// <summary>
/// Liteparse textItems[] 元素的空间感知文本块。
/// 仅用于反序列化 /parse 响应；不是公开 API 的一部分。
/// </summary>
internal class LiteParseTextItemDto
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("width")]
    public float Width { get; set; }

    [JsonPropertyName("height")]
    public float Height { get; set; }

    [JsonPropertyName("fontName")]
    public string? FontName { get; set; }

    [JsonPropertyName("fontSize")]
    public float FontSize { get; set; }

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("rotation")]
    public float Rotation { get; set; }

    [JsonPropertyName("words")]
    public List<object>? Words { get; set; }
}