using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.LearningAnalytics.Dtos;
using KnowledgeHub.LearningAnalytics.Enums;
using KnowledgeHub.LearningAnalytics.Ml;
using KnowledgeHub.LearningAnalytics.Ocr;
using KnowledgeHub.LearningAnalytics.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Content;
using Volo.Abp.Uow;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 智学分析应用服务：学生薄弱点 / 纸质试卷分析 / 班级薄弱点 / 教学成果分析 / 课程联动。
/// 路由前缀：<c>/api/app/learning-analytics</c>。
/// </summary>
[IgnoreAntiforgeryToken]
[Authorize]
public class LearningAnalyticsAppService : KnowledgeHubAppService, ILearningAnalyticsAppService
{
    private const double WeakThresholdForLoss = 0.6;

    private readonly IRepository<KnowledgePoint, Guid> _kpRepository;
    private readonly IRepository<ExerciseKnowledgePoint> _mapRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<PaperBatch, Guid> _batchRepository;
    private readonly IRepository<PaperScript, Guid> _scriptRepository;
    private readonly IRepository<PaperItemScore, Guid> _itemRepository;
    private readonly IRepository<AnswerRecord, Guid> _answerRepository;
    private readonly IRepository<StudentNotification, Guid> _notificationRepository;
    private readonly IRepository<AnalysisReport, Guid> _reportRepository;
    private readonly IRepository<TeachingInsight, Guid> _teachingInsightRepository;
    private readonly IRepository<KpPrerequisite, Guid> _prerequisiteRepository;
    private readonly IRepository<CourseLink, Guid> _courseLinkRepository;
    private readonly IRepository<ReviewPackage, Guid> _packageRepository;
    private readonly IRepository<PaperUploadImage, Guid> _uploadImageRepository;
    private readonly IRepository<AnalysisReportFile, Guid> _reportFileRepository;

    private readonly KnowledgePointIndexer _indexer;
    private readonly MasteryRecalculationService _masteryService;
    private readonly ThresholdResolver _thresholdResolver;
    private readonly PaperOcrSimulator _ocrSimulator;
    private readonly RecommendationEngine _recommendation;
    private readonly AnalysisReportWriter _reportWriter;
    private readonly PaperImageStore _imageStore;
    private readonly AnalysisReportFileStore _reportFileStore;
    private readonly RecommendationEvaluator _recommendationEvaluator;
    private readonly RecommendationWeightProvider _recommendationWeights;
    private readonly KnowledgeHub.Resources.Oss.IOssCredentialProvider _ossCredentials;
    private readonly PaperBatchStatsService _batchStats;
    private readonly MasteryModelTrainer _modelTrainer;
    private readonly PrerequisiteMiner _prerequisiteMiner;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IServiceProvider _serviceProvider;
    private readonly IGuidGenerator _guidGenerator;

    public LearningAnalyticsAppService(
        IRepository<KnowledgePoint, Guid> kpRepository,
        IRepository<ExerciseKnowledgePoint> mapRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<PaperBatch, Guid> batchRepository,
        IRepository<PaperScript, Guid> scriptRepository,
        IRepository<PaperItemScore, Guid> itemRepository,
        IRepository<AnswerRecord, Guid> answerRepository,
        IRepository<StudentNotification, Guid> notificationRepository,
        IRepository<AnalysisReport, Guid> reportRepository,
        IRepository<TeachingInsight, Guid> teachingInsightRepository,
        IRepository<KpPrerequisite, Guid> prerequisiteRepository,
        IRepository<CourseLink, Guid> courseLinkRepository,
        IRepository<ReviewPackage, Guid> packageRepository,
        IRepository<PaperUploadImage, Guid> uploadImageRepository,
        IRepository<AnalysisReportFile, Guid> reportFileRepository,
        KnowledgePointIndexer indexer,
        MasteryRecalculationService masteryService,
        ThresholdResolver thresholdResolver,
        PaperOcrSimulator ocrSimulator,
        RecommendationEngine recommendation,
        AnalysisReportWriter reportWriter,
        PaperImageStore imageStore,
        AnalysisReportFileStore reportFileStore,
        RecommendationEvaluator recommendationEvaluator,
        RecommendationWeightProvider recommendationWeights,
        KnowledgeHub.Resources.Oss.IOssCredentialProvider ossCredentials,
        PaperBatchStatsService batchStats,
        MasteryModelTrainer modelTrainer,
        PrerequisiteMiner prerequisiteMiner,
        IConfiguration configuration,
        IUnitOfWorkManager unitOfWorkManager,
        IServiceProvider serviceProvider,
        IGuidGenerator guidGenerator)
    {
        _kpRepository = kpRepository;
        _mapRepository = mapRepository;
        _exerciseRepository = exerciseRepository;
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
        _studentCourseRepository = studentCourseRepository;
        _batchRepository = batchRepository;
        _scriptRepository = scriptRepository;
        _itemRepository = itemRepository;
        _answerRepository = answerRepository;
        _notificationRepository = notificationRepository;
        _reportRepository = reportRepository;
        _teachingInsightRepository = teachingInsightRepository;
        _prerequisiteRepository = prerequisiteRepository;
        _courseLinkRepository = courseLinkRepository;
        _packageRepository = packageRepository;
        _uploadImageRepository = uploadImageRepository;
        _reportFileRepository = reportFileRepository;
        _indexer = indexer;
        _masteryService = masteryService;
        _thresholdResolver = thresholdResolver;
        _ocrSimulator = ocrSimulator;
        _recommendation = recommendation;
        _reportWriter = reportWriter;
        _imageStore = imageStore;
        _reportFileStore = reportFileStore;
        _recommendationEvaluator = recommendationEvaluator;
        _recommendationWeights = recommendationWeights;
        _ossCredentials = ossCredentials;
        _batchStats = batchStats;
        _modelTrainer = modelTrainer;
        _prerequisiteMiner = prerequisiteMiner;
        _configuration = configuration;
        _unitOfWorkManager = unitOfWorkManager;
        _serviceProvider = serviceProvider;
        _guidGenerator = guidGenerator;
    }

    // ══════════════════════════════════════════════════════════
    //  学生端
    // ══════════════════════════════════════════════════════════

    public async Task<StudentAnalysisDashboardDto> GetStudentDashboardAsync()
    {
        var studentId = CurrentUser.Id ?? throw new AbpAuthorizationException("未登录");

        var dto = new StudentAnalysisDashboardDto();

        var enrollments = await _studentCourseRepository.GetListAsync(x => x.StudentId == studentId);
        if (!enrollments.Any())
        {
            return dto;
        }

        var courseIds = enrollments.Select(e => e.CourseId).Distinct().ToList();
        var courses = await _courseRepository.GetListAsync(c => courseIds.Contains(c.Id));
        var courseById = courses.ToDictionary(c => c.Id);

        var currentCourse = await PickCurrentCourseAsync(enrollments, courses);
        if (currentCourse == null)
        {
            return dto;
        }

        var snapshots = await _masteryService.ComputeAsync(studentId, currentCourse.Id);
        var thresholds = await _thresholdResolver.ResolveAllAsync(currentCourse.Id);

        dto.Overview = await BuildStudentOverviewAsync(
            studentId, currentCourse, snapshots, thresholds, enrollments.FirstOrDefault(e => e.CourseId == currentCourse.Id));

        var path = await BuildCoursePathAsync(studentId, currentCourse, enrollments);
        dto.Path = path.Nodes;
        dto.ReviewProgress = path.ReviewProgress;

        var notifications = await _notificationRepository.GetListAsync(x => x.StudentId == studentId);
        dto.Notifications = notifications
            .OrderByDescending(n => n.CreationTime)
            .Take(30)
            .Select(n => new AnalysisNotificationDto
            {
                Id = n.Id,
                Type = n.Type.ToString(),
                Title = n.Title,
                Content = n.Content,
                Source = n.Source,
                CreatedAt = n.CreationTime,
                IsRead = n.IsRead,
            })
            .ToList();
        dto.UnreadCount = notifications.Count(n => !n.IsRead);

        dto.Practices = await _recommendation.RecommendPracticeAsync(studentId, currentCourse.Id, snapshots);

        // 前置课程衔接练习（来自复习包）
        if (path.ReviewProgress != null)
        {
            var prerequisitePractices = path.ReviewProgress.Items
                .Where(i => !i.IsCompleted)
                .Take(2)
                .Select(i => new PracticeItemDto
                {
                    Id = $"review-{i.KnowledgePointId:N}",
                    Title = $"{i.Name}衔接练习",
                    CourseId = i.CourseId,
                    CourseTitle = i.CourseTitle,
                    ExerciseCount = 3,
                    KnowledgePointId = i.KnowledgePointId,
                    FromPrerequisite = true,
                    Reason = $"{path.ReviewProgress.UpcomingCourseTitle}开课前衔接复习",
                })
                .ToList();

            dto.Practices.AddRange(prerequisitePractices);
        }

        return dto;
    }

    public async Task MarkNotificationReadAsync(Guid notificationId)
    {
        var studentId = CurrentUser.Id ?? throw new AbpAuthorizationException("未登录");
        var notification = await _notificationRepository.FindAsync(notificationId);
        if (notification == null || notification.StudentId != studentId)
        {
            return;
        }

        notification.MarkRead();
        await _notificationRepository.UpdateAsync(notification, autoSave: true);
    }

    private async Task<Course?> PickCurrentCourseAsync(List<StudentCourse> enrollments, List<Course> courses)
    {
        if (!courses.Any())
        {
            return null;
        }

        // 优先：进行中的课程；其次：最近有学习记录的课程；最后：第一个
        var inProgress = enrollments
            .Where(e => e.Status == StudentCourseStatus.InProgress)
            .OrderByDescending(e => e.LastModificationTime ?? e.CreationTime)
            .FirstOrDefault();
        var chosenId = inProgress?.CourseId;

        if (chosenId == null)
        {
            var latest = enrollments
                .OrderByDescending(e => e.LastModificationTime ?? e.CreationTime)
                .FirstOrDefault();
            chosenId = latest?.CourseId;
        }

        return courses.FirstOrDefault(c => c.Id == chosenId) ?? courses.First();
    }

