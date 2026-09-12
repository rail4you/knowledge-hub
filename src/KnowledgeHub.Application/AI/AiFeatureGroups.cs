namespace KnowledgeHub.Application.AI;

/// <summary>AI 功能分组：用量记录 + 每日配额共用维度。</summary>
public static class AiFeatureGroups
{
    public const string CareerGuidance = "CareerGuidance";
    public const string LessonPlan = "LessonPlan";
    public const string CaseAnalysis = "CaseAnalysis";
    public const string ExerciseGenerate = "ExerciseGenerate";
    public const string Chat = "Chat";
    public const string Video = "Video";
    public const string Summary = "Summary";

    public static string Label(string group) => group switch
    {
        CareerGuidance => "职业规划生成",
        LessonPlan => "教案生成",
        CaseAnalysis => "案例分析",
        ExerciseGenerate => "习题生成",
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
