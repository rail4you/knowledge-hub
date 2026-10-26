namespace KnowledgeHub.ResourceLibraries;

/// <summary>
/// 教学资源库的内容类型。
///
/// <para>
/// 需求「资源库是一系列资源（文件资源）、课程、实训的关联集合，
/// 不包含微专业、就业、咨询、双高等数据」——教学资源库只聚合这三类内容。
/// </para>
/// </summary>
public enum LibraryContentType : byte
{
    /// <summary>文件资源（<c>Resource</c>）。</summary>
    Resource = 0,

    /// <summary>课程（<c>Course</c>）。</summary>
    Course = 1,

    /// <summary>实训（<c>PracticumProject</c>）。</summary>
    Practicum = 2
}