    private async Task<StudentWeaknessOverviewDto> BuildStudentOverviewAsync(
        Guid studentId,
        Course course,
        List<KpMasterySnapshot> snapshots,
        CourseThresholds thresholds,
        StudentCourse? enrollment)
    {
        // 只展示有作答证据的知识点：没做过的知识点不应显示为 0%（会误导学生）
        var items = snapshots
            .Where(s => s.Evidence > 0)
            .Select(s => ToMasteryItem(s, thresholds))
            .ToList();

        var overview = new StudentWeaknessOverviewDto
        {
            StudentName = CurrentUser.Name ?? CurrentUser.UserName ?? string.Empty,
            CourseId = course.Id,
            CourseName = course.Title,
            Confirmed = false,
            SourceLabel = "来自在线练习",
            WeakCount = items.Count(i => i.Level == (int)MasteryGrade.Weak && i.Evidence > 0),
            KnowledgePoints = items,
        };

        // 最近的纸质试卷（偏好已确认批次）
        var batches = await _batchRepository.GetListAsync(b => b.CourseId == course.Id);
        var batchIds = batches.Select(b => b.Id).ToList();
        var myScripts = batchIds.Any()
            ? await _scriptRepository.GetListAsync(s => batchIds.Contains(s.BatchId) && s.StudentId == studentId)
            : new List<PaperScript>();

        var script = myScripts.OrderByDescending(s => s.Confirmed).FirstOrDefault();
        if (script != null)
        {
            var batch = batches.FirstOrDefault(b => b.Id == script.BatchId);
            if (batch != null)
            {
                var batchScripts = await _scriptRepository.GetListAsync(s => s.BatchId == batch.Id);
                overview.PaperBatchId = batch.Id;
                overview.PaperTitle = batch.Title;
                overview.Score = Math.Round(script.TotalScore, 1);
                overview.ClassAverage = Math.Round(batch.AverageScore, 1);
                overview.ClassSize = batch.ScriptCount;
                overview.ClassRank = batchScripts.Count(s => s.TotalScore > script.TotalScore) + 1;
                overview.Confirmed = script.Confirmed;
                overview.SourceLabel = script.Confirmed
                    ? $"来自纸质试卷「{batch.Title}」· 老师已确认"
                    : $"来自纸质试卷「{batch.Title}」· 待老师确认";

                overview.Losses = await BuildLossesAsync(script);
            }
        }
        else
        {
            overview.PaperTitle = enrollment != null ? "课程练习记录" : string.Empty;
        }

        return overview;
    }

    private async Task<List<LossAttributionDto>> BuildLossesAsync(PaperScript script)
    {
        var items = await _itemRepository.GetListAsync(x => x.ScriptId == script.Id);
        var weakItems = items
            .Where(i => i.FullScore > 0 && i.Ratio < WeakThresholdForLoss)
            .OrderByDescending(i => i.FullScore * (1 - i.Ratio))
            .Take(5)
            .ToList();

        if (!weakItems.Any())
        {
            return new List<LossAttributionDto>();
        }

        var exerciseIds = weakItems.Where(i => i.ExerciseId.HasValue).Select(i => i.ExerciseId!.Value).Distinct().ToList();
        var exercises = exerciseIds.Any()
            ? (await _exerciseRepository.GetListAsync(e => exerciseIds.Contains(e.Id))).ToDictionary(e => e.Id)
            : new Dictionary<Guid, Exercise>();
        var maps = exerciseIds.Any()
            ? await _mapRepository.GetListAsync(m => exerciseIds.Contains(m.ExerciseId))
            : new List<ExerciseKnowledgePoint>();
        var kpIds = maps.Select(m => m.KnowledgePointId).Distinct().ToList();
        var kps = kpIds.Any()
            ? (await _kpRepository.GetListAsync(k => kpIds.Contains(k.Id))).ToDictionary(k => k.Id)
            : new Dictionary<Guid, KnowledgePoint>();

        var result = new List<LossAttributionDto>();
        foreach (var item in weakItems)
        {
            exercises.TryGetValue(item.ExerciseId ?? Guid.Empty, out var exercise);
            var itemKpNames = maps.Where(m => m.ExerciseId == item.ExerciseId)
                .Select(m => kps.TryGetValue(m.KnowledgePointId, out var kp) ? kp.Name : null)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList();

            var kpName = itemKpNames.FirstOrDefault() ?? "课程综合";
            var ratio = item.Ratio;

            result.Add(new LossAttributionDto
            {
                QuestionNo = item.QuestionNo,
                ExerciseId = item.ExerciseId,
                ExerciseTitle = exercise?.Title ?? $"第 {item.QuestionNo} 题",
                KnowledgePointName = kpName,
                Mastery = Math.Round(ratio, 3),
                Reason = ratio <= 0
                    ? "未得分，基础概念可能未掌握"
                    : ratio < 0.4
                        ? "解题方法选择错误"
                        : "步骤或计算失分",
                Advice = $"回看「{kpName}」相关讲解，并完成 3 道同类练习",
            });
        }

        return result;
    }

    private record CoursePathResult(List<CoursePathNodeDto> Nodes, ReviewPackageProgressDto? ReviewProgress);

    private async Task<CoursePathResult> BuildCoursePathAsync(
        Guid studentId,
        Course currentCourse,
        List<StudentCourse> enrollments)
    {
        var links = await _courseLinkRepository.GetListAsync(x => x.Status == PrerequisiteStatus.Confirmed);
        var ordered = new List<Guid>();
        var visited = new HashSet<Guid>();

        void WalkBack(Guid courseId, int depth)
        {
            if (depth <= 0) return;
            var predecessor = links.FirstOrDefault(l => l.SuccessorCourseId == courseId);
            if (predecessor == null || visited.Contains(predecessor.PredecessorCourseId)) return;
            visited.Add(predecessor.PredecessorCourseId);
            WalkBack(predecessor.PredecessorCourseId, depth - 1);
            ordered.Add(predecessor.PredecessorCourseId);
        }

        visited.Add(currentCourse.Id);
        WalkBack(currentCourse.Id, 3);
        ordered.Add(currentCourse.Id);

        var successor = links.FirstOrDefault(l => l.PredecessorCourseId == currentCourse.Id);
        while (successor != null && !visited.Contains(successor.SuccessorCourseId))
        {
            visited.Add(successor.SuccessorCourseId);
            ordered.Add(successor.SuccessorCourseId);
            successor = links.FirstOrDefault(l => l.PredecessorCourseId == successor.SuccessorCourseId);
        }

        var allCourses = await _courseRepository.GetListAsync(c => ordered.Contains(c.Id));
        var courseById = allCourses.ToDictionary(c => c.Id);
        var enrollmentByCourse = enrollments.GroupBy(e => e.CourseId).ToDictionary(g => g.Key, g => g.First());

        ReviewPackageProgressDto? reviewProgress = null;
        var immediateSuccessorId = links
            .Where(l => l.PredecessorCourseId == currentCourse.Id)
            .Select(l => l.SuccessorCourseId)
            .FirstOrDefault();

        if (immediateSuccessorId != Guid.Empty)
        {
            reviewProgress = await GetOrBuildReviewPackageAsync(studentId, immediateSuccessorId);
        }

        var nodes = new List<CoursePathNodeDto>();
        for (var i = 0; i < ordered.Count; i++)
        {
            var courseId = ordered[i];
            if (!courseById.TryGetValue(courseId, out var course))
            {
                continue;
            }

            var enrollment = enrollmentByCourse.GetValueOrDefault(courseId);
            var isCurrent = courseId == currentCourse.Id;
            var isImmediateSuccessor = courseId == immediateSuccessorId;

            var node = new CoursePathNodeDto
            {
                CourseId = courseId,
                CourseTitle = course.Title,
            };

            if (isCurrent)
            {
                var completed = enrollment?.Status == StudentCourseStatus.Completed;
                node.State = completed ? 0 : 1;
                node.Tag = completed ? "已完成" : "学习中";
                node.Note = completed ? "薄弱点已生成，可查看复习建议" : "继续完成课程练习以更新掌握度";
            }
            else if (ordered.IndexOf(courseId) < ordered.IndexOf(currentCourse.Id))
            {
                node.State = 0;
                node.Tag = "已完成";
            }
            else if (isImmediateSuccessor)
            {
                node.State = 1;
                node.Tag = "即将开课";
                if (reviewProgress != null)
                {
                    node.PrerequisiteNames = reviewProgress.Items.Select(x => x.Name).Take(4).ToList();
                    node.Note = $"开课前建议先补：{string.Join("、", node.PrerequisiteNames)}";
                }
                else
                {
                    node.Note = "尚未配置前置知识点映射，可联系老师补充";
                }
            }
            else
            {
                node.State = 2;
                node.Tag = "待学习";
            }

            nodes.Add(node);
        }

        return new CoursePathResult(nodes, reviewProgress);
    }

    private async Task<ReviewPackageProgressDto?> GetOrBuildReviewPackageAsync(Guid studentId, Guid upcomingCourseId)
    {
        var built = await _recommendation.BuildReviewPackageAsync(studentId, upcomingCourseId, PathStrategy.Mixed, persist: true);
        return built;
    }

    // ══════════════════════════════════════════════════════════
    //  教师端 · 纸质试卷
    // ══════════════════════════════════════════════════════════

    public async Task<PaperAnalysisOverviewDto> GetPaperOverviewAsync()
    {
        var batches = await _batchRepository.GetListAsync();
        var ordered = batches.OrderByDescending(b => b.CreationTime).ToList();
        var images = await _uploadImageRepository.GetListAsync();
        var imagesByBatch = images.GroupBy(i => i.BatchId).ToDictionary(g => g.Key, g => g.ToList());

        return new PaperAnalysisOverviewDto
        {
            BatchCount = ordered.Count,
            ScriptCount = ordered.Sum(b => b.ScriptCount),
            AverageScore = ordered.Any() ? Math.Round(ordered.Average(b => b.AverageScore), 1) : 0,
            PendingConfirmCount = ordered.Sum(b => b.NeedsConfirmCount),
            PushedCount = ordered.Sum(b => b.PushedCount),
            Batches = ordered.Select(b => ToBatchDto(b, imagesByBatch.GetValueOrDefault(b.Id))).ToList(),
        };
    }

    public async Task<PaperBatchDto> GetPaperBatchAsync(Guid batchId)
    {
        var batch = await _batchRepository.GetAsync(batchId);
        var images = await _uploadImageRepository.GetListAsync(i => i.BatchId == batchId);
        return ToBatchDto(batch, images);
    }

    public async Task<List<PaperUploadImageDto>> GetPaperUploadImagesAsync(Guid batchId)
    {
        var images = await _uploadImageRepository.GetListAsync(i => i.BatchId == batchId);
        return images
            .OrderBy(i => i.CreationTime)
            .Select(i => new PaperUploadImageDto
            {
                Id = i.Id,
                FileName = i.FileName,
                SizeBytes = i.SizeBytes,
                PageIndex = i.PageIndex,
                MergeGroup = i.MergeGroup,
                Status = (int)i.Status,
                ErrorMessage = i.ErrorMessage,
                ScriptId = i.ScriptId,
                IsMockOcr = string.Equals(i.OcrProvider, "mock", StringComparison.OrdinalIgnoreCase),
                IsPdf = string.Equals(i.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase),
            })
            .ToList();
    }

    public Task<OcrCapabilitiesDto> GetOcrCapabilitiesAsync()
    {
        var pdfEnabled = _configuration.GetValue("LearningAnalytics:PdfDirectEnabled", true);
        var imageEnabled = _configuration.GetValue("LearningAnalytics:OcrProvider", "qwen") != "mock";

        return Task.FromResult(new OcrCapabilitiesDto
        {
            Image = imageEnabled,
            PdfDirect = pdfEnabled,
            PdfModel = _configuration["LearningAnalytics:PdfOcrModel"] ?? "qwen3.5-ocr",
            Note = pdfEnabled
                ? "PDF 将整份上传 OSS 后由 qwen3.5-ocr 直接解析（最多 50 页）"
                : "PDF 将由浏览器拆页为图片后识别",
        });
    }

