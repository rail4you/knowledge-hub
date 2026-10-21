using System;
using System.Collections.Generic;

namespace KnowledgeHub.LearningAnalytics.Dtos;

// ══════════════════════════════════════════════════════════
//  通用
// ══════════════════════════════════════════════════════════

/// <summary>知识点掌握项（0 薄弱 / 1 待巩固 / 2 达标）</summary>
public class KnowledgeMasteryItemDto
{
    public Guid KnowledgePointId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ChapterTitle { get; set; }
    /// <summary>掌握度 0~1</summary>
    public double Mastery { get; set; }
    /// <summary>有效作答量</summary>
    public double Evidence { get; set; }
    /// <summary>0 薄弱 / 1 待巩固 / 2 达标</summary>
    public int Level { get; set; }
    /// <summary>是否为矩阵补全的预测值</summary>
    public bool IsPredicted { get; set; }
    /// <summary>该知识点生效的达标线（知识点级覆盖优先，其次课程级）</summary>
    public double TargetLine { get; set; } = 0.75;
    /// <summary>是否使用了知识点级判定线覆盖</summary>
    public bool HasCustomThreshold { get; set; }
    /// <summary>是否数据不足（证据 &lt; 2）</summary>
    public bool InsufficientData { get; set; }
}

/// <summary>失分归因</summary>
public class LossAttributionDto
{
    public string QuestionNo { get; set; } = string.Empty;
    public Guid? ExerciseId { get; set; }
    public string ExerciseTitle { get; set; } = string.Empty;
    public string KnowledgePointName { get; set; } = string.Empty;
    public double Mastery { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Advice { get; set; } = string.Empty;
}

// ══════════════════════════════════════════════════════════
//  学生端
// ══════════════════════════════════════════════════════════

public class StudentWeaknessOverviewDto
{
    public string StudentName { get; set; } = string.Empty;
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public Guid? PaperBatchId { get; set; }
    public string PaperTitle { get; set; } = string.Empty;
    public double? Score { get; set; }
    public double? ClassAverage { get; set; }
    public int? ClassRank { get; set; }
    public int? ClassSize { get; set; }
    public bool Confirmed { get; set; }
    public string SourceLabel { get; set; } = string.Empty;
    public int WeakCount { get; set; }
    public List<KnowledgeMasteryItemDto> KnowledgePoints { get; set; } = new();
    public List<LossAttributionDto> Losses { get; set; } = new();
}

/// <summary>课程路径节点（0 已完成 / 1 进行中 / 2 待学习）</summary>
public class CoursePathNodeDto
{
    public Guid CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public int State { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string? Note { get; set; }
    public List<string> PrerequisiteNames { get; set; } = new();
}

public class ReviewPackageItemDto
{
    public Guid KnowledgePointId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>知识点所属课程（前置课程）</summary>
    public Guid CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public double Mastery { get; set; }
    public int Level { get; set; }
    public bool IsCompleted { get; set; }
}

public class ReviewPackageProgressDto
{
    public Guid ReviewPackageId { get; set; }
    public Guid UpcomingCourseId { get; set; }
    public string UpcomingCourseTitle { get; set; } = string.Empty;
    public int Completed { get; set; }
    public int Total { get; set; }
    public List<ReviewPackageItemDto> Items { get; set; } = new();
}

public class AnalysisNotificationDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
}

public class PracticeItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;
    public int ExerciseCount { get; set; }
    public Guid? KnowledgePointId { get; set; }
    public bool FromPrerequisite { get; set; }
    public string? Reason { get; set; }
    /// <summary>推荐到的习题 Id（前端可用其直接跳转练习）</summary>
    public List<Guid> ExerciseIds { get; set; } = new();
}

public class StudentAnalysisDashboardDto
{
    public StudentWeaknessOverviewDto Overview { get; set; } = new();
    public List<CoursePathNodeDto> Path { get; set; } = new();
    public ReviewPackageProgressDto? ReviewProgress { get; set; }
    public List<AnalysisNotificationDto> Notifications { get; set; } = new();
    public int UnreadCount { get; set; }
    public List<PracticeItemDto> Practices { get; set; } = new();
}

