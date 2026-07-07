using System;
using System.Threading.Tasks;

namespace KnowledgeHub.Application.Search.LiteParse;

/// <summary>
/// 扩展的文档解析服务接口：在 IDocumentExtractionService 基础上，额外返回页面尺寸与空间感知文本块。
/// 由 DocumentIndexingBackgroundJob / MeiliSearchService.IndexDocumentAsync 等需要落
/// PageContent.PageWidth / PageHeight / TextItemsJson 字段的调用方使用。
/// </summary>
public interface ILiteParseExtractionService
{
    Task<LiteParseExtractionResult> ExtractWithLayoutAsync(Guid resourceId);
}