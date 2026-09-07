namespace KnowledgeHub.SpecialEducation;

/// <summary>特殊教育类别：固定四类（培智、听障、视障、孤独症）。</summary>
public enum SpecialEduCategory
{
    Peizhi = 0,
    TingZhang = 1,
    ShiZhang = 2,
    Guzuzheng = 3
}

/// <summary>方案状态机：草稿 → 待审核 → 已审核/已发布 → 归档。</summary>
public enum SpecialEduPlanStatus
{
    Draft = 0,
    PendingReview = 1,
    Reviewed = 2,
    Published = 3,
    Archived = 4
}

/// <summary>多模态资源类型。</summary>
public static class SpecialEduResourceModality
{
    public const string Text = "Text";
    public const string ImageDesc = "ImageDesc";
    public const string AudioScript = "AudioScript";
    public const string VideoScript = "VideoScript";
    public const string SocialStory = "SocialStory";
    public const string VisualSupport = "VisualSupport";
    public const string BehaviorPlan = "BehaviorPlan";
    /// <summary>盲文对照：盲文点位显示 + 翻译对照（Unicode 盲文 + 点位图解 + 明文）。</summary>
    public const string BrailleParallel = "BrailleParallel";

    public static readonly string[] All =
    {
        Text, ImageDesc, AudioScript, VideoScript, SocialStory, VisualSupport, BehaviorPlan, BrailleParallel
    };
}

/// <summary>结构化内容归属：版本历史表用此区分三类主体。</summary>
public enum SpecialEduContentType
{
    TeachingDesign = 0,
    Iep = 1,
    Resource = 2
}

public static class SpecialEduCategoryNames
{
    public static string ToDisplayName(SpecialEduCategory category) => category switch
    {
        SpecialEduCategory.Peizhi => "培智",
        SpecialEduCategory.TingZhang => "听障",
        SpecialEduCategory.ShiZhang => "视障",
        SpecialEduCategory.Guzuzheng => "孤独症",
        _ => category.ToString()
    };
}