// ══════════════════════════════════════════════════════════
//  教师端 · 试卷
// ══════════════════════════════════════════════════════════

public class CreatePaperBatchInput
{
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime ExamDate { get; set; } = DateTime.UtcNow;
    public List<string> FileNames { get; set; } = new();
}

public class PaperBatchDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public DateTime ExamDate { get; set; }
    public int ScriptCount { get; set; }
    public int NeedsConfirmCount { get; set; }
    public int PushedCount { get; set; }
    public double AverageScore { get; set; }
    /// <summary>0 待识别 / 1 识别中 / 2 待校对 / 3 已确认 / 4 已推送</summary>
    public int Status { get; set; }
    /// <summary>已上传图片数</summary>
    public int UploadedCount { get; set; }
    /// <summary>OCR 已处理图片数</summary>
    public int ProcessedCount { get; set; }
    /// <summary>OCR 失败图片数</summary>
    public int FailedCount { get; set; }
}

public class PaperAnalysisOverviewDto
{
    public int BatchCount { get; set; }
    public int ScriptCount { get; set; }
    public double AverageScore { get; set; }
    public int PendingConfirmCount { get; set; }
    public int PushedCount { get; set; }
    public List<PaperBatchDto> Batches { get; set; } = new();
}

public class PaperItemRowDto
{
    public Guid Id { get; set; }
    public string QuestionNo { get; set; } = string.Empty;
    public Guid? ExerciseId { get; set; }
    public string ExerciseTitle { get; set; } = string.Empty;
    /// <summary>matched / low / none</summary>
    public string MatchStatus { get; set; } = "none";
    public double Score { get; set; }
    public double FullScore { get; set; }
    public List<string> KnowledgePointNames { get; set; } = new();
    public double OcrConfidence { get; set; }
}

public class PaperScriptRowDto
{
    public Guid Id { get; set; }
    public Guid? StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentNumber { get; set; } = string.Empty;
    public double TotalScore { get; set; }
    public double OcrConfidence { get; set; }
    public bool Confirmed { get; set; }
    public List<PaperItemRowDto> Items { get; set; } = new();
}

/// <summary>校对提交：老师修正后的题目得分</summary>
public class ConfirmPaperScriptInput
{
    public double TotalScore { get; set; }
    public List<ConfirmPaperItemInput> Items { get; set; } = new();
}

public class ConfirmPaperItemInput
{
    public Guid Id { get; set; }
    public string? QuestionNo { get; set; }
    public Guid? ExerciseId { get; set; }
    public double Score { get; set; }
    public double FullScore { get; set; }
}

public class PushResultDto
{
    public int PushedCount { get; set; }
}

// ══════════════════════════════════════════════════════════
//  教师端 · 分析
// ══════════════════════════════════════════════════════════

public class FocusStudentDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public double TotalScore { get; set; }
    public List<string> WeakestNames { get; set; } = new();
    public bool Pushed { get; set; }
}

public class ClassWeaknessDto
{
    public Guid BatchId { get; set; }
    public string BatchTitle { get; set; } = string.Empty;
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public double TargetLine { get; set; } = 0.75;
    public List<KnowledgeMasteryItemDto> KnowledgePoints { get; set; } = new();
    public List<FocusStudentDto> FocusStudents { get; set; } = new();
    public int PushedCount { get; set; }
    public int TotalStudents { get; set; }
}

public class ChapterStatDto
{
    public Guid? ChapterId { get; set; }
    public string Title { get; set; } = string.Empty;
    public double Rate { get; set; }
    public double StdDev { get; set; }
    public double TopQuartile { get; set; }
    public int SampleSize { get; set; }
}

public class TeachingDiagnosisDto
{
    /// <summary>good / fair / gap / polarized</summary>
    public string Level { get; set; } = "fair";
    public string ChapterTitle { get; set; } = string.Empty;
    public double Rate { get; set; }
    /// <summary>相对教学目标的差值</summary>
    public double Gap { get; set; }
    public string Advice { get; set; } = string.Empty;
}

