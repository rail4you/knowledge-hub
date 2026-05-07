using System;

namespace KnowledgeHub.Courses.Dtos;

/// <summary>
/// 章节排序更新Dto
/// </summary>
public class ChapterOrderDto
{
    /// <summary>
    /// 章节ID
    /// </summary>
    public Guid ChapterId { get; set; }

    /// <summary>
    /// 排序值（越小越靠前）
    /// </summary>
    public int SortOrder { get; set; }
}