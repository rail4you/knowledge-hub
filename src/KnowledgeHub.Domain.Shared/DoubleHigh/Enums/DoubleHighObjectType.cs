namespace KnowledgeHub.DoubleHigh.Enums;

/// <summary>
/// 评估对象的类型 —— 同时也是「自动取值的上下文范围」。
///
/// <para>
/// 评估对象决定了「系统取数」类指标的数据范围：
/// </para>
/// <list type="bullet">
/// <item><description>
/// <see cref="ResourceLibrary"/>：教学资源库 —— 取该库内的课程、资源等信息；
/// </description></item>
/// <item><description>
/// <see cref="Major"/>：专业 —— 取该专业下的课程、资源等信息；
/// </description></item>
/// <item><description>
/// <see cref="Course"/>：课程 —— 取该课程相关的资源、选课、完成率等信息；
/// </description></item>
/// <item><description>
/// <see cref="Manual"/>：手动填写 —— 对象名由老师自定义，
/// 此时老师需要为每个「系统取数」指标单独确认取值范围（库 / 专业 / 课程 / 全校）。
/// </description></item>
/// </list>
/// </summary>
public enum DoubleHighObjectType
{
    /// <summary>手动填写（老师自定义对象，并逐指标确认取值范围）。</summary>
    Manual = 0,

    /// <summary>教学资源库。</summary>
    ResourceLibrary = 1,

    /// <summary>专业。</summary>
    Major = 2,

    /// <summary>课程。</summary>
    Course = 3
}
