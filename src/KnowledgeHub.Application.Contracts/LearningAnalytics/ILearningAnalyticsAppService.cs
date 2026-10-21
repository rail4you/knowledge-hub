using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KnowledgeHub.LearningAnalytics.Dtos;
using Volo.Abp.Content;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 智学分析应用服务契约。
/// 路由前缀（ABP 约定）：<c>/api/app/learning-analytics</c>。
/// </summary>
public interface ILearningAnalyticsAppService : IApplicationService
{
    // ── 学生端 ──
    Task<StudentAnalysisDashboardDto> GetStudentDashboardAsync();

    Task MarkNotificationReadAsync(Guid notificationId);

    /// <summary>学生端：老师推送给我的学习分析报告列表（新的在前）</summary>
    Task<List<StudentReportListItemDto>> GetMyReportsAsync();

    /// <summary>学生端：报告详情（薄弱点 + 学习计划）；不是自己的报告返回 null</summary>
    Task<StudentReportDetailDto?> GetMyReportAsync(Guid reportId);

    /// <summary>学生端：把报告下载成 Word（.docx）</summary>
    Task<IRemoteStreamContent> DownloadMyReportAsync(Guid reportId);

    // ── 教师端 · 纸质试卷 ──
    Task<PaperAnalysisOverviewDto> GetPaperOverviewAsync();

    Task<PaperBatchDto> CreatePaperBatchAsync(CreatePaperBatchInput input);

    Task<PaperBatchDto> GetPaperBatchAsync(Guid batchId);

    /// <summary>获取课程的选课学生（上传纸质试卷时选择归属学生）</summary>
    Task<List<CourseStudentDto>> GetCourseStudentsAsync(Guid courseId);

    /// <summary>
    /// 上传单份学生试卷：一次一份卷，必须指定归属学生（课程的选课学生）。
    /// 同一「课程 + 批次名称 + 考试日期」的试卷会归入同一批次，便于班级统计。
    /// </summary>
    Task<UploadPaperScriptResultDto> UploadPaperScriptAsync(UploadPaperScriptInput input);

    Task<List<PaperUploadImageDto>> GetPaperUploadImagesAsync(Guid batchId);

    /// <summary>探测 OCR 能力：图片 OCR 是否可用、是否支持 PDF 直传</summary>
    Task<OcrCapabilitiesDto> GetOcrCapabilitiesAsync();

    // ── 试卷生成器 ──
    /// <summary>
    /// 生成一份可 OCR 扫描的纸质试卷样张：题目从课程题库随机抽取、得分可随机，
    /// 返回 A4 排版的 HTML + 结构化题目，前端可直接预览 / 打印 / 栅格化成 PNG 上传。
    /// </summary>
    Task<PaperTemplateDto> GeneratePaperTemplateAsync(GeneratePaperTemplateInput input);

    /// <summary>试卷生成器的当前生效配置（前端据此预填表单）</summary>
    Task<PaperGeneratorConfigDto> GetPaperGeneratorConfigAsync();

    /// <summary>执行 OCR（Qwen OCR）并生成待校对试卷</summary>
    Task RunPaperOcrAsync(Guid batchId);

    Task<List<PaperScriptRowDto>> GetPaperScriptsAsync(Guid batchId);

    Task ConfirmPaperScriptAsync(Guid scriptId, ConfirmPaperScriptInput input);

    /// <summary>
    /// 删除单份试卷的分析结果：识别明细、题目得分、来源图片，以及该份试卷写入的作答记录，
    /// 删除后重算该学生的掌握度（避免已删除的数据继续留在掌握度矩阵里）。
    /// </summary>
    Task DeletePaperScriptAsync(Guid scriptId);

    /// <summary>
    /// 删除某个试卷批次的**全部分析结果**：所有试卷与题目得分、作答记录、分析报告与站内消息、
    /// 教学诊断、归档报告文件、来源图片，最后删除批次本身（再次上传同名同日期试卷会自动重建批次）。
    /// </summary>
    Task DeletePaperBatchAsync(Guid batchId);

    Task<PushResultDto> PushReportsAsync(Guid batchId);

    // ── 教师端 · 分析 ──
    Task<ClassWeaknessDto> GetClassWeaknessAsync(Guid batchId);

    Task<TeachingInsightDto> GetTeachingInsightAsync(Guid batchId, bool useAiAdvice = true);

    // ── 报告导出 / 预览 ──
    /// <summary>
    /// 导出 Word 文字报告（简化版）：type = class（班级学情 + 教学诊断，默认）| students（学生薄弱点汇总）。
    /// </summary>
    Task<IRemoteStreamContent> ExportDocxReportAsync(Guid batchId, string type = "class");

