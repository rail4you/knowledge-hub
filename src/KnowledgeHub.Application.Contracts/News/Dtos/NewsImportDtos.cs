using System;
using System.Collections.Generic;

namespace KnowledgeHub.News.Dtos;

public class NewsImportResultDto
{
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public List<NewsImportFailItemDto> FailItems { get; set; } = new();
}

public class NewsImportFailItemDto
{
    public int RowNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
