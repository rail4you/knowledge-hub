using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;

namespace KnowledgeHub.Application.Search;

/// <summary>
/// 复合文档解析服务：按文件格式分派到对应的解析器
/// NPOI 处理 docx/pptx/xlsx（失败或空结果时转 OpenDataLoader）
/// OpenDataLoader 处理 pdf 等
/// </summary>
public class CompositeDocumentExtractionService : IDocumentExtractionService
{
    private readonly NpoiDocumentParserService _npoi;
    private readonly OpenDataLoaderService _openDataLoader;

    public CompositeDocumentExtractionService(
        NpoiDocumentParserService npoi,
        OpenDataLoaderService openDataLoader)
    {
        _npoi = npoi;
        _openDataLoader = openDataLoader;
    }

    public async Task<List<PageContentDto>> ExtractPagesAsync(Guid resourceId)
    {
        // 先试 NPOI（docx/pptx/xlsx）
        var npoiResult = await _npoi.ExtractPagesAsync(resourceId);
        if (npoiResult.Count > 0)
            return npoiResult;

        // 回退到 OpenDataLoader（pdf 等）
        return await _openDataLoader.ExtractPagesAsync(resourceId);
    }
}