    public async Task UploadPaperImagesAsync(Guid batchId, UploadPaperImagesInput input)
    {
        var batch = await _batchRepository.GetAsync(batchId);
        if (input?.Files == null || !input.Files.Any())
        {
            throw new UserFriendlyException("请至少上传一张试卷图片");
        }

        var maxBytes = _configuration.GetValue("LearningAnalytics:MaxImageBytes", 10 * 1024 * 1024);
        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/bmp" };
        // PDF 只接受 OSS URL（走 qwen3.5-ocr Responses API 直传，不需要拆页/落盘）
        var pdfContentType = "application/pdf";

        foreach (var file in input.Files)
        {
            var contentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? GuessContentType(file.FileName)
                : file.ContentType.Split(';')[0].Trim().ToLowerInvariant();

            var isPdf = contentType == pdfContentType;
            if (!isPdf && !allowed.Contains(contentType))
            {
                throw new UserFriendlyException($"不支持的文件类型：{file.FileName}（仅支持 JPG / PNG / WEBP / BMP，PDF 需走 OSS）");
            }

            if (isPdf && string.IsNullOrWhiteSpace(file.Url))
            {
                throw new UserFriendlyException("PDF 试卷请通过 OSS 直传（页面会自动上传并只传 URL）");
            }

            // ① 走 OSS：前端已用 /api/oss-upload/image 上传并拿到公网 URL，
            //    这里只校验 URL 归属（必须是本租户配置的 OSS 域名），不落本地磁盘，
            //    OCR 时把 URL 直接交给模型按需拉取。
            if (!string.IsNullOrWhiteSpace(file.Url))
            {
                var validatedUrl = await ValidateOssImageUrlAsync(file.Url!);
                var urlImage = new PaperUploadImage(
                    _guidGenerator.Create(), batchId, file.FileName, $"url:{validatedUrl}", 0)
                {
                    ContentType = contentType,
                    PageIndex = file.PageIndex <= 0 ? 1 : file.PageIndex,
                    MergeGroup = string.IsNullOrWhiteSpace(file.MergeGroup) ? null : file.MergeGroup.Trim(),
                };
                await _uploadImageRepository.InsertAsync(urlImage, autoSave: true);
                continue;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(StripDataUrl(file.Base64));
            }
            catch (FormatException)
            {
                throw new UserFriendlyException($"图片内容不是合法的 Base64：{file.FileName}");
            }

            if (bytes.Length == 0)
            {
                throw new UserFriendlyException($"图片为空：{file.FileName}");
            }

            if (bytes.Length > maxBytes)
            {
                throw new UserFriendlyException($"图片过大：{file.FileName}（上限 {maxBytes / 1024 / 1024}MB）");
            }

            var storedPath = await _imageStore.SaveAsync(batchId, file.FileName, bytes);
            var image = new PaperUploadImage(_guidGenerator.Create(), batchId, file.FileName, storedPath, bytes.Length)
            {
                ContentType = contentType,
                PageIndex = file.PageIndex <= 0 ? 1 : file.PageIndex,
                MergeGroup = string.IsNullOrWhiteSpace(file.MergeGroup) ? null : file.MergeGroup.Trim(),
            };
            await _uploadImageRepository.InsertAsync(image, autoSave: true);
        }

        batch.Status = PaperBatchStatus.Draft;
        await _batchRepository.UpdateAsync(batch, autoSave: true);
        await _batchStats.RefreshAsync(batchId);
    }

    /// <summary>
    /// 执行 OCR。有两种模式：
    /// 1) 已上传图片 → 走 OCR（Qwen-VL-OCR；未配置 Key 自动回退 Mock）+ 习题匹配；
    /// 2) 没有图片 → 使用演示模拟器按班级学生生成（仅用于无真实图片的快速体验）。
    /// </summary>
    public async Task RunPaperOcrAsync(Guid batchId)
    {
        var batch = await _batchRepository.GetAsync(batchId);
        var images = await _uploadImageRepository.GetListAsync(i => i.BatchId == batchId);

        await _indexer.EnsureAsync(batch.CourseId);

        if (!images.Any())
        {
            // 演示模式：无图片时按班级学生生成（结果标记 mock）
            batch.Status = PaperBatchStatus.OcrRunning;
            await _batchRepository.UpdateAsync(batch, autoSave: true);
            await _ocrSimulator.GenerateAsync(batch);
            return;
        }

        // 重跑时把失败/已完成的图片重置为待处理（幂等）
        foreach (var image in images)
        {
            if (image.Status == PaperUploadStatus.Failed)
            {
                image.Status = PaperUploadStatus.Pending;
                image.ErrorMessage = null;
                await _uploadImageRepository.UpdateAsync(image, autoSave: true);
            }
        }

        batch.Status = PaperBatchStatus.OcrRunning;
        await _batchRepository.UpdateAsync(batch, autoSave: true);

        await RunAfterCommitAsync(async () =>
        {
            var queue = _serviceProvider.GetService(typeof(ILearningAnalyticsJobQueue)) as ILearningAnalyticsJobQueue;
            if (queue != null)
            {
                queue.EnqueuePaperOcr(batchId, CurrentTenant.Id);
                return;
            }

            // 未注册队列（如单元测试）→ 同步执行，保证功能可用
            var job = (PaperOcrProcessingJob)_serviceProvider.GetService(typeof(PaperOcrProcessingJob))!;
            await job.ExecuteAsync(batchId, CurrentTenant.Id);
        });
    }

    public async Task<ExtractKnowledgePointsResultDto> ExtractKnowledgePointsAsync(Guid courseId, bool overwrite = false)
    {
        var course = await _courseRepository.FindAsync(courseId);
        if (course == null)
        {
            throw new UserFriendlyException("课程不存在");
        }

        var exerciseCount = (await _exerciseRepository.GetListAsync(e => e.CourseId == courseId)).Count;
        if (exerciseCount == 0)
        {
            throw new UserFriendlyException("该课程还没有习题，先创建习题后再抽取知识点");
        }

        var result = new ExtractKnowledgePointsResultDto
        {
            CourseId = courseId,
            ExerciseCount = exerciseCount,
            Queued = true,
            Message = $"已提交 {exerciseCount} 道习题的知识点抽取任务，稍后刷新查看结果",
        };

        await RunAfterCommitAsync(async () =>
        {
            var queue = _serviceProvider.GetService(typeof(ILearningAnalyticsJobQueue)) as ILearningAnalyticsJobQueue;
            if (queue != null)
            {
                queue.EnqueueKnowledgePointExtraction(courseId, overwrite, CurrentTenant.Id);
                return;
            }

            var job = (KpExtractionJob)_serviceProvider.GetService(typeof(KpExtractionJob))!;
            await job.ExecuteAsync(courseId, overwrite, CurrentTenant.Id);
        });

        return result;
    }

    public async Task<ModelTrainResultDto> TrainMasteryModelAsync()
    {
        return await _modelTrainer.TrainAsync();
    }

    public async Task<MiningResultDto> MinePrerequisitesAsync(Guid? courseId = null)
    {
        return await _prerequisiteMiner.MineAsync(courseId);
    }

    // ══════════════════════════════════════════════════════════
    //  报告导出 / 预览
    // ══════════════════════════════════════════════════════════

    public async Task<IRemoteStreamContent> ExportDocxReportAsync(Guid batchId, string type = "class")
    {
        var (batch, course) = await LoadBatchContextAsync(batchId);
        var courseName = course?.Title ?? batch.CourseName;

        // 导出不调用模型：优先用已持久化的教学建议，缺失的章节用规则文案补齐
        var insight = await GetTeachingInsightAsync(batchId, useAiAdvice: false);

        byte[] bytes;
        string suffix;

        if (string.Equals(type, "students", StringComparison.OrdinalIgnoreCase))
        {
            var studentData = await BuildStudentReportDataAsync(batchId, courseName);
            bytes = LearningAnalyticsDocxReportBuilder.BuildStudentSummaryReport(studentData);
            suffix = "学生薄弱点汇总";
        }
        else
        {
            var weakness = await GetClassWeaknessAsync(batchId);
            var students = await BuildStudentScoreRowsAsync(batchId, weakness);

            var data = new ClassReportData(
                courseName,
                batch.Title,
                batch.ExamDate,
                batch.AverageScore,
                batch.ScriptCount,
                batch.PushedCount,
                batch.NeedsConfirmCount > 0 ? $"待校对 {batch.NeedsConfirmCount} 份" : "已全部校对",
                weakness.TargetLine,
                students,
                weakness.KnowledgePoints,
                weakness.FocusStudents,
                insight.Chapters,
                insight.Diagnoses);

            bytes = LearningAnalyticsDocxReportBuilder.BuildClassReport(data);
            suffix = "班级学情报告";
        }

        var fileName = $"{SanitizeFileName(courseName)}_{SanitizeFileName(batch.Title)}_{suffix}_{DateTime.Now:yyyyMMdd}.docx";
        return new RemoteStreamContent(new MemoryStream(bytes), fileName, DocxContentType);
    }

    public async Task<IRemoteStreamContent> ExportStudentReportsAsync(Guid batchId)
    {
        var (batch, course) = await LoadBatchContextAsync(batchId);
        var courseName = course?.Title ?? batch.CourseName;
        var data = await BuildStudentReportDataAsync(batchId, courseName);

        var stream = LearningAnalyticsExcelReportBuilder.BuildStudentReport(data);
        var fileName = $"{SanitizeFileName(courseName)}_{SanitizeFileName(batch.Title)}_学生薄弱点明细_{DateTime.Now:yyyyMMdd}.xlsx";
        return new RemoteStreamContent(stream, fileName, ExcelContentType);
    }

    /// <summary>学生薄弱点汇总数据（Word 汇总 / Excel 明细共用）。</summary>
    private async Task<StudentReportData> BuildStudentReportDataAsync(Guid batchId, string courseName)
    {
        var batch = await _batchRepository.GetAsync(batchId);
        var weakness = await GetClassWeaknessAsync(batchId);
        var scripts = (await _scriptRepository.GetListAsync(s => s.BatchId == batchId))
            .Where(s => s.StudentId.HasValue)
            .OrderBy(s => s.TotalScore)
            .ToList();

        var thresholds = await _thresholdResolver.ResolveAllAsync(batch.CourseId);
        var reports = await _reportRepository.GetListAsync(r => r.PaperBatchId == batchId && r.StudentId != null);
        var reportByStudent = reports
            .Where(r => r.StudentId.HasValue)
            .GroupBy(r => r.StudentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.GeneratedAt).First());

        var summaries = new List<StudentReportRow>();
        var weakRows = new List<StudentWeakPointRow>();

