using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.Application.Contracts.Search;

namespace KnowledgeHub.Application.Search;

/// <summary>
/// 复合文档解析服务：按文件格式分派到对应的解析器
/// NPOI → docx/pptx/xlsx
/// PdfTextExtractor → pdf
/// </summary>
public class CompositeDocumentExtractionService : IDocumentExtractionService
{
    private readonly NpoiDocumentParserService _npoi;
    private readonly PdfTextExtractorService _pdfExtractor;

    public CompositeDocumentExtractionService(
        NpoiDocumentParserService npoi,
        PdfTextExtractorService pdfExtractor)
    {
        _npoi = npoi;
        _pdfExtractor = pdfExtractor;
    }

    public async Task<List<PageContentDto>> ExtractPagesAsync(Guid resourceId)
    {
        // 先试 NPOI（docx/pptx/xlsx）
        var npoiResult = await _npoi.ExtractPagesAsync(resourceId);
        if (npoiResult.Count > 0)
            return npoiResult;

        // PDF 用 PdfPig
        var pdfResult = await _pdfExtractor.ExtractPagesAsync(resourceId);
        if (pdfResult.Count > 0)
            return pdfResult;

        // 最后尝试 OpenDataLoader（需要 Java 环境）
        return pdfResult;
    }
}
