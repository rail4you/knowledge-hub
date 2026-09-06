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

    public static readonly string[] All =
    {
        Text, ImageDesc, AudioScript, VideoScript, SocialStory, VisualSupport, BehaviorPlan
    };
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
