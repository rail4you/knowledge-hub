using System.Collections.Generic;

namespace KnowledgeHub.Application.Search.LiteParse;

/// <summary>
/// LiteParse 文档解析结果（除文本外，还包含页面尺寸与空间感知文本块）。
/// 与 PageContentDto 解耦：仅 PageNumber/Content/Title 进 PageContentDto，
/// PageWidth/PageHeight/TextItemsJson 直接写入 PageContent 实体。
/// </summary>
public class LiteParseExtractionResult
{
    /// <summary>
    /// 页面级公开 DTO（与 IDocumentExtractionService.ExtractPagesAsync 返回类型一致）。
    /// </summary>
    public List<KnowledgeHub.Application.Contracts.Search.PageContentDto> Pages { get; set; } = new();

    /// <summary>
    /// 与 Pages 一一对应的页面宽度（pt，0 表示 liteparse 未返回）。
    /// </summary>
    public List<float> PageWidths { get; set; } = new();

    /// <summary>
    /// 与 Pages 一一对应的页面高度（pt，0 表示 liteparse 未返回）。
    /// </summary>
    public List<float> PageHeights { get; set; } = new();

    /// <summary>
    /// 与 Pages 一一对应的 textItems[] JSON 字符串（null 表示该页 liteparse 未返回 textItems）。
    /// </summary>
    public List<string?> TextItemsJson { get; set; } = new();
}