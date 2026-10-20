namespace KnowledgeHub.LearningAnalytics.Enums;

/// <summary>知识点状态：归一化灰区需老师确认</summary>
public enum KnowledgePointStatus
{
    Confirmed = 0,
    PendingReview = 1
}

/// <summary>知识点来源 / 习题-知识点映射来源</summary>
public enum KpMapSource
{
    /// <summary>系统按章节 / 知识资源初始化</summary>
    Seed = 0,
    /// <summary>LLM 抽取</summary>
    Llm = 1,
    /// <summary>老师手工维护</summary>
    Manual = 2
}

/// <summary>
/// 知识点生成模式（AI 抽取 / 重新抽取 / 重建语义索引 的统一入口）。
/// </summary>
public enum KpGenerationMode
{
    /// <summary>
    /// 全量刷新：章节 → 知识点、习题 → 知识点关联 全量跑一遍，并按需重建语义索引。
    /// </summary>
    Full = 0,
    /// <summary>仅习题：只处理还没有 AI 关联的题目（增量）。</summary>
    ExerciseIncremental = 1,
    /// <summary>仅习题：覆盖已有的 AI 关联（重新抽取）。</summary>
    ExerciseOverwrite = 2
}

/// <summary>作答记录来源</summary>
public enum AnswerSource
{
    /// <summary>应用内在线练习（StudentExerciseRecord）</summary>
    Online = 0,
    /// <summary>课堂练习</summary>
    Classroom = 1,
    /// <summary>纸质试卷（OCR 校对后）</summary>
    Paper = 2
}

/// <summary>先修关系来源</summary>
public enum PrerequisiteSource
{
    /// <summary>老师手工配置（可信度最高）</summary>
    Manual = 0,
    /// <summary>数据挖掘（相关不等于因果，需确认）</summary>
    Mined = 1,
    /// <summary>LLM 语义复核</summary>
    Llm = 2
}

/// <summary>先修关系状态</summary>
public enum PrerequisiteStatus
{
    Proposed = 0,
    Confirmed = 1
}

/// <summary>试卷批次状态</summary>
public enum PaperBatchStatus
{
    Draft = 0,
    OcrRunning = 1,
    NeedConfirm = 2,
    Confirmed = 3,
    Pushed = 4
}

/// <summary>题目与题库原题的匹配状态</summary>
public enum PaperMatchStatus
{
    Matched = 0,
    Low = 1,
    None = 2
}

/// <summary>报告范围</summary>
public enum AnalysisScope
{
    Student = 0,
    Class = 1,
    Teaching = 2
}

/// <summary>教学诊断类别</summary>
public enum DiagnosisLevel
{
    /// <summary>达成</summary>
    Good = 0,
    /// <summary>一般</summary>
    Fair = 1,
    /// <summary>讲解不足</summary>
    TeachingGap = 2,
    /// <summary>两极分化</summary>
    Polarized = 3
}

/// <summary>站内消息类型</summary>
public enum AnalysisNotificationType
{
    PaperWeakness = 0,
    PreCourseReview = 1,
    TeachingInsight = 2,
    Custom = 9
}

/// <summary>衔接复习包状态</summary>
public enum ReviewPackageStatus
{
    Draft = 0,
    Pushed = 1,
    Completed = 2
}

/// <summary>学习路径排序策略</summary>
public enum PathStrategy
{
    /// <summary>只学薄弱的，最弱的先</summary>
    WeakFirst = 0,
    /// <summary>按教学大纲顺序</summary>
    Syllabus = 1,
    /// <summary>混合：先修在前，同层影响面大 / 掌握度低优先</summary>
    Mixed = 2
}

/// <summary>试卷图片 OCR 处理状态</summary>
public enum PaperUploadStatus
{
    Pending = 0,
    Processing = 1,
    Done = 2,
    Failed = 3
}

/// <summary>归档报告的类型</summary>
public enum AnalysisReportFileType
{
    /// <summary>班级学情 + 教学诊断（Word）</summary>
    ClassDocx = 0,
    /// <summary>学生薄弱点汇总（Word）</summary>
    StudentsDocx = 1,
    /// <summary>学生薄弱点明细（Excel）</summary>
    StudentsExcel = 2
}