        foreach (var script in scripts)
        {
            var snapshots = await _masteryService.ComputeAsync(script.StudentId!.Value, batch.CourseId);
            var weak = snapshots
                .Where(s => s.Evidence > 0 && thresholds.Judge(s.KnowledgePointId, s.Mastery) != MasteryGrade.Ok)
                .OrderBy(s => s.Mastery)
                .ToList();

            reportByStudent.TryGetValue(script.StudentId.Value, out var report);
            var summary = report?.Summary;
            if (string.IsNullOrWhiteSpace(summary))
            {
                summary = weak.Any()
                    ? $"{script.StudentName} 本次得分 {script.TotalScore:0.#}（班级平均 {batch.AverageScore:0.#}），" +
                      $"有 {weak.Count} 个知识点待加强，建议优先复习「{string.Join("」「", weak.Take(3).Select(w => w.Name))}」。"
                    : $"{script.StudentName} 本次得分 {script.TotalScore:0.#}，各知识点均已达标，继续保持。";
            }

            var adviceByName = ExtractAdvice(report?.ContentJson);
            summaries.Add(new StudentReportRow(
                script.StudentNumber,
                script.StudentName,
                Math.Round(script.TotalScore, 1),
                weak.Count,
                summary));

            foreach (var item in weak)
            {
                var effective = thresholds.For(item.KnowledgePointId);
                var level = effective.Judge(item.Mastery);
                var advice = adviceByName.GetValueOrDefault(item.Name)
                             ?? (item.Evidence <= 0
                                 ? $"完成「{item.Name}」专项练习以建立作答数据"
                                 : $"回看「{item.ChapterTitle ?? item.Name}」相关讲解，并完成 3~5 道同类练习");

                weakRows.Add(new StudentWeakPointRow(
                    script.StudentNumber,
                    script.StudentName,
                    item.Name,
                    Math.Round(item.Mastery, 4),
                    level switch
                    {
                        MasteryGrade.Weak => "薄弱",
                        MasteryGrade.Fair => "待巩固",
                        _ => "达标",
                    },
                    advice));
            }
        }

