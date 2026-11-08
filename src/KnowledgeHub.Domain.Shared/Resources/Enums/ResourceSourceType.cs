namespace KnowledgeHub.Resources.Enums;

/// <summary>
/// 资源来源：用于区分「新建」与「从另一侧转换而来」的资源。
///
/// <para>
/// 平台把 <b>课程资源</b> 与 <b>教学资源库资源</b> 当作两类独立记录处理：
/// 转换 = 复制一份新记录（带来源标记），从此两侧各自维护、互不同步。
/// 这样课程资源不会被"直接共享"，库资源也天然独立于课程。
/// </para>
/// </summary>
public enum ResourceSourceType : byte
{
    /// <summary>新建（上传 / AI 生成 / 运营导入）。</summary>
    Created = 0,

    /// <summary>从课程资源转换而来（副本；来源记在 SourceResourceId / SourceCourseId）。</summary>
    FromCourse = 1,

    /// <summary>从教学资源库资源转换而来（副本；来源记在 SourceResourceId / SourceLibraryId）。</summary>
    FromLibrary = 2
}
