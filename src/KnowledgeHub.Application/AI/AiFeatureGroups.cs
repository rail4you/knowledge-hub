using System;

namespace KnowledgeHub.Application.AI;

/// <summary>AI 功能分组：用量记录 + 每日配额共用维度。</summary>
public static class AiFeatureGroups
{
    public const string CareerGuidance = "CareerGuidance";
    public const string LessonPlan = "LessonPlan";
    public const string CaseAnalysis = "CaseAnalysis";
    public const string ExerciseGenerate = "ExerciseGenerate";
    public const string ImageGeneration = "ImageGeneration";
    public const string VideoGeneration = "VideoGeneration";
    public const string Chat = "Chat";
    public const string Video = "Video";
    public const string Summary = "Summary";

    public static string Label(string group) => group switch
    {
        CareerGuidance => "职业规划生成",
        LessonPlan => "教案生成",
        CaseAnalysis => "案例分析",
        ExerciseGenerate => "习题生成",
        ImageGeneration => "教学图片生成",
        VideoGeneration => "教学短视频生成",
        Chat => "AI 对话",
        Video => "视频理解",
        Summary => "文档摘要",
        _ => group,
    };
}

/// <summary>用量记录状态。</summary>
public static class AiUsageStatus
{
    public const byte Running = 0;
    public const byte Completed = 10;
    public const byte Failed = 40;
}

/// <summary>
/// 媒体生成计费（元）：图片按张、视频按秒计费。
/// 单价为估算值，仅供成本参考；调价时同步这里。
/// </summary>
public static class AiMediaPricing
{
    public static decimal ImageCost(string model) => model switch
    {
        "wan2.2-t2i-flash" => 0.14m,
        "wan2.2-t2i-plus" => 0.50m,
        _ => 0.20m,
    };

    public static decimal VideoCost(string model, int durationSeconds)
    {
        var seconds = Math.Max(1, durationSeconds);
        var perSecond = model switch
        {
            "wan2.2-i2v-flash" => 0.10m,
            _ => 0.20m,
        };
        return perSecond * seconds;
    }
}