        return new StudentReportData(courseName, batch.Title, summaries, weakRows);
    }

    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    // ══════════════════════════════════════════════════════════
    //  报告留档
    // ══════════════════════════════════════════════════════════

    public async Task<AnalysisReportFileDto> ArchiveReportAsync(Guid batchId, string type = "class")
    {
        var (batch, course) = await LoadBatchContextAsync(batchId);
        var courseName = course?.Title ?? batch.CourseName;

        var normalized = (type ?? "class").Trim().ToLowerInvariant();
        AnalysisReportFileType analysisType;
        byte[] content;
        string fileName;
        string contentType;

        switch (normalized)
        {
            case "students":
            case "students-docx":
            {
                var data = await BuildStudentReportDataAsync(batchId, courseName);
                content = LearningAnalyticsDocxReportBuilder.BuildStudentSummaryReport(data);
                fileName = $"{SanitizeFileName(courseName)}_{SanitizeFileName(batch.Title)}_学生薄弱点汇总_{DateTime.Now:yyyyMMdd}.docx";
                contentType = DocxContentType;
                analysisType = AnalysisReportFileType.StudentsDocx;
                break;
            }
            case "excel":
            case "students-excel":
            {
                var data = await BuildStudentReportDataAsync(batchId, courseName);
                using var stream = LearningAnalyticsExcelReportBuilder.BuildStudentReport(data);
                content = stream.ToArray();
                fileName = $"{SanitizeFileName(courseName)}_{SanitizeFileName(batch.Title)}_学生薄弱点明细_{DateTime.Now:yyyyMMdd}.xlsx";
                contentType = ExcelContentType;
                analysisType = AnalysisReportFileType.StudentsExcel;
                break;
            }
            default:
            {
                var weakness = await GetClassWeaknessAsync(batchId);
                var insight = await GetTeachingInsightAsync(batchId, useAiAdvice: false);
                var students = await BuildStudentScoreRowsAsync(batchId, weakness);
                var data = new ClassReportData(
                    courseName,
                    batch.Title,
                    batch.ExamDate,
                    batch.AverageScore,
                    batch.ScriptCount,
                    batch.PushedCount,
                    batch.NeedsConfirmCount > 0 ? $"待校对 {batch.NeedsConfirmCount} 份" : "已全部校对",
                    weakness.TargetLine,
                    students,
                    weakness.KnowledgePoints,
                    weakness.FocusStudents,
                    insight.Chapters,
                    insight.Diagnoses);
                content = LearningAnalyticsDocxReportBuilder.BuildClassReport(data);
                fileName = $"{SanitizeFileName(courseName)}_{SanitizeFileName(batch.Title)}_班级学情报告_{DateTime.Now:yyyyMMdd}.docx";
                contentType = DocxContentType;
                analysisType = AnalysisReportFileType.ClassDocx;
                break;
            }
        }

        var storedPath = await _reportFileStore.SaveAsync(batchId, fileName, contentType, content);

        var entity = new AnalysisReportFile(_guidGenerator.Create(), batchId, batch.CourseId, analysisType)
        {
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = content.Length,
            StoredPath = storedPath,
            MetricsSnapshot = BuildMetricsSnapshot(batch),
        };
        await _reportFileRepository.InsertAsync(entity, autoSave: true);

        return ToReportFileDto(entity);
    }

    public async Task<List<AnalysisReportFileDto>> GetReportFilesAsync(Guid batchId)
    {
        var files = await _reportFileRepository.GetListAsync(f => f.BatchId == batchId);
        return files
            .OrderByDescending(f => f.CreationTime)
            .Select(ToReportFileDto)
            .ToList();
    }

    public async Task<IRemoteStreamContent> DownloadReportFileAsync(Guid fileId)
    {
        var file = await _reportFileRepository.GetAsync(fileId);
        var bytes = await _reportFileStore.ReadAsync(file.StoredPath);

        file.MarkDownloaded();
        await _reportFileRepository.UpdateAsync(file, autoSave: true);

        return new RemoteStreamContent(new MemoryStream(bytes), file.FileName, file.ContentType);
    }

    public async Task DeleteReportFileAsync(Guid fileId)
    {
        var file = await _reportFileRepository.FindAsync(fileId);
        if (file == null)
        {
            return;
        }

        _reportFileStore.DeleteQuietly(file.StoredPath);
        await _reportFileRepository.DeleteAsync(file);
    }

    private static string BuildMetricsSnapshot(PaperBatch batch)
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            averageScore = batch.AverageScore,
            scriptCount = batch.ScriptCount,
            pushedCount = batch.PushedCount,
            needsConfirmCount = batch.NeedsConfirmCount,
            examDate = batch.ExamDate.ToString("yyyy-MM-dd"),
        });
    }

    private static AnalysisReportFileDto ToReportFileDto(AnalysisReportFile file) => new()
    {
        Id = file.Id,
        BatchId = file.BatchId,
        Type = (int)file.Type,
        TypeLabel = file.Type switch
        {
            AnalysisReportFileType.StudentsDocx => "学生汇总（Word）",
            AnalysisReportFileType.StudentsExcel => "学生明细（Excel）",
            _ => "班级报告（Word）",
        },
        FileName = file.FileName,
        ContentType = file.ContentType,
        SizeBytes = file.SizeBytes,
        Storage = AnalysisReportFileStore.ResolveStorage(file.StoredPath),
        DownloadCount = file.DownloadCount,
        CreationTime = file.CreationTime,
        LastDownloadedAt = file.LastDownloadedAt,
        MetricsSnapshot = file.MetricsSnapshot,
    };

    // ══════════════════════════════════════════════════════════
    //  推荐效果离线评估
    // ══════════════════════════════════════════════════════════

    public async Task<RecommendationEvaluationDto> EvaluateRecommendationAsync(Guid? courseId = null, int topK = 5)
    {
        return await _recommendationEvaluator.EvaluateAsync(courseId, topK);
    }

    public async Task<RecommendationWeightsDto> GetRecommendationWeightsAsync()
    {
        var weights = await _recommendationWeights.GetAsync();
        var fromConfig = RecommendationWeights.FromConfiguration(_configuration);
        var isSetting = Math.Abs(weights.Content - fromConfig.Content) > 0.0001 ||
                        Math.Abs(weights.Collab - fromConfig.Collab) > 0.0001 ||
                        Math.Abs(weights.Fit - fromConfig.Fit) > 0.0001;

        return ToWeightsDto(weights, isSetting ? "setting" : "config");
    }

    public async Task<RecommendationWeightsDto> ApplyRecommendationWeightsAsync(RecommendationWeightsDto input)
    {
        if (input == null || input.Content + input.Collab + input.Fit <= 0)
        {
            throw new UserFriendlyException("权重之和必须大于 0");
        }

        var applied = await _recommendationWeights.SetAsync(
            new RecommendationWeights(input.Content, input.Collab, input.Fit));
        return ToWeightsDto(applied, "setting");
    }

    public async Task<RecommendationWeightsDto> ApplyBestRecommendationWeightsAsync(Guid? courseId = null, int topK = 5)
    {
        var evaluation = await _recommendationEvaluator.EvaluateAsync(courseId, topK);
        var best = evaluation.Best;
        if (best == null || best.WeakTotal == 0)
        {
            throw new UserFriendlyException("样本不足，无法给出建议权重（需要学生有足够作答且最近几条含错题）");
        }

        var applied = await _recommendationWeights.SetAsync(
            new RecommendationWeights(best.Content, best.Collab, best.Fit));
        return ToWeightsDto(applied, "setting");
    }

    private static RecommendationWeightsDto ToWeightsDto(RecommendationWeights weights, string source) => new()
    {
        Content = weights.Content,
        Collab = weights.Collab,
        Fit = weights.Fit,
        Label = weights.Label,
        Source = source,
    };

    public async Task<StudentReportPreviewDto> GetStudentReportPreviewAsync(Guid batchId, Guid? student = null)
    {
        var batch = await _batchRepository.GetAsync(batchId);
        var scripts = (await _scriptRepository.GetListAsync(s => s.BatchId == batchId))
            .Where(s => s.StudentId.HasValue)
            .ToList();

        if (!scripts.Any())
        {
            throw new UserFriendlyException("该批次还没有匹配到学生的试卷");
        }

        var script = student.HasValue
            ? scripts.FirstOrDefault(s => s.StudentId == student.Value)
              ?? throw new UserFriendlyException("该学生不在本批次中")
            : scripts.OrderBy(s => s.TotalScore).First();

        var thresholds = await _thresholdResolver.ResolveAllAsync(batch.CourseId);
        var snapshots = await _masteryService.ComputeAsync(script.StudentId!.Value, batch.CourseId);
        var weak = snapshots
            .Where(s => s.Evidence > 0 && thresholds.Judge(s.KnowledgePointId, s.Mastery) != MasteryGrade.Ok)
            .OrderBy(s => s.Mastery)
            .Take(6)
            .ToList();

        var reports = await _reportRepository.GetListAsync(r =>
            r.PaperBatchId == batchId && r.StudentId == script.StudentId);
        var report = reports.OrderByDescending(r => r.GeneratedAt).FirstOrDefault();

        var adviceByName = ExtractAdvice(report?.ContentJson);
        var content = report?.Summary;
        if (string.IsNullOrWhiteSpace(content))
        {
            content = weak.Any()
                ? $"你在本次考试中得分 {script.TotalScore:0.#}（班级平均 {batch.AverageScore:0.#}），" +
                  $"「{string.Join("」「", weak.Take(3).Select(w => w.Name))}」需要加强，已为你生成复习建议。"
                : $"你在本次考试中得分 {script.TotalScore:0.#}，各知识点均已达标，继续保持！";
        }

        return new StudentReportPreviewDto
        {
            StudentId = script.StudentId,
            StudentName = script.StudentName,
            StudentNumber = script.StudentNumber,
            Title = $"【成绩分析】{batch.Title} · 个人薄弱点报告",
            Content = content,
            Pushed = report?.PushedAt != null,
            PushedAt = report?.PushedAt,
            Items = weak.Select(w => new LossAttributionDto
            {
                QuestionNo = string.Empty,
                ExerciseTitle = w.ChapterTitle ?? string.Empty,
                KnowledgePointName = w.Name,
                Mastery = Math.Round(w.Mastery, 4),
                Reason = w.Evidence <= 0 ? "暂无作答数据（模型预测）" : $"掌握率 {(int)Math.Round(w.Mastery * 100)}%，低于达标线 {(int)(thresholds.For(w.KnowledgePointId).Ok * 100)}%",
                Advice = adviceByName.GetValueOrDefault(w.Name)
                         ?? $"完成「{w.Name}」专项练习，并回看对应讲解",
            }).ToList(),
        };
    }

    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private async Task<(PaperBatch Batch, Course? Course)> LoadBatchContextAsync(Guid batchId)
    {
        var batch = await _batchRepository.GetAsync(batchId);
        var course = await _courseRepository.FindAsync(batch.CourseId);
        return (batch, course);
    }

    /// <summary>学生成绩明细（含排名与薄弱点），导出用。</summary>
    private async Task<List<StudentScoreRow>> BuildStudentScoreRowsAsync(Guid batchId, ClassWeaknessDto weakness)
    {
        var scripts = (await _scriptRepository.GetListAsync(s => s.BatchId == batchId))
            .Where(s => s.StudentId.HasValue)
            .OrderByDescending(s => s.TotalScore)
            .ToList();

        var studentIds = scripts.Select(s => s.StudentId!.Value).Distinct().ToList();
        var thresholds = await _thresholdResolver.ResolveAllAsync(weakness.CourseId);
        var snapshotsByStudent = studentIds.Any()
            ? await _masteryService.ComputeForStudentsAsync(studentIds, weakness.CourseId)
            : new Dictionary<Guid, List<KpMasterySnapshot>>();

        var rows = new List<StudentScoreRow>();
        var rank = 0;
        foreach (var script in scripts)
        {
            rank++;
            snapshotsByStudent.TryGetValue(script.StudentId!.Value, out var snaps);
            var weakest = (snaps ?? new List<KpMasterySnapshot>())
                .Where(s => s.Evidence > 0 && thresholds.Judge(s.KnowledgePointId, s.Mastery) == MasteryGrade.Weak)
                .OrderBy(s => s.Mastery)
                .Take(2)
                .Select(s => s.Name)
                .ToList();

            rows.Add(new StudentScoreRow(
                script.StudentNumber,
                script.StudentName,
                Math.Round(script.TotalScore, 1),
                rank,
                weakest,
                weakness.FocusStudents.FirstOrDefault(f => f.StudentId == script.StudentId)?.Pushed ?? false));
        }

        return rows;
    }

    /// <summary>从已生成报告的 ContentJson 里取出「知识点 → 建议」映射，导出时复用真实建议。</summary>
    private static Dictionary<string, string> ExtractAdvice(string? contentJson)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(contentJson))
        {
            return result;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(contentJson);
            if (!doc.RootElement.TryGetProperty("WeakPoints", out var weakPoints) &&
                !doc.RootElement.TryGetProperty("weakPoints", out weakPoints))
            {
                return result;
            }

            if (weakPoints.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return result;
            }

            foreach (var item in weakPoints.EnumerateArray())
            {
                var name = item.TryGetProperty("Name", out var n) ? n.GetString()
                    : item.TryGetProperty("name", out var n2) ? n2.GetString() : null;
                var advice = item.TryGetProperty("Advice", out var a) ? a.GetString()
                    : item.TryGetProperty("advice", out var a2) ? a2.GetString() : null;

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(advice))
                {
                    result[name!] = advice!;
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // 内容损坏时忽略，调用方回退规则文案
        }

        return result;
    }

    private static string SanitizeFileName(string? name)
    {
        var value = string.IsNullOrWhiteSpace(name) ? "未命名" : name.Trim();
        foreach (var invalid in System.IO.Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value.Length > 40 ? value[..40] : value;
    }

    public async Task<PushResultDto> PushPreCourseReviewsAsync()
    {
        var service = (PreCourseReviewPushService)_serviceProvider.GetService(typeof(PreCourseReviewPushService))!;
        var count = await service.PushAsync(CurrentTenant.Id);
        return new PushResultDto { PushedCount = count };
    }

    public async Task<PaperBatchDto> CreatePaperBatchAsync(CreatePaperBatchInput input)
    {
        var batch = new PaperBatch(_guidGenerator.Create(), input.CourseId, input.CourseName, input.Title)
        {
            ExamDate = input.ExamDate,
            SourceFileNames = input.FileNames.Any() ? string.Join(",", input.FileNames) : null,
            Status = PaperBatchStatus.Draft,
        };

        if (string.IsNullOrWhiteSpace(batch.CourseName))
        {
            var course = await _courseRepository.FindAsync(input.CourseId);
            batch.CourseName = course?.Title ?? string.Empty;
        }

        await _batchRepository.InsertAsync(batch, autoSave: true);
        return ToBatchDto(batch);
    }

    public async Task<List<PaperScriptRowDto>> GetPaperScriptsAsync(Guid batchId)
    {
        var scripts = await _scriptRepository.GetListAsync(s => s.BatchId == batchId);
        if (!scripts.Any())
        {
            return new List<PaperScriptRowDto>();
        }

        var scriptIds = scripts.Select(s => s.Id).ToList();
        var items = await _itemRepository.GetListAsync(i => scriptIds.Contains(i.ScriptId));

        var exerciseIds = items.Where(i => i.ExerciseId.HasValue).Select(i => i.ExerciseId!.Value).Distinct().ToList();
        var exercises = exerciseIds.Any()
            ? (await _exerciseRepository.GetListAsync(e => exerciseIds.Contains(e.Id))).ToDictionary(e => e.Id)
            : new Dictionary<Guid, Exercise>();
        var maps = exerciseIds.Any()
            ? await _mapRepository.GetListAsync(m => exerciseIds.Contains(m.ExerciseId))
            : new List<ExerciseKnowledgePoint>();
        var kpIds = maps.Select(m => m.KnowledgePointId).Distinct().ToList();
        var kps = kpIds.Any()
            ? (await _kpRepository.GetListAsync(k => kpIds.Contains(k.Id))).ToDictionary(k => k.Id)
            : new Dictionary<Guid, KnowledgePoint>();

        var result = new List<PaperScriptRowDto>();
        foreach (var script in scripts.OrderBy(s => s.StudentName))
        {
            var row = new PaperScriptRowDto
            {
                Id = script.Id,
                StudentId = script.StudentId,
                StudentName = script.StudentName,
                StudentNumber = script.StudentNumber,
                TotalScore = Math.Round(script.TotalScore, 1),
                OcrConfidence = Math.Round(script.OcrConfidence, 3),
                Confirmed = script.Confirmed,
            };

            foreach (var item in items.Where(i => i.ScriptId == script.Id).OrderBy(i => ParseQuestionNo(i.QuestionNo)))
            {
                exercises.TryGetValue(item.ExerciseId ?? Guid.Empty, out var exercise);
                row.Items.Add(new PaperItemRowDto
                {
                    Id = item.Id,
                    QuestionNo = item.QuestionNo,
                    ExerciseId = item.ExerciseId,
                    ExerciseTitle = exercise?.Title ?? "（未匹配到题库原题）",
                    MatchStatus = item.MatchStatus switch
                    {
                        PaperMatchStatus.Matched => "matched",
                        PaperMatchStatus.Low => "low",
                        _ => "none",
                    },
                    Score = item.Score,
                    FullScore = item.FullScore,
                    OcrConfidence = Math.Round(item.OcrConfidence, 3),
                    KnowledgePointNames = maps.Where(m => m.ExerciseId == item.ExerciseId)
                        .Select(m => kps.TryGetValue(m.KnowledgePointId, out var kp) ? kp.Name : null)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x!)
                        .Distinct()
                        .ToList(),
                });
            }

            result.Add(row);
        }

        return result;
    }

    public async Task ConfirmPaperScriptAsync(Guid scriptId, ConfirmPaperScriptInput input)
    {
        var script = await _scriptRepository.GetAsync(scriptId);
        var items = await _itemRepository.GetListAsync(i => i.ScriptId == scriptId);

        var byId = items.ToDictionary(i => i.Id);
        foreach (var incoming in input.Items)
        {
            if (!byId.TryGetValue(incoming.Id, out var item))
            {
                continue;
            }

            item.Score = Math.Clamp(incoming.Score, 0, item.FullScore);
            item.MatchStatus = item.ExerciseId.HasValue ? PaperMatchStatus.Matched : PaperMatchStatus.None;
            await _itemRepository.UpdateAsync(item, autoSave: true);
        }

        var refreshed = await _itemRepository.GetListAsync(i => i.ScriptId == scriptId);
        script.TotalScore = refreshed.Sum(i => i.Score);
        script.Confirmed = true;
        script.ConfirmedAt = DateTime.UtcNow;
        await _scriptRepository.UpdateAsync(script, autoSave: true);

        // 校对通过后才写入作答记录，避免污染掌握度矩阵
        await _answerRepository.DeleteAsync(a => a.PaperScriptId == script.Id);
        if (script.StudentId.HasValue)
        {
            foreach (var item in refreshed.Where(i => i.ExerciseId.HasValue && i.FullScore > 0))
            {
                await _answerRepository.InsertAsync(new AnswerRecord(
                    _guidGenerator.Create(), script.StudentId.Value, await ResolveCourseIdAsync(script.BatchId), item.ExerciseId!.Value)
                {
                    ChapterId = item.ChapterId,
                    Score = item.Score,
                    FullScore = item.FullScore,
                    Source = AnswerSource.Paper,
                    AnsweredAt = DateTime.UtcNow,
                    PaperScriptId = script.Id,
                }, autoSave: true);
            }

            var courseId = await ResolveCourseIdAsync(script.BatchId);
            await _masteryService.RecalculateAsync(script.StudentId.Value, courseId);
        }

        await RefreshBatchCountersAsync(script.BatchId);
    }

    public async Task<PushResultDto> PushReportsAsync(Guid batchId)
    {
        var batch = await _batchRepository.GetAsync(batchId);
        var scripts = await _scriptRepository.GetListAsync(s => s.BatchId == batchId && s.StudentId != null);
        var course = await _courseRepository.FindAsync(batch.CourseId);
        var thresholds = await _thresholdResolver.ResolveAsync(batch.CourseId);

        var pushed = 0;
        var llmBudget = 8; // 控制单次推送的模型调用次数，其余走规则文本
        foreach (var script in scripts)
        {
            if (!script.StudentId.HasValue)
            {
                continue;
            }

            var snapshots = await _masteryService.ComputeAsync(script.StudentId.Value, batch.CourseId);
            var weakSources = snapshots
                .Where(s => s.Evidence >= 1 && thresholds.Judge(s.Mastery) != MasteryGrade.Ok)
                .OrderBy(s => s.Mastery)
                .Select(s => new AnalysisReportWriter.WeakPointTextSource(s.Name, s.Mastery, s.ChapterTitle))
                .ToList();

            if (!weakSources.Any())
            {
                continue;
            }

            var text = await _reportWriter.WriteStudentAsync(
                course?.Title ?? batch.CourseName, script.TotalScore, batch.AverageScore, weakSources,
                allowLlm: llmBudget-- > 0);

            var report = new AnalysisReport(_guidGenerator.Create(), AnalysisScope.Student, batch.CourseId)
            {
                StudentId = script.StudentId,
                PaperBatchId = batch.Id,
                Title = $"{batch.Title} · 个人薄弱点报告",
                Summary = text.Summary,
                FactsJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    course = course?.Title,
                    score = script.TotalScore,
                    classAverage = batch.AverageScore,
                    weak = weakSources.Select(w => new { w.Name, w.Mastery }),
                }),
                ContentJson = System.Text.Json.JsonSerializer.Serialize(text),
                GeneratedByLlm = text.UsedLlm,
                PushedAt = DateTime.UtcNow,
            };
            await _reportRepository.InsertAsync(report, autoSave: true);

            var notification = new StudentNotification(
                _guidGenerator.Create(),
                script.StudentId.Value,
                "【成绩分析】个人薄弱点报告已生成",
                text.Summary)
            {
                Type = AnalysisNotificationType.PaperWeakness,
                Source = "老师",
                CourseId = batch.CourseId,
                RelatedId = report.Id,
            };
            await _notificationRepository.InsertAsync(notification, autoSave: true);
            pushed++;
        }

        batch.PushedCount = pushed;
        batch.Status = PaperBatchStatus.Pushed;
        await _batchRepository.UpdateAsync(batch, autoSave: true);

        return new PushResultDto { PushedCount = pushed };
    }

    private async Task RefreshBatchCountersAsync(Guid batchId)
    {
        await _batchStats.RefreshAsync(batchId);
    }

    /// <summary>UoW 提交后再执行（入队/落盘），避免后台任务读不到未提交数据。</summary>
    private Task RunAfterCommitAsync(Func<Task> action)
    {
        var uow = _unitOfWorkManager.Current;
        if (uow == null)
        {
            return action();
        }

        uow.OnCompleted(action);
        return Task.CompletedTask;
    }

    /// <summary>校验 OSS 图片 URL 归属：只允许本租户配置的 OSS Bucket / 自定义域名，避免任意 URL 被送去第三方模型。</summary>
    private async Task<string> ValidateOssImageUrlAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new UserFriendlyException("图片地址不合法");
        }

        var options = await _ossCredentials.GetOptionsAsync();
        if (!options.IsConfigured)
        {
            throw new UserFriendlyException("未配置 OSS，无法使用图片直传；请改用文件上传");
        }

        var allowedHosts = new List<string> { options.BucketHost };
        if (!string.IsNullOrWhiteSpace(options.UrlPrefix) &&
            Uri.TryCreate(options.UrlPrefix, UriKind.Absolute, out var prefixUri))
        {
            allowedHosts.Add(prefixUri.Host);
        }

        if (!allowedHosts.Any(h => string.Equals(h, uri.Host, StringComparison.OrdinalIgnoreCase)))
        {
            throw new UserFriendlyException($"图片地址必须来自本站 OSS（{string.Join(" / ", allowedHosts)}）");
        }

        return uri.ToString();
    }

    private static string StripDataUrl(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return string.Empty;
        }

        var comma = base64.IndexOf(',');
        return base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0
            ? base64[(comma + 1)..]
            : base64;
    }

    private static string GuessContentType(string fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/jpeg",
        };
    }

    private async Task<Guid> ResolveCourseIdAsync(Guid batchId)
    {
        var batch = await _batchRepository.GetAsync(batchId);
        return batch.CourseId;
    }

    // ══════════════════════════════════════════════════════════
    //  教师端 · 班级薄弱点
    // ══════════════════════════════════════════════════════════

    public async Task<ClassWeaknessDto> GetClassWeaknessAsync(Guid batchId)
    {
        var batch = await _batchRepository.GetAsync(batchId);
        var course = await _courseRepository.FindAsync(batch.CourseId);
        var thresholds = await _thresholdResolver.ResolveAllAsync(batch.CourseId);
        var scripts = await _scriptRepository.GetListAsync(s => s.BatchId == batchId);

        var studentIds = scripts.Where(s => s.StudentId.HasValue).Select(s => s.StudentId!.Value).Distinct().ToList();
        var snapshotsByStudent = studentIds.Any()
            ? await _masteryService.ComputeForStudentsAsync(studentIds, batch.CourseId)
            : new Dictionary<Guid, List<KpMasterySnapshot>>();

        // 聚合：按知识点平均掌握度（证据加权）
        var kpAgg = new Dictionary<Guid, (string Name, Guid? ChapterId, string? ChapterTitle, double Weighted, double Weight, int SyllabusOrder)>();
        foreach (var snaps in snapshotsByStudent.Values)
        {
            foreach (var snap in snaps.Where(s => s.Evidence > 0))
            {
                if (!kpAgg.TryGetValue(snap.KnowledgePointId, out var agg))
                {
                    agg = (snap.Name, snap.ChapterId, snap.ChapterTitle, 0, 0, snap.SyllabusOrder);
                }

                agg = (agg.Name, agg.ChapterId, agg.ChapterTitle,
                    agg.Weighted + snap.Mastery * snap.Evidence,
                    agg.Weight + snap.Evidence,
                    agg.SyllabusOrder);
                kpAgg[snap.KnowledgePointId] = agg;
            }
        }

        var knowledgePoints = kpAgg
            .Select(kv =>
            {
                var mastery = kv.Value.Weight > 0 ? kv.Value.Weighted / kv.Value.Weight : 0;
                var effective = thresholds.For(kv.Key);
                return new KnowledgeMasteryItemDto
                {
                    KnowledgePointId = kv.Key,
                    Name = kv.Value.Name,
                    ChapterTitle = kv.Value.ChapterTitle,
                    Mastery = Math.Round(mastery, 4),
                    Evidence = Math.Round(kv.Value.Weight, 2),
                    Level = (int)effective.Judge(mastery),
                    TargetLine = effective.Ok,
                    HasCustomThreshold = thresholds.Overrides.ContainsKey(kv.Key),
                };
            })
            .OrderBy(i => i.Mastery)
            .ToList();

        // 重点关注学生：总分最低，且取各自最薄弱的 2 个知识点
        var pushedStudentIds = await GetPushedStudentIdsAsync(batchId);
        var focusStudents = new List<FocusStudentDto>();
        foreach (var script in scripts.Where(s => s.StudentId.HasValue).OrderBy(s => s.TotalScore).Take(10))
        {
            snapshotsByStudent.TryGetValue(script.StudentId!.Value, out var snaps);
            var weakest = (snaps ?? new List<KpMasterySnapshot>())
                .Where(s => s.Evidence > 0 && thresholds.Judge(s.KnowledgePointId, s.Mastery) == MasteryGrade.Weak)
                .OrderBy(s => s.Mastery)
                .Take(2)
                .Select(s => s.Name)
                .ToList();

            focusStudents.Add(new FocusStudentDto
            {
                StudentId = script.StudentId!.Value,
                StudentName = script.StudentName,
                TotalScore = Math.Round(script.TotalScore, 1),
                WeakestNames = weakest,
                Pushed = pushedStudentIds.Contains(script.StudentId!.Value),
            });
        }

        return new ClassWeaknessDto
        {
            BatchId = batch.Id,
            BatchTitle = batch.Title,
            CourseId = batch.CourseId,
            CourseName = course?.Title ?? batch.CourseName,
            TargetLine = thresholds.Default.Ok,
            KnowledgePoints = knowledgePoints,
            FocusStudents = focusStudents,
            PushedCount = pushedStudentIds.Count,
            TotalStudents = studentIds.Count,
        };
    }

    private async Task<HashSet<Guid>> GetPushedStudentIdsAsync(Guid batchId)
    {
        var reports = await _reportRepository.GetListAsync(r => r.PaperBatchId == batchId && r.StudentId != null);
        return reports.Where(r => r.StudentId.HasValue).Select(r => r.StudentId!.Value).ToHashSet();
    }

    // ══════════════════════════════════════════════════════════
    //  教师端 · 教学成果分析
    // ══════════════════════════════════════════════════════════

    public async Task<TeachingInsightDto> GetTeachingInsightAsync(Guid batchId, bool useAiAdvice = true)
    {
        var batch = await _batchRepository.GetAsync(batchId);
        var course = await _courseRepository.FindAsync(batch.CourseId);
        var thresholds = await _thresholdResolver.ResolveAsync(batch.CourseId);

        var items = await _itemRepository.GetListAsync(i => i.BatchId == batchId);
        var scoredItems = items.Where(i => i.ChapterId.HasValue && i.FullScore > 0).ToList();

        var chapters = await _chapterRepository.GetListAsync(c => c.CourseId == batch.CourseId);
        var chapterById = chapters.ToDictionary(c => c.Id);

        var chapterStats = new List<ChapterStatDto>();
        foreach (var group in scoredItems.GroupBy(i => i.ChapterId!.Value))
        {
            var perStudent = group
                .GroupBy(i => i.ScriptId)
                .Select(g =>
                {
                    var total = g.Sum(i => i.FullScore);
                    return total > 0 ? g.Sum(i => i.Score) / total : 0;
                })
                .ToList();

            var title = chapterById.TryGetValue(group.Key, out var chapter) ? chapter.Title : "未命名章节";
            chapterStats.Add(new ChapterStatDto
            {
                ChapterId = group.Key,
                Title = title,
                Rate = Math.Round(perStudent.Any() ? perStudent.Average() : 0, 4),
                StdDev = Math.Round(MasteryCalculator.StdDev(perStudent), 4),
                TopQuartile = Math.Round(TopQuartile(perStudent), 4),
                SampleSize = perStudent.Count,
            });
        }

        chapterStats = chapterStats.OrderByDescending(c => c.Rate).ToList();
        var averageRate = chapterStats.Any() ? chapterStats.Average(c => c.Rate) : 0;

        // 较上届：同课程更早的批次
        var earlierBatches = (await _batchRepository.GetListAsync(b => b.CourseId == batch.CourseId))
            .Where(b => b.Id != batch.Id && b.ExamDate < batch.ExamDate)
            .OrderByDescending(b => b.ExamDate)
            .ToList();
        double delta = 0;
        foreach (var earlier in earlierBatches)
        {
            var earlierItems = await _itemRepository.GetListAsync(i => i.BatchId == earlier.Id && i.FullScore > 0);
            if (!earlierItems.Any())
            {
                continue;
            }

            var totalFull = earlierItems.Sum(i => i.FullScore);
            var earlierRate = totalFull > 0 ? earlierItems.Sum(i => i.Score) / totalFull : 0;
            delta = averageRate - earlierRate;
            break;
        }

        var facts = chapterStats.Select(c => new AnalysisReportWriter.TeachingChapterFact(
            c.Title, c.Rate, c.TopQuartile, c.StdDev, DiagnoseCode(Diagnose(c, thresholds)))).ToList();
        var factByTitle = facts.ToDictionary(f => f.Title);

        // 导出场景传 useAiAdvice=false：复用已持久化的建议或规则文案，避免导出时调模型
        var adviceByTitle = new Dictionary<string, string>();
        if (useAiAdvice)
        {
            var advice = await _reportWriter.WriteTeachingAsync(course?.Title ?? batch.CourseName, facts);
            adviceByTitle = advice.ToDictionary(a => a.ChapterTitle, a => a.Advice);
        }
        else
        {
            var persisted = await _teachingInsightRepository.GetListAsync(t => t.PaperBatchId == batch.Id);
            foreach (var item in persisted.Where(t => !string.IsNullOrWhiteSpace(t.Advice)))
            {
                adviceByTitle[item.ChapterTitle] = item.Advice!;
            }
        }

        var diagnoses = chapterStats.Select(c =>
        {
            var level = Diagnose(c, thresholds);
            return new TeachingDiagnosisDto
            {
                Level = level switch
                {
                    DiagnosisLevel.Good => "good",
                    DiagnosisLevel.Polarized => "polarized",
                    DiagnosisLevel.TeachingGap => "gap",
                    _ => "fair",
                },
                ChapterTitle = c.Title,
                Rate = c.Rate,
                Gap = Math.Round(c.Rate - thresholds.Ok, 4),
                // LLM 只返回部分章节（或导出未调模型）时，缺的章节用规则文案补齐，保证每章都有建议
                Advice = adviceByTitle.GetValueOrDefault(c.Title)
                    ?? AnalysisReportWriter.RuleTeachingAdvice(factByTitle[c.Title]),
            };
        }).ToList();

        await PersistTeachingInsightsAsync(batch, chapterStats, diagnoses);

        return new TeachingInsightDto
        {
            BatchId = batch.Id,
            CourseId = batch.CourseId,
            CourseName = course?.Title ?? batch.CourseName,
            AverageRate = Math.Round(averageRate, 4),
            DeltaVsPrevious = Math.Round(delta, 4),
            BelowTargetCount = chapterStats.Count(c => c.Rate < thresholds.Ok),
            TargetLine = thresholds.Ok,
            Chapters = chapterStats,
            Diagnoses = diagnoses,
            GeneratedAt = DateTime.UtcNow,
        };
    }

    private static string DiagnoseCode(DiagnosisLevel level) => level switch
    {
        DiagnosisLevel.Good => "good",
        DiagnosisLevel.Polarized => "polarized",
        DiagnosisLevel.TeachingGap => "gap",
        _ => "fair",
    };

    /// <summary>规则先行：连高分学生也失分 → 讲解不足；标准差大 → 两极分化。</summary>
    private static DiagnosisLevel Diagnose(ChapterStatDto stat, Thresholds thresholds)
    {
        if (stat.Rate >= thresholds.Ok)
        {
            return DiagnosisLevel.Good;
        }

        if (stat.TopQuartile < thresholds.Ok && stat.Rate < thresholds.Weak)
        {
            return DiagnosisLevel.TeachingGap;
        }

        if (stat.StdDev > 0.25)
        {
            return DiagnosisLevel.Polarized;
        }

        return stat.Rate >= thresholds.Weak ? DiagnosisLevel.Fair : DiagnosisLevel.TeachingGap;
    }

    private static double TopQuartile(List<double> values)
    {
        if (!values.Any())
        {
            return 0;
        }

        var ordered = values.OrderByDescending(v => v).ToList();
        var count = Math.Max(1, (int)Math.Ceiling(ordered.Count * 0.25));
        return ordered.Take(count).Average();
    }

    private async Task PersistTeachingInsightsAsync(
        PaperBatch batch,
        List<ChapterStatDto> stats,
        List<TeachingDiagnosisDto> diagnoses)
    {
        await _teachingInsightRepository.DeleteAsync(t => t.PaperBatchId == batch.Id);

        foreach (var stat in stats)
        {
            var diagnosis = diagnoses.FirstOrDefault(d => d.ChapterTitle == stat.Title);
            var level = diagnosis?.Level switch
            {
                "good" => DiagnosisLevel.Good,
                "polarized" => DiagnosisLevel.Polarized,
                "gap" => DiagnosisLevel.TeachingGap,
                _ => DiagnosisLevel.Fair,
            };

            var insight = new TeachingInsight(_guidGenerator.Create(), batch.CourseId, stat.Title)
            {
                PaperBatchId = batch.Id,
                ChapterId = stat.ChapterId,
                AverageRate = stat.Rate,
                StdDev = stat.StdDev,
                TopQuartileRate = stat.TopQuartile,
                SampleSize = stat.SampleSize,
                Diagnosis = level,
                Advice = diagnosis?.Advice,
            };
            await _teachingInsightRepository.InsertAsync(insight, autoSave: true);
        }
    }

    // ══════════════════════════════════════════════════════════
    //  教师端 · 课程联动
    // ══════════════════════════════════════════════════════════

    public async Task<CourseLinkConfigDto> GetCourseLinkAsync(Guid courseId)
    {
        var course = await _courseRepository.FindAsync(courseId);
        var kps = await _indexer.EnsureAsync(courseId);
        var kpIds = kps.Select(k => k.Id).ToList();

        var edges = kpIds.Any()
            ? await _prerequisiteRepository.GetListAsync(e => kpIds.Contains(e.ToKpId))
            : new List<KpPrerequisite>();

        var fromKpIds = edges.Select(e => e.FromKpId).Distinct().ToList();
        var fromKps = fromKpIds.Any()
            ? (await _kpRepository.GetListAsync(k => fromKpIds.Contains(k.Id))).ToDictionary(k => k.Id)
            : new Dictionary<Guid, KnowledgePoint>();

        var fromCourseIds = fromKps.Values.Select(k => k.CourseId).Distinct().ToList();
        var fromCourses = fromCourseIds.Any()
            ? (await _courseRepository.GetListAsync(c => fromCourseIds.Contains(c.Id))).ToDictionary(c => c.Id, c => c.Title)
            : new Dictionary<Guid, string>();

        var kpById = kps.ToDictionary(k => k.Id);
        var mappings = new List<PrerequisiteMappingDto>();

        foreach (var group in edges.GroupBy(e => e.ToKpId))
        {
            if (!kpById.TryGetValue(group.Key, out var toKp))
            {
                continue;
            }

            var first = group.First();
            var deps = group.Select(e => fromKps.GetValueOrDefault(e.FromKpId)).Where(k => k != null).Select(k => k!).ToList();

            mappings.Add(new PrerequisiteMappingDto
            {
                Id = first.Id,
                SuccessorChapterTitle = toKp.Name,
                DependencyNames = deps.Select(d => d.Name).Distinct().ToList(),
                SourceCourseTitle = deps
                    .Select(d => fromCourses.GetValueOrDefault(d.CourseId, string.Empty))
                    .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? string.Empty,
                Condition = first.Condition ?? "掌握率 < 60%",
                Status = first.Status == PrerequisiteStatus.Confirmed ? 1 : 0,
                Source = first.Source.ToString(),
                ToKpId = toKp.Id,
                FromKpIds = deps.Select(d => d.Id).ToList(),
            });
        }

        // 课程路径（继承学生端逻辑，但不计算复习包）
        var path = await BuildCourseLinkPathAsync(courseId);

        var links = await _courseLinkRepository.GetListAsync();
        var linkCourseIds = links.SelectMany(l => new[] { l.PredecessorCourseId, l.SuccessorCourseId })
            .Distinct().ToList();
        var linkCourses = linkCourseIds.Any()
            ? (await _courseRepository.GetListAsync(c => linkCourseIds.Contains(c.Id)))
                .ToDictionary(c => c.Id, c => c.Title)
            : new Dictionary<Guid, string>();

        return new CourseLinkConfigDto
        {
            CourseId = courseId,
            CourseName = course?.Title ?? string.Empty,
            Path = path,
            Mappings = mappings,
            Links = links
                .Where(l => l.PredecessorCourseId == courseId || l.SuccessorCourseId == courseId)
                .Select(l => new CourseLinkDto
                {
                    Id = l.Id,
                    PredecessorCourseId = l.PredecessorCourseId,
                    PredecessorCourseTitle = linkCourses.GetValueOrDefault(l.PredecessorCourseId, string.Empty),
                    SuccessorCourseId = l.SuccessorCourseId,
                    SuccessorCourseTitle = linkCourses.GetValueOrDefault(l.SuccessorCourseId, string.Empty),
                })
                .ToList(),
        };
    }

    public async Task<CourseLinkDto> SaveCourseLinkAsync(SaveCourseLinkInput input)
    {
        if (input.PredecessorCourseId == input.SuccessorCourseId)
        {
            throw new UserFriendlyException("前置课程与后续课程不能相同");
        }

        var predecessor = await _courseRepository.FindAsync(input.PredecessorCourseId)
                          ?? throw new UserFriendlyException("前置课程不存在");
        var successor = await _courseRepository.FindAsync(input.SuccessorCourseId)
                        ?? throw new UserFriendlyException("后续课程不存在");

        var existing = await _courseLinkRepository.GetListAsync(l =>
            l.PredecessorCourseId == input.PredecessorCourseId &&
            l.SuccessorCourseId == input.SuccessorCourseId);

        var link = existing.FirstOrDefault();
        if (link == null)
        {
            link = new CourseLink(_guidGenerator.Create(), input.PredecessorCourseId, input.SuccessorCourseId);
            link.Status = PrerequisiteStatus.Confirmed;
            await _courseLinkRepository.InsertAsync(link, autoSave: true);
        }
        else
        {
            link.Status = PrerequisiteStatus.Confirmed;
            await _courseLinkRepository.UpdateAsync(link, autoSave: true);
        }

        return new CourseLinkDto
        {
            Id = link.Id,
            PredecessorCourseId = link.PredecessorCourseId,
            PredecessorCourseTitle = predecessor.Title,
            SuccessorCourseId = link.SuccessorCourseId,
            SuccessorCourseTitle = successor.Title,
        };
    }

    public async Task DeleteCourseLinkAsync(Guid courseLinkId)
    {
        await _courseLinkRepository.DeleteAsync(l => l.Id == courseLinkId);
    }

    private async Task<List<CoursePathNodeDto>> BuildCourseLinkPathAsync(Guid courseId)
    {
        var links = await _courseLinkRepository.GetListAsync(x => x.Status == PrerequisiteStatus.Confirmed);
        var ordered = new List<Guid>();
        var visited = new HashSet<Guid>();

        void WalkBack(Guid id, int depth)
        {
            if (depth <= 0) return;
            var predecessor = links.FirstOrDefault(l => l.SuccessorCourseId == id);
            if (predecessor == null || visited.Contains(predecessor.PredecessorCourseId)) return;
            visited.Add(predecessor.PredecessorCourseId);
            WalkBack(predecessor.PredecessorCourseId, depth - 1);
            ordered.Add(predecessor.PredecessorCourseId);
        }

        visited.Add(courseId);
        WalkBack(courseId, 3);
        ordered.Add(courseId);

        var successor = links.FirstOrDefault(l => l.PredecessorCourseId == courseId);
        while (successor != null && !visited.Contains(successor.SuccessorCourseId))
        {
            visited.Add(successor.SuccessorCourseId);
            ordered.Add(successor.SuccessorCourseId);
            successor = links.FirstOrDefault(l => l.PredecessorCourseId == successor.SuccessorCourseId);
        }

        var courses = await _courseRepository.GetListAsync(c => ordered.Contains(c.Id));
        var byId = courses.ToDictionary(c => c.Id);

        return ordered.Where(id => byId.ContainsKey(id)).Select(id =>
        {
            var isCurrent = id == courseId;
            var index = ordered.IndexOf(id);
            return new CoursePathNodeDto
            {
                CourseId = id,
                CourseTitle = byId[id].Title,
                State = isCurrent ? 1 : index < ordered.IndexOf(courseId) ? 0 : 2,
                Tag = isCurrent ? "当前课程" : index < ordered.IndexOf(courseId) ? "前置课程" : "后续课程",
            };
        }).ToList();
    }

    public async Task<PrerequisiteMappingDto> SavePrerequisiteAsync(SavePrerequisiteInput input)
    {
        var courseKps = await _indexer.EnsureAsync(input.CourseId);
        if (!courseKps.Any())
        {
            throw new UserFriendlyException("该课程还没有知识点，无法配置前置映射");
        }

        // 解析后续知识点：显式指定 → 按标题匹配 → 取课程最后一个知识点
        var toKp = input.ToKpId.HasValue
            ? courseKps.FirstOrDefault(k => k.Id == input.ToKpId.Value)
            : null;
        toKp ??= courseKps.FirstOrDefault(k =>
            !string.IsNullOrWhiteSpace(input.SuccessorChapterTitle) && k.Name == input.SuccessorChapterTitle);
        toKp ??= courseKps.OrderBy(k => k.SyllabusOrder).Last();

        // 解析前置知识点：显式指定 → 前置课程知识点 → 同课程更早的知识点（作为建议）
        var fromKps = new List<KnowledgePoint>();
        if (input.FromKpIds.Any())
        {
            fromKps = (await _kpRepository.GetListAsync(k => input.FromKpIds.Contains(k.Id))).ToList();
        }

        if (!fromKps.Any())
        {
            var links = await _courseLinkRepository.GetListAsync(l => l.SuccessorCourseId == input.CourseId);
            var predecessor = links.FirstOrDefault();
            if (predecessor != null)
            {
                var predecessorKps = await _indexer.EnsureAsync(predecessor.PredecessorCourseId);
                if (predecessorKps.Any())
                {
                    fromKps.Add(predecessorKps.OrderBy(k => k.SyllabusOrder).First());
                }
            }
        }

        if (!fromKps.Any())
        {
            var earlier = courseKps.Where(k => k.Id != toKp.Id && k.SyllabusOrder < toKp.SyllabusOrder)
                .OrderByDescending(k => k.SyllabusOrder)
                .FirstOrDefault();
            if (earlier != null)
            {
                fromKps.Add(earlier);
            }
        }

        if (!fromKps.Any())
        {
            throw new UserFriendlyException("无法推断前置知识点，请先在「课程联动」中配置前置课程，或手动选择前置知识点");
        }

        var source = Enum.TryParse<PrerequisiteSource>(input.Source, true, out var parsedSource)
            ? parsedSource
            : PrerequisiteSource.Manual;
        var status = input.Status == 1 ? PrerequisiteStatus.Confirmed : PrerequisiteStatus.Proposed;

        foreach (var from in fromKps)
        {
            var existing = await _prerequisiteRepository.GetListAsync(e =>
                e.FromKpId == from.Id && e.ToKpId == toKp.Id);
            var edge = existing.FirstOrDefault();
            if (edge == null)
            {
                edge = new KpPrerequisite(_guidGenerator.Create(), from.Id, toKp.Id);
                edge.Source = source;
                edge.Status = status;
                edge.Condition = input.Condition ?? "掌握率 < 60%";
                edge.Strength = 1;
                await _prerequisiteRepository.InsertAsync(edge, autoSave: true);
            }
            else
            {
                edge.Source = source;
                edge.Status = status;
                edge.Condition = input.Condition ?? edge.Condition;
                await _prerequisiteRepository.UpdateAsync(edge, autoSave: true);
            }
        }

        var allEdges = await _prerequisiteRepository.GetListAsync(e => e.ToKpId == toKp.Id);
        var first = allEdges.First();
        var fromIds = allEdges.Select(e => e.FromKpId).ToList();
        var fromEntities = await _kpRepository.GetListAsync(k => fromIds.Contains(k.Id));
        var fromCourseIds = fromEntities.Select(f => f.CourseId).Distinct().ToList();
        var fromCourses = fromCourseIds.Any()
            ? (await _courseRepository.GetListAsync(c => fromCourseIds.Contains(c.Id))).ToDictionary(c => c.Id, c => c.Title)
            : new Dictionary<Guid, string>();

        return new PrerequisiteMappingDto
        {
            Id = first.Id,
            SuccessorChapterTitle = toKp.Name,
            DependencyNames = fromEntities.Select(f => f.Name).Distinct().ToList(),
            SourceCourseTitle = fromEntities
                .Select(f => fromCourses.GetValueOrDefault(f.CourseId, string.Empty))
                .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? string.Empty,
            Condition = first.Condition ?? "掌握率 < 60%",
            Status = first.Status == PrerequisiteStatus.Confirmed ? 1 : 0,
            Source = first.Source.ToString(),
            ToKpId = toKp.Id,
            FromKpIds = fromIds,
        };
    }

    public async Task DeletePrerequisiteAsync(Guid prerequisiteId)
    {
        var edges = await _prerequisiteRepository.GetListAsync(e => e.Id == prerequisiteId);
        var edge = edges.FirstOrDefault();
        if (edge == null)
        {
            return;
        }

        // 同组（同一后续知识点）一并删除，保持映射行一致
        await _prerequisiteRepository.DeleteAsync(e => e.ToKpId == edge.ToKpId);
    }

    // ══════════════════════════════════════════════════════════
    //  知识点 / 判定线
    // ══════════════════════════════════════════════════════════

    public async Task<List<KnowledgePointDto>> EnsureCourseKnowledgePointsAsync(Guid courseId)
    {
        await _indexer.EnsureAsync(courseId);
        return await GetKnowledgePointsAsync(courseId);
    }

    public async Task<List<KnowledgePointDto>> GetKnowledgePointsAsync(Guid courseId)
    {
        var kps = await _kpRepository.GetListAsync(k => k.CourseId == courseId);
        var chapterIds = kps.Where(k => k.ChapterId.HasValue).Select(k => k.ChapterId!.Value).Distinct().ToList();
        var chapters = chapterIds.Any()
            ? (await _chapterRepository.GetListAsync(c => chapterIds.Contains(c.Id))).ToDictionary(c => c.Id, c => c.Title)
            : new Dictionary<Guid, string>();

        return kps
            .OrderBy(k => k.SyllabusOrder)
            .ThenBy(k => k.Name)
            .Select(k => new KnowledgePointDto
            {
                Id = k.Id,
                CourseId = k.CourseId,
                ChapterId = k.ChapterId,
                ChapterTitle = k.ChapterId.HasValue ? chapters.GetValueOrDefault(k.ChapterId.Value) : null,
                Name = k.Name,
                SyllabusOrder = k.SyllabusOrder,
                Status = (int)k.Status,
                Source = k.Source.ToString(),
                CanonicalId = k.CanonicalId,
            })
            .ToList();
    }

    public async Task<MasteryThresholdDto> GetThresholdAsync(Guid courseId, Guid? knowledgePoint = null)
    {
        var all = await _thresholdResolver.ResolveAllAsync(courseId);
        var effective = all.For(knowledgePoint);
        var isOverride = knowledgePoint.HasValue && all.Overrides.ContainsKey(knowledgePoint.Value);

        // 课程级是否有显式配置（用于「使用系统默认」提示）
        var courseRaw = await _thresholdResolver.GetRawAsync(courseId);

        return new MasteryThresholdDto
        {
            CourseId = courseId,
            KnowledgePointId = knowledgePoint,
            WeakLine = effective.Weak,
            OkLine = effective.Ok,
            IsDefault = !isOverride && courseRaw == null,
            IsOverride = isOverride,
            CourseWeakLine = courseRaw?.WeakLine,
            CourseOkLine = courseRaw?.OkLine,
        };
    }

    public async Task ClearThresholdAsync(SetThresholdInput input)
    {
        if (!input.KnowledgePointId.HasValue)
        {
            throw new UserFriendlyException("清除操作需要指定知识点");
        }

        await _thresholdResolver.RemoveOverrideAsync(input.CourseId, input.KnowledgePointId.Value);
    }

    public async Task SetThresholdAsync(SetThresholdInput input)
    {
        if (input.WeakLine < 0 || input.OkLine > 1 || input.WeakLine >= input.OkLine)
        {
            throw new UserFriendlyException("判定线配置不合法：需满足 0 ≤ 薄弱线 < 达标线 ≤ 1");
        }

        await _thresholdResolver.SetAsync(input.CourseId, input.KnowledgePointId, input.WeakLine, input.OkLine);
    }

    // ══════════════════════════════════════════════════════════
    //  辅助
    // ══════════════════════════════════════════════════════════

    private static KnowledgeMasteryItemDto ToMasteryItem(KpMasterySnapshot snapshot, CourseThresholds thresholds)
    {
        var effective = thresholds.For(snapshot.KnowledgePointId);
        return new KnowledgeMasteryItemDto
        {
            KnowledgePointId = snapshot.KnowledgePointId,
            Name = snapshot.Name,
            ChapterTitle = snapshot.ChapterTitle,
            Mastery = Math.Round(snapshot.Mastery, 4),
            Evidence = Math.Round(snapshot.Evidence, 2),
            Level = (int)effective.Judge(snapshot.Mastery),
            InsufficientData = snapshot.Evidence > 0 && snapshot.Evidence < 2,
            TargetLine = effective.Ok,
            HasCustomThreshold = thresholds.Overrides.ContainsKey(snapshot.KnowledgePointId),
        };
    }

    private static PaperBatchDto ToBatchDto(PaperBatch batch, List<PaperUploadImage>? images = null) => new()
    {
        Id = batch.Id,
        Title = batch.Title,
        CourseId = batch.CourseId,
        CourseName = batch.CourseName,
        ExamDate = batch.ExamDate,
        ScriptCount = batch.ScriptCount,
        NeedsConfirmCount = batch.NeedsConfirmCount,
        PushedCount = batch.PushedCount,
        AverageScore = batch.AverageScore,
        Status = (int)batch.Status,
        UploadedCount = images?.Count ?? 0,
        ProcessedCount = images?.Count(i => i.Status == PaperUploadStatus.Done) ?? 0,
        FailedCount = images?.Count(i => i.Status == PaperUploadStatus.Failed) ?? 0,
    };

    private static int ParseQuestionNo(string questionNo) =>
        int.TryParse(questionNo, out var n) ? n : int.MaxValue;
}