    /// <summary>导出学生薄弱点明细（Excel，供二次分析）</summary>
    Task<IRemoteStreamContent> ExportStudentReportsAsync(Guid batchId);

    /// <summary>预览推送给某个学生的报告内容（student 为空时取重点关注名单首个）</summary>
    Task<StudentReportPreviewDto> GetStudentReportPreviewAsync(Guid batchId, Guid? student = null);

    // ── 报告留档 ──
    /// <summary>生成并归档报告文件（type = class | students | excel）</summary>
    Task<AnalysisReportFileDto> ArchiveReportAsync(Guid batchId, string type = "class");

    Task<List<AnalysisReportFileDto>> GetReportFilesAsync(Guid batchId);

    /// <summary>下载归档报告（同时累加下载次数）</summary>
    Task<IRemoteStreamContent> DownloadReportFileAsync(Guid fileId);

    Task DeleteReportFileAsync(Guid fileId);

    // ── 推荐效果离线评估 ──
    /// <summary>
    /// 用真实作答做离线评估（留出每个学生最近 K 条作答作为测试集），
    /// 对若干组「内容/协同/难度」权重做对比，给出 WeakHitRate@K、Precision@K 与建议权重。
    /// </summary>
    Task<RecommendationEvaluationDto> EvaluateRecommendationAsync(Guid? courseId = null, int topK = 5);

    /// <summary>当前生效的推荐权重</summary>
    Task<RecommendationWeightsDto> GetRecommendationWeightsAsync();

    /// <summary>应用指定权重（写入 Setting，保存即生效）</summary>
    Task<RecommendationWeightsDto> ApplyRecommendationWeightsAsync(RecommendationWeightsDto input);

    /// <summary>跑一次离线评估并应用其中表现最好的权重（样本不足时报错）</summary>
    Task<RecommendationWeightsDto> ApplyBestRecommendationWeightsAsync(Guid? courseId = null, int topK = 5);

    // ── 教师端 · 课程联动 ──
    Task<CourseLinkConfigDto> GetCourseLinkAsync(Guid courseId);

    Task<PrerequisiteMappingDto> SavePrerequisiteAsync(SavePrerequisiteInput input);

    Task DeletePrerequisiteAsync(Guid prerequisiteId);

    // ── 知识点 / 判定线 ──
    /// <summary>确保课程知识点已建立（按章节 / 知识资源初始化）并返回</summary>
    Task<List<KnowledgePointDto>> EnsureCourseKnowledgePointsAsync(Guid courseId);

    Task<List<KnowledgePointDto>> GetKnowledgePointsAsync(Guid courseId);

    /// <summary>用 LLM 从习题抽取知识点（按课程批量，后台任务）</summary>
    Task<ExtractKnowledgePointsResultDto> ExtractKnowledgePointsAsync(Guid courseId, bool overwrite = false);

    /// <summary>
    /// 读取判定线：knowledgePoint 为空时返回课程级；否则返回该知识点生效值（含是否覆盖）。
    /// 参数名刻意用 knowledgePoint（不带 Id 后缀）：ABP 只有在「仅一个 Id 参数」时才把 courseId 放进路径，
    /// 否则整条路由会退化成 query string（曾导致 /threshold/{courseId} 失效）。
    /// </summary>
    Task<MasteryThresholdDto> GetThresholdAsync(Guid courseId, Guid? knowledgePoint = null);

    Task SetThresholdAsync(SetThresholdInput input);

    /// <summary>清除知识点级判定线覆盖（恢复课程级）</summary>
    Task ClearThresholdAsync(SetThresholdInput input);

    // ── 课程前后关系（CourseLink） ──
    Task<CourseLinkDto> SaveCourseLinkAsync(SaveCourseLinkInput input);

    Task DeleteCourseLinkAsync(Guid courseLinkId);

    // ── 模型 / 挖掘（AI 任务） ──
    /// <summary>重训掌握度矩阵分解模型（数据不足时返回 Trained=false）</summary>
    Task<ModelTrainResultDto> TrainMasteryModelAsync();

    /// <summary>挖掘先修知识点候选（写入 Proposed，需老师确认）</summary>
    Task<MiningResultDto> MinePrerequisitesAsync(Guid? courseId = null);

    /// <summary>立即为进入推送窗口的学生生成并推送衔接复习包（返回推送条数）</summary>
    Task<PushResultDto> PushPreCourseReviewsAsync();
}