public class TeachingInsightDto
{
    public Guid BatchId { get; set; }
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public double AverageRate { get; set; }
    /// <summary>较上一届变化（百分点/100）</summary>
    public double DeltaVsPrevious { get; set; }
    public int BelowTargetCount { get; set; }
    public double TargetLine { get; set; } = 0.75;
    public List<ChapterStatDto> Chapters { get; set; } = new();
    public List<TeachingDiagnosisDto> Diagnoses { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

// ══════════════════════════════════════════════════════════
//  教师端 · 课程联动
// ══════════════════════════════════════════════════════════

public class PrerequisiteMappingDto
{
    public Guid Id { get; set; }
    public string SuccessorChapterTitle { get; set; } = string.Empty;
    public List<string> DependencyNames { get; set; } = new();
    public string SourceCourseTitle { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    /// <summary>0 待确认 / 1 已生效</summary>
    public int Status { get; set; }
    /// <summary>Manual / Mined / Llm</summary>
    public string Source { get; set; } = "Manual";
    /// <summary>后续知识点 Id（编辑用）</summary>
    public Guid? ToKpId { get; set; }
    /// <summary>前置知识点 Id 列表（编辑用）</summary>
    public List<Guid> FromKpIds { get; set; } = new();
}

public class CourseLinkConfigDto
{
    public Guid CourseId { get; set; }
    public string CourseName { get; set; } = string.Empty;
    public List<CoursePathNodeDto> Path { get; set; } = new();
    public List<PrerequisiteMappingDto> Mappings { get; set; } = new();
    /// <summary>已配置的课程前后关系</summary>
    public List<CourseLinkDto> Links { get; set; } = new();
}

public class CourseLinkDto
{
    public Guid Id { get; set; }
    public Guid PredecessorCourseId { get; set; }
    public string PredecessorCourseTitle { get; set; } = string.Empty;
    public Guid SuccessorCourseId { get; set; }
    public string SuccessorCourseTitle { get; set; } = string.Empty;
}

public class SaveCourseLinkInput
{
    public Guid PredecessorCourseId { get; set; }
    public Guid SuccessorCourseId { get; set; }
}

public class SavePrerequisiteInput
{
    public Guid? Id { get; set; }
    public Guid CourseId { get; set; }
    /// <summary>后续知识点（必填）</summary>
    public Guid? ToKpId { get; set; }
    /// <summary>后续课程章节标题（未指定 ToKpId 时用于展示）</summary>
    public string? SuccessorChapterTitle { get; set; }
    public List<Guid> FromKpIds { get; set; } = new();
    public List<string> DependencyNames { get; set; } = new();
    public string? SourceCourseTitle { get; set; }
    public string? Condition { get; set; }
    /// <summary>Manual / Mined / Llm</summary>
    public string Source { get; set; } = "Manual";
    /// <summary>0 待确认 / 1 已生效</summary>
    public int Status { get; set; }
}

// ══════════════════════════════════════════════════════════
//  知识点 / 判定线
// ══════════════════════════════════════════════════════════

public class KnowledgePointDto
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public Guid? ChapterId { get; set; }
    public string? ChapterTitle { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SyllabusOrder { get; set; }
    public int Status { get; set; }
    public string Source { get; set; } = "Seed";
    public Guid? CanonicalId { get; set; }
}

public class SetThresholdInput
{
    public Guid CourseId { get; set; }
    public Guid? KnowledgePointId { get; set; }
    public double WeakLine { get; set; } = 0.60;
    public double OkLine { get; set; } = 0.75;
}

public class MasteryThresholdDto
{
    public Guid? Id { get; set; }
    public Guid CourseId { get; set; }
    public Guid? KnowledgePointId { get; set; }
    public double WeakLine { get; set; }
    public double OkLine { get; set; }
    /// <summary>没有任何显式配置（使用系统默认 0.60 / 0.75）</summary>
    public bool IsDefault { get; set; }
    /// <summary>该知识点是否存在自己的判定线覆盖</summary>
    public bool IsOverride { get; set; }
    /// <summary>课程级判定线（用于编辑界面展示「课程级为 x/y」）</summary>
    public double? CourseWeakLine { get; set; }
    public double? CourseOkLine { get; set; }
}

// ══════════════════════════════════════════════════════════
//  试卷图片上传 / OCR
// ══════════════════════════════════════════════════════════

public class PaperUploadItemDto
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/jpeg";
    /// <summary>图片 Base64（不含 data: 前缀），单张上限 10MB</summary>
    public string Base64 { get; set; } = string.Empty;
    /// <summary>
    /// 已上传到 OSS 的公网 URL（走 /api/oss-upload/image 上传后传这里）。
    /// 提供 Url 时后端不再接收 Base64，OCR 直接让模型按 URL 取图，显著减小请求体。
    /// </summary>
    public string? Url { get; set; }
    /// <summary>PDF 拆页页码（从 1 开始）</summary>
    public int PageIndex { get; set; } = 1;
    /// <summary>多页合并分组键（同一份试卷的多个页面填同一个值）；为空表示独立成卷</summary>
    public string? MergeGroup { get; set; }
}

public class UploadPaperImagesInput
{
    public List<PaperUploadItemDto> Files { get; set; } = new();
}

/// <summary>
/// 上传单份学生试卷：一次一份卷，必须指定该卷所属学生（课程的选课学生）。
/// </summary>
public class UploadPaperScriptInput
{
    /// <summary>关联课程</summary>
    public Guid CourseId { get; set; }

    /// <summary>课程名称（冗余，避免前端额外查询）</summary>
    public string CourseName { get; set; } = string.Empty;

    /// <summary>试卷批次名称（同一课程 + 名称 + 考试日期视为同一批次）</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>考试日期</summary>
    public DateTime ExamDate { get; set; } = DateTime.UtcNow;

    /// <summary>本份试卷所属学生（必须是该课程的选课学生）</summary>
    public Guid StudentId { get; set; }

    /// <summary>学生姓名</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>学生学号</summary>
    public string StudentNumber { get; set; } = string.Empty;

    /// <summary>
    /// 试卷文件：一张图片 = 一项；多页 PDF 拆页后每页一项，
    /// 同一份试卷的多页共用相同 <see cref="PaperUploadItemDto.MergeGroup"/>，后端会合并成一份试卷。
    /// </summary>
    public List<PaperUploadItemDto> Files { get; set; } = new();
}

/// <summary>单份试卷上传结果</summary>
public class UploadPaperScriptResultDto
{
    /// <summary>所属批次 Id</summary>
    public Guid BatchId { get; set; }

    /// <summary>批次名称</summary>
    public string BatchTitle { get; set; } = string.Empty;

    /// <summary>试卷图片 Id</summary>
    public Guid ImageId { get; set; }

    /// <summary>所属学生 Id</summary>
    public Guid StudentId { get; set; }

    /// <summary>所属学生姓名</summary>
    public string StudentName { get; set; } = string.Empty;
}

/// <summary>课程的选课学生（教师上传纸质试卷时选择）</summary>
public class CourseStudentDto
{
    public Guid StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentNumber { get; set; } = string.Empty;
    /// <summary>登录用户名（学号）</summary>
    public string UserName { get; set; } = string.Empty;
    /// <summary>选课状态：0 已选 / 1 学习中 / 2 已完成 / 3 已退课</summary>
    public int Status { get; set; }
}

public class PaperUploadImageDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int PageIndex { get; set; }
    public string? MergeGroup { get; set; }
    /// <summary>所属学生 Id（教师上传时指定）</summary>
    public Guid? StudentId { get; set; }
    /// <summary>所属学生姓名</summary>
    public string StudentName { get; set; } = string.Empty;
    /// <summary>所属学生学号</summary>
    public string StudentNumber { get; set; } = string.Empty;
    /// <summary>0 待处理 / 1 处理中 / 2 完成 / 3 失败</summary>
    public int Status { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? ScriptId { get; set; }
    /// <summary>是否 PDF（PDF 走 qwen3.5-ocr Responses API 直传）</summary>
    public bool IsPdf { get; set; }
}

// ══════════════════════════════════════════════════════════
//  AI 任务 / 模型
// ══════════════════════════════════════════════════════════

public class ExtractKnowledgePointsResultDto
{
    public Guid CourseId { get; set; }
    public int ExerciseCount { get; set; }
    public bool Queued { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ModelTrainResultDto
{
    public bool Trained { get; set; }
    public int SampleCount { get; set; }
    public int StudentCount { get; set; }
    public int KnowledgePointCount { get; set; }
    public string Estimator { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class MiningResultDto
{
    public int ProposedCount { get; set; }
    public int SamplePairs { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>学生报告推送预览</summary>
public class StudentReportPreviewDto
{
    public Guid? StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentNumber { get; set; } = string.Empty;
    /// <summary>消息标题（与站内消息一致）</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>报告摘要正文</summary>
    public string Content { get; set; } = string.Empty;
    /// <summary>薄弱点 + 建议</summary>
    public List<LossAttributionDto> Items { get; set; } = new();
    /// <summary>是否已生成并推送（false 表示当前为实时预览）</summary>
    public bool Pushed { get; set; }
    public DateTime? PushedAt { get; set; }
}

// ══════════════════════════════════════════════════════════
//  报告留档
// ══════════════════════════════════════════════════════════

public class AnalysisReportFileDto
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    /// <summary>0 班级Word / 1 学生汇总Word / 2 学生明细Excel</summary>
    public int Type { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    /// <summary>存储后端：oss / local</summary>
    public string Storage { get; set; } = "local";
    public int DownloadCount { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? LastDownloadedAt { get; set; }
    /// <summary>生成时的关键指标（平均分/达标率等）</summary>
    public string? MetricsSnapshot { get; set; }
}

// ══════════════════════════════════════════════════════════
//  推荐效果离线评估
// ══════════════════════════════════════════════════════════

public class RecommendationWeightSetDto
{
    public string Name { get; set; } = string.Empty;
    public double Content { get; set; }
    public double Collab { get; set; }
    public double Fit { get; set; }
    /// <summary>权重展示文案，如「内容 0.6 / 协同 0.2 / 难度 0.2」</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>测试集中「学生答错的题」被推荐到的比例</summary>
    public double WeakHitRate { get; set; }
    /// <summary>推荐 top-K 中学生确实答错的比例</summary>
    public double Precision { get; set; }
    /// <summary>命中数 / 可评估的错题数</summary>
    public int Hits { get; set; }
    public int WeakTotal { get; set; }
}

public class RecommendationEvaluationDto
{
    public Guid? CourseId { get; set; }
    /// <summary>可用于评估的学生数（测试集含错题）</summary>
    public int StudentCount { get; set; }
    /// <summary>实际处理过的学生数（含测试集全对、不计入分母的）</summary>
    public int EvaluatedStudentCount { get; set; }
    /// <summary>参与评估的作答记录数</summary>
    public int AnswerCount { get; set; }
    public int TopK { get; set; }
    public List<RecommendationWeightSetDto> Results { get; set; } = new();
    /// <summary>推荐权重（按 WeakHitRate 优先、Precision 次之）</summary>
    public RecommendationWeightSetDto? Best { get; set; }
    /// <summary>当前线上配置的权重</summary>
    public RecommendationWeightSetDto? Current { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>推荐三路权重（可运行时应用）</summary>
public class RecommendationWeightsDto
{
    public double Content { get; set; }
    public double Collab { get; set; }
    public double Fit { get; set; }
    public string Label { get; set; } = string.Empty;
    /// <summary>来源：setting（已应用）/ config（appsettings 默认）</summary>
    public string Source { get; set; } = "config";
}

/// <summary>OCR 能力探测（前端据此决定 PDF 直传还是拆页）</summary>
public class OcrCapabilitiesDto
{
    /// <summary>图片 OCR 是否可用（配置了 Key）</summary>
    public bool Image { get; set; }
    /// <summary>是否支持 PDF 直传（qwen3.5-ocr Responses API）</summary>
    public bool PdfDirect { get; set; }
    public string PdfModel { get; set; } = string.Empty;
    /// <summary>提示文案（不可用原因 / 使用建议）</summary>
    public string? Note { get; set; }
}
