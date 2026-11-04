using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KnowledgeHub.Assignments.Dtos;
using KnowledgeHub.Assignments.Enums;
using KnowledgeHub.Courses;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Permissions;
using KnowledgeHub.Resources.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace KnowledgeHub.Assignments;

/// <summary>
/// 课程作业应用服务。
///
/// 职责边界：
///   · 教师端 —— 发布作业（标题 / 说明 / 附件 / 截止时间 / 迟交规则 / 评分细则 / 允许格式），
///     查看提交与批阅进度，逐份批阅（得分明细 + 评语），导出成绩；
///   · 学生端 —— 查看本课程作业、提交（保存草稿 / 正式提交，可重复提交）、
///     查看成绩与反馈（含得分明细与提交记录）。
///
/// 数据口径：
///   · 「提交进度」= 已提交人数 / 课程选课人数（StudentCourse，非 Dropped）；
///   · 「批阅进度」= 已批阅份数 / 已提交份数；
///   · 迟交判定：提交时间晚于截止时间；是否允许迟交、扣多少分由作业配置决定。
/// </summary>
[Authorize]
[IgnoreAntiforgeryToken]
public class AssignmentAppService : KnowledgeHubAppService, IAssignmentAppService
{
    private readonly IRepository<CourseAssignment, Guid> _assignmentRepository;
    private readonly IRepository<AssignmentCriterion, Guid> _criterionRepository;
    private readonly IRepository<AssignmentFile, Guid> _fileRepository;
    private readonly IRepository<AssignmentSubmission, Guid> _submissionRepository;
    private readonly IRepository<SubmissionScore, Guid> _scoreRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IFileStorageService _fileStorage;

    /// <summary>当前登录用户 Id（教师 / 学生）。</summary>
    private Guid CurrentUserId => CurrentUser.Id ?? Guid.Empty;

    /// <summary>
    /// 立即落盘当前工作单元。
    /// ABP 的 ApplicationService 只暴露 CurrentUnitOfWork，需要显式调用 SaveChangesAsync，
    /// 否则新建的明细行在同一次请求内查询不到（EF 未提交）。
    /// </summary>
    private async Task SaveNowAsync()
    {
        if (CurrentUnitOfWork != null)
        {
            await CurrentUnitOfWork.SaveChangesAsync();
        }
    }

    public AssignmentAppService(
        IRepository<CourseAssignment, Guid> assignmentRepository,
        IRepository<AssignmentCriterion, Guid> criterionRepository,
        IRepository<AssignmentFile, Guid> fileRepository,
        IRepository<AssignmentSubmission, Guid> submissionRepository,
        IRepository<SubmissionScore, Guid> scoreRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IFileStorageService fileStorage)
    {
        _assignmentRepository = assignmentRepository;
        _criterionRepository = criterionRepository;
        _fileRepository = fileRepository;
        _submissionRepository = submissionRepository;
        _scoreRepository = scoreRepository;
        _courseRepository = courseRepository;
        _studentCourseRepository = studentCourseRepository;
        _userRepository = userRepository;
        _fileStorage = fileStorage;
    }

    // ══════════════════════════════════════════════════════════
    //  教师端：作业管理
    // ══════════════════════════════════════════════════════════

    public async Task<PagedResultDto<AssignmentListItemDto>> GetListAsync(AssignmentListQueryDto input)
    {
        var query = await _assignmentRepository.GetQueryableAsync();

        if (input.CourseId.HasValue && input.CourseId.Value != Guid.Empty)
        {
            query = query.Where(x => x.CourseId == input.CourseId.Value);
        }

        if (input.Status.HasValue)
        {
            query = query.Where(x => x.Status == input.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var kw = input.Filter.Trim();
            query = query.Where(x => x.Title.Contains(kw) || (x.Description != null && x.Description.Contains(kw)));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);

        var assignments = await AsyncExecuter.ToListAsync(
            query.OrderByDescending(x => x.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        var dtos = new List<AssignmentListItemDto>();
        foreach (var assignment in assignments)
        {
            dtos.Add(await MapListItemAsync(assignment));
        }

        return new PagedResultDto<AssignmentListItemDto>(totalCount, dtos);
    }

    public async Task<AssignmentStatisticsDto> GetStatisticsAsync(Guid courseId)
    {
        var assignmentQuery = await _assignmentRepository.GetQueryableAsync();
        if (courseId != Guid.Empty)
        {
            assignmentQuery = assignmentQuery.Where(x => x.CourseId == courseId);
        }

        var assignments = await AsyncExecuter.ToListAsync(assignmentQuery);
        var assignmentIds = assignments.Select(x => x.Id).ToList();

        var submissions = assignmentIds.Count == 0
            ? new List<AssignmentSubmission>()
            : await AsyncExecuter.ToListAsync(
                (await _submissionRepository.GetQueryableAsync()).Where(x => assignmentIds.Contains(x.AssignmentId)));

        var now = Clock.Now;
        var ongoing = assignments.Count(x => x.Status == AssignmentStatus.Published && x.Deadline > now);
        var pendingGrade = submissions.Count(x => x.Status == SubmissionStatus.Submitted);
        var graded = submissions.Where(x => x.Status == SubmissionStatus.Graded && x.Score.HasValue).ToList();
        var average = graded.Count == 0
            ? 0m
            : Math.Round(graded.Average(x => x.Score!.Value), 1);

        return new AssignmentStatisticsDto
        {
            TotalCount = assignments.Count,
            OngoingCount = ongoing,
            PendingGradeCount = pendingGrade,
            AverageScore = average,
            LateCount = submissions.Count(x => x.IsLate),
            TotalStudents = await CountEnrolledStudentsAsync(courseId)
        };
    }

    public async Task<AssignmentDetailDto> GetAsync(Guid id)
    {
        var assignment = await _assignmentRepository.GetAsync(id);
        return await MapDetailAsync(assignment);
    }

    [Authorize(KnowledgeHubPermissions.Assignments.Manage)]
    public async Task<AssignmentDetailDto> CreateAsync(CreateUpdateAssignmentDto input)
    {
        ValidateInput(input);

        var course = await _courseRepository.GetAsync(input.CourseId);
        if (course == null)
        {
            throw new UserFriendlyException("课程不存在");
        }

        var assignment = new CourseAssignment(GuidGenerator.Create(), input.CourseId, input.Title.Trim())
        {
            TenantId = CurrentTenant.Id,
            Description = input.Description?.Trim(),
            Type = input.Type,
            MaxScore = input.MaxScore,
            Deadline = input.Deadline,
            AllowLate = input.AllowLate,
            LatePenaltyPercent = input.LatePenaltyPercent,
            AllowedFileTypes = NormalizeFileTypes(input.AllowedFileTypes),
            MaxFileSizeMB = input.MaxFileSizeMB <= 0 ? 20 : input.MaxFileSizeMB,
            MaxFileCount = input.MaxFileCount <= 0 ? 5 : input.MaxFileCount,
            AllowResubmit = input.AllowResubmit,
            Status = AssignmentStatus.Draft,
            TeacherId = CurrentUserId,
            ChapterId = input.ChapterId
        };

        await _assignmentRepository.InsertAsync(assignment);
        await SaveCriteriaAsync(assignment, input.Criteria);
        await AttachRequirementFilesAsync(assignment, input.AttachmentFileIds);
        await SaveNowAsync();

        return await MapDetailAsync(assignment);
    }

    [Authorize(KnowledgeHubPermissions.Assignments.Manage)]
    public async Task<AssignmentDetailDto> UpdateAsync(Guid id, CreateUpdateAssignmentDto input)
    {
        ValidateInput(input);

        var assignment = await _assignmentRepository.GetAsync(id);
        if (assignment.Status == AssignmentStatus.Closed)
        {
            throw new UserFriendlyException("作业已结束，无法修改");
        }

        assignment.Title = input.Title.Trim();
        assignment.Description = input.Description?.Trim();
        assignment.Type = input.Type;
        assignment.MaxScore = input.MaxScore;
        assignment.Deadline = input.Deadline;
        assignment.AllowLate = input.AllowLate;
        assignment.LatePenaltyPercent = input.LatePenaltyPercent;
        assignment.AllowedFileTypes = NormalizeFileTypes(input.AllowedFileTypes);
        assignment.MaxFileSizeMB = input.MaxFileSizeMB <= 0 ? 20 : input.MaxFileSizeMB;
        assignment.MaxFileCount = input.MaxFileCount <= 0 ? 5 : input.MaxFileCount;
        assignment.AllowResubmit = input.AllowResubmit;
        assignment.ChapterId = input.ChapterId;

        await _assignmentRepository.UpdateAsync(assignment);
        await SaveCriteriaAsync(assignment, input.Criteria);
        await AttachRequirementFilesAsync(assignment, input.AttachmentFileIds);
        await SaveNowAsync();

        return await MapDetailAsync(assignment);
    }

    [Authorize(KnowledgeHubPermissions.Assignments.Manage)]
    public async Task<AssignmentDetailDto> PublishAsync(Guid id)
    {
        var assignment = await _assignmentRepository.GetAsync(id);
        if (assignment.Status == AssignmentStatus.Published)
        {
            return await MapDetailAsync(assignment);
        }

        assignment.Publish(Clock.Now);
        await _assignmentRepository.UpdateAsync(assignment);
        await SaveNowAsync();

        return await MapDetailAsync(assignment);
    }

    [Authorize(KnowledgeHubPermissions.Assignments.Manage)]
    public async Task<AssignmentDetailDto> CloseAsync(Guid id)
    {
        var assignment = await _assignmentRepository.GetAsync(id);
        if (assignment.Status == AssignmentStatus.Closed)
        {
            return await MapDetailAsync(assignment);
        }

        assignment.Close(Clock.Now);
        await _assignmentRepository.UpdateAsync(assignment);
        await SaveNowAsync();

        return await MapDetailAsync(assignment);
    }

    [Authorize(KnowledgeHubPermissions.Assignments.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        var assignment = await _assignmentRepository.GetAsync(id);

        // 清理物理文件
        var files = await AsyncExecuter.ToListAsync(
            (await _fileRepository.GetQueryableAsync()).Where(x => x.AssignmentId == id));
        foreach (var file in files)
        {
            try
            {
                await _fileStorage.DeleteAsync(file.FilePath);
            }
            catch
            {
                // 物理文件删除失败不阻断业务删除
            }
        }

        var submissions = await AsyncExecuter.ToListAsync(
            (await _submissionRepository.GetQueryableAsync()).Where(x => x.AssignmentId == id));
        foreach (var submission in submissions)
        {
            await _submissionRepository.DeleteAsync(submission);
        }

        var criteria = await AsyncExecuter.ToListAsync(
            (await _criterionRepository.GetQueryableAsync()).Where(x => x.AssignmentId == id));
        foreach (var item in criteria)
        {
            await _criterionRepository.DeleteAsync(item);
        }

        await _assignmentRepository.DeleteAsync(assignment);
    }

    [Authorize(KnowledgeHubPermissions.Assignments.Manage)]
    public async Task<string> GetExportScoresAsync(Guid assignmentId)
    {
        var assignment = await _assignmentRepository.GetAsync(assignmentId);
        var submissions = await AsyncExecuter.ToListAsync(
            (await _submissionRepository.GetQueryableAsync()).Where(x => x.AssignmentId == assignmentId));

        var studentNames = await ResolveUserNamesAsync(submissions.Select(x => x.StudentId).ToList());

        var builder = new StringBuilder();
        builder.AppendLine("学号,姓名,状态,提交时间,是否迟交,版本,得分,满分,教师评语,批阅时间");
        foreach (var submission in submissions.OrderBy(x => studentNames.TryGetValue(x.StudentId, out var n) ? n : string.Empty))
        {
            var name = studentNames.TryGetValue(submission.StudentId, out var n) ? n : submission.StudentName;
            builder.AppendLine(string.Join(",",
                Csv(submission.StudentNumber ?? string.Empty),
                Csv(name),
                Csv(StatusLabel(submission.Status)),
                Csv(submission.SubmittedAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty),
                Csv(submission.IsLate ? "是" : "否"),
                submission.Version.ToString(CultureInfo.InvariantCulture),
                (submission.Score ?? 0m).ToString(CultureInfo.InvariantCulture),
                assignment.MaxScore.ToString(CultureInfo.InvariantCulture),
                Csv(submission.Comment ?? string.Empty),
                Csv(submission.ReviewedAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty)));
        }

        // 带 BOM，避免 Excel 打开中文列乱码
        return "\uFEFF" + builder.ToString();
    }

    // ══════════════════════════════════════════════════════════
    //  教师端：批阅
    // ══════════════════════════════════════════════════════════

    [Authorize(KnowledgeHubPermissions.Assignments.Grade)]
    public async Task<PagedResultDto<AssignmentSubmissionItemDto>> GetSubmissionsAsync(AssignmentSubmissionQueryDto input)
    {
        var assignment = await _assignmentRepository.GetAsync(input.AssignmentId);

        var enrolled = await GetEnrolledStudentsAsync(assignment.CourseId);
        var submissions = await AsyncExecuter.ToListAsync(
            (await _submissionRepository.GetQueryableAsync()).Where(x => x.AssignmentId == assignment.Id));

        var submissionMap = submissions
            .GroupBy(x => x.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Version).First());

        var fileCounts = await GetFileCountsAsync(submissions.Select(x => x.Id).ToList());

        var userNames = await ResolveUserNamesAsync(enrolled.Select(x => x.StudentId)
            .Concat(submissions.Select(x => x.StudentId)).Distinct().ToList());

        var items = new List<AssignmentSubmissionItemDto>();
        foreach (var student in enrolled)
        {
            submissionMap.TryGetValue(student.StudentId, out var submission);
            var name = userNames.TryGetValue(student.StudentId, out var n) ? n : student.StudentName;

            var item = new AssignmentSubmissionItemDto
            {
                SubmissionId = submission?.Id ?? Guid.Empty,
                AssignmentId = assignment.Id,
                AssignmentTitle = assignment.Title,
                StudentId = student.StudentId,
                StudentName = name,
                StudentNumber = student.StudentNumber,
                Status = submission?.Status ?? SubmissionStatus.Draft,
                Submitted = submission != null && submission.Status != SubmissionStatus.Draft,
                IsLate = submission?.IsLate ?? false,
                Score = submission?.Score,
                MaxScore = assignment.MaxScore,
                SubmittedAt = submission?.SubmittedAt,
                ReviewedAt = submission?.ReviewedAt,
                Version = submission?.Version ?? 0,
                FileCount = submission != null && fileCounts.TryGetValue(submission.Id, out var fc) ? fc : 0
            };

            if (input.Status.HasValue)
            {
                if (input.Status.Value == SubmissionStatus.Draft && item.Submitted)
                {
                    continue;
                }
                if (input.Status.Value != SubmissionStatus.Draft && item.Status != input.Status.Value)
                {
                    continue;
                }
            }

            if (input.OnlyNotSubmitted == true && item.Submitted)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(input.Filter))
            {
                var kw = input.Filter.Trim();
                if (!item.StudentName.Contains(kw, StringComparison.OrdinalIgnoreCase) &&
                    !(item.StudentNumber ?? string.Empty).Contains(kw, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            items.Add(item);
        }

        // 未选课时，把「有提交但不在选课名单里」的学生也带上（防止选课后台数据遗漏）
        foreach (var submission in submissions)
        {
            if (enrolled.Any(x => x.StudentId == submission.StudentId))
            {
                continue;
            }

            var name = userNames.TryGetValue(submission.StudentId, out var n) ? n : submission.StudentName;
            items.Add(new AssignmentSubmissionItemDto
            {
                SubmissionId = submission.Id,
                AssignmentId = assignment.Id,
                AssignmentTitle = assignment.Title,
                StudentId = submission.StudentId,
                StudentName = name,
                StudentNumber = submission.StudentNumber,
                Status = submission.Status,
                Submitted = submission.Status != SubmissionStatus.Draft,
                IsLate = submission.IsLate,
                Score = submission.Score,
                MaxScore = assignment.MaxScore,
                SubmittedAt = submission.SubmittedAt,
                ReviewedAt = submission.ReviewedAt,
                Version = submission.Version,
                FileCount = fileCounts.TryGetValue(submission.Id, out var fc2) ? fc2 : 0
            });
        }

        var totalCount = items.Count;
        var paged = items
            .OrderByDescending(x => x.Submitted)
            .ThenBy(x => x.StudentName)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        return new PagedResultDto<AssignmentSubmissionItemDto>(totalCount, paged);
    }

    [Authorize(KnowledgeHubPermissions.Assignments.Grade)]
    public async Task<AssignmentSubmissionDetailDto> GetSubmissionAsync(Guid submissionId)
    {
        var submission = await _submissionRepository.GetAsync(submissionId);
        return await MapSubmissionDetailAsync(submission);
    }

    [Authorize(KnowledgeHubPermissions.Assignments.Grade)]
    public async Task<AssignmentSubmissionDetailDto> GradeAsync(Guid submissionId, GradeAssignmentSubmissionDto input)
    {
        var submission = await _submissionRepository.GetAsync(submissionId);
        var assignment = await _assignmentRepository.GetAsync(submission.AssignmentId);

        var criteria = await AsyncExecuter.ToListAsync(
            (await _criterionRepository.GetQueryableAsync())
                .Where(x => x.AssignmentId == assignment.Id)
                .OrderBy(x => x.SortOrder));

        // 得分明细：以教师提交的明细为准；未提交的细则项按 0 分保留
        var scoreInputs = input.Scores ?? new List<SubmissionScoreInputDto>();
        var oldScores = await AsyncExecuter.ToListAsync(
            (await _scoreRepository.GetQueryableAsync()).Where(x => x.SubmissionId == submission.Id));

        foreach (var old in oldScores)
        {
            await _scoreRepository.DeleteAsync(old);
        }

        decimal detailSum = 0m;
        var index = 0;
        foreach (var criterion in criteria)
        {
            var match = scoreInputs.FirstOrDefault(x =>
                (x.CriterionId.HasValue && x.CriterionId.Value == criterion.Id) ||
                (string.IsNullOrWhiteSpace(x.CriterionName) == false && x.CriterionName == criterion.Name));

            var score = match?.Score ?? 0m;
            if (score < 0m) score = 0m;
            if (score > criterion.MaxScore) score = criterion.MaxScore;
            detailSum += score;

            await _scoreRepository.InsertAsync(new SubmissionScore(
                GuidGenerator.Create(),
                submission.Id,
                criterion.Id,
                criterion.Name,
                criterion.MaxScore,
                score,
                criterion.SortOrder,
                match?.Comment));

            index++;
        }

        // 手工输入的额外明细（教师临时新增的评分项）
        foreach (var extra in scoreInputs.Where(x => !criteria.Any(c =>
            (x.CriterionId.HasValue && x.CriterionId.Value == c.Id) || x.CriterionName == c.Name)))
        {
            var score = extra.Score ?? 0m;
            if (score < 0m) score = 0m;
            detailSum += score;
            await _scoreRepository.InsertAsync(new SubmissionScore(
                GuidGenerator.Create(),
                submission.Id,
                extra.CriterionId,
                extra.CriterionName ?? "其他",
                score,
                score,
                extra.SortOrder,
                extra.Comment));
        }

        var total = input.Score ?? (criteria.Count > 0 ? detailSum : detailSum);
        if (total < 0m) total = 0m;
        if (total > assignment.MaxScore) total = assignment.MaxScore;

        // 迟交扣分：教师勾选后按作业配置的百分比自动扣
        if (input.ApplyLatePenalty && submission.IsLate && assignment.LatePenaltyPercent > 0)
        {
            total = Math.Round(total * (1m - assignment.LatePenaltyPercent / 100m), 1);
        }

        submission.Grade(total, input.Comment, CurrentUserId, CurrentUser.Name ?? CurrentUser.UserName, Clock.Now);
        submission.AllowResubmit = input.AllowResubmit;

        await _submissionRepository.UpdateAsync(submission);
        await SaveNowAsync();

        return await MapSubmissionDetailAsync(submission);
    }

    // ══════════════════════════════════════════════════════════
    //  文件
    // ══════════════════════════════════════════════════════════

    public async Task<AssignmentFileDto> UploadFileAsync(Guid assignmentId, IFormFile file, string? purpose = null)
    {
        if (file == null || file.Length <= 0)
        {
            throw new UserFriendlyException("请选择要上传的文件");
        }

        var assignment = await _assignmentRepository.GetAsync(assignmentId);

        var type = ResolveFilePurpose(purpose, assignment);
        var maxBytes = (long)assignment.MaxFileSizeMB * 1024L * 1024L;
        if (file.Length > maxBytes)
        {
            throw new UserFriendlyException($"文件大小超过限制（单文件 ≤ {assignment.MaxFileSizeMB} MB）");
        }

        var extension = Path.GetExtension(file.FileName)?.TrimStart('.').ToLowerInvariant() ?? string.Empty;
        var allowed = ParseFileTypes(assignment.AllowedFileTypes);
        if (allowed.Count > 0 && !allowed.Contains(extension))
        {
            throw new UserFriendlyException($"不支持的文件格式「.{extension}」，允许的格式：{assignment.AllowedFileTypes}");
        }

        var directory = $"assignments/{assignment.Id.ToString("N")}";
        var storedName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
        string path;
        await using (var stream = file.OpenReadStream())
        {
            path = await _fileStorage.SaveAsync(stream, storedName, directory);
        }

        var entity = new AssignmentFile(
            GuidGenerator.Create(),
            assignment.Id,
            type,
            file.FileName,
            path,
            file.Length,
            file.ContentType,
            type == AssignmentFileType.Submission ? await NextSubmissionVersionAsync(assignment.Id, CurrentUserId) : 0);

        await _fileRepository.InsertAsync(entity);
        await SaveNowAsync();

        return MapFile(entity);
    }

    public async Task<byte[]> GetFileAsync(Guid fileId)
    {
        var file = await _fileRepository.GetAsync(fileId);

        // 学生只能下载自己作业里的文件（要求附件 / 自己的提交件 / 批注版）
        if (!await IsTeacherOfCourseAsync(file.AssignmentId))
        {
            var studentId = CurrentUserId;
            var isRequirement = file.Type == AssignmentFileType.Requirement;
            var isOwnSubmission = await AsyncExecuter.AnyAsync(
                (await _fileRepository.GetQueryableAsync())
                    .Where(x => x.Id == file.Id && x.SubmissionId != null));

            if (!isRequirement && !isOwnSubmission)
            {
                throw new UserFriendlyException("无权下载该文件");
            }

            var isOwner = await AsyncExecuter.AnyAsync(
                (await _submissionRepository.GetQueryableAsync())
                    .Where(x => x.AssignmentId == file.AssignmentId && x.StudentId == studentId));

            if (!isOwner)
            {
                throw new UserFriendlyException("无权下载该文件");
            }
        }

        var stream = await _fileStorage.GetAsync(file.FilePath);
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    // ══════════════════════════════════════════════════════════
    //  学生端
    // ══════════════════════════════════════════════════════════

    public async Task<List<StudentAssignmentItemDto>> GetMyAssignmentsAsync(Guid courseId)
    {
        var studentId = CurrentUserId;
        await EnsureEnrolledAsync(courseId, studentId);

        var assignments = await AsyncExecuter.ToListAsync(
            (await _assignmentRepository.GetQueryableAsync())
                .Where(x => x.CourseId == courseId && x.Status != AssignmentStatus.Draft)
                .OrderByDescending(x => x.Deadline));

        var assignmentIds = assignments.Select(x => x.Id).ToList();
        var submissions = assignmentIds.Count == 0
            ? new List<AssignmentSubmission>()
            : await AsyncExecuter.ToListAsync(
                (await _submissionRepository.GetQueryableAsync())
                    .Where(x => assignmentIds.Contains(x.AssignmentId) && x.StudentId == studentId));

        var submissionMap = submissions
            .GroupBy(x => x.AssignmentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Version).First());

        var now = Clock.Now;
        var result = new List<StudentAssignmentItemDto>();
        foreach (var assignment in assignments)
        {
            submissionMap.TryGetValue(assignment.Id, out var submission);
            result.Add(new StudentAssignmentItemDto
            {
                AssignmentId = assignment.Id,
                Title = assignment.Title,
                Type = assignment.Type,
                MaxScore = assignment.MaxScore,
                Deadline = assignment.Deadline,
                Status = submission?.Status ?? SubmissionStatus.Draft,
                Submitted = submission != null && submission.Status != SubmissionStatus.Draft,
                IsLate = submission?.IsLate ?? false,
                Score = submission?.Score,
                SubmittedAt = submission?.SubmittedAt,
                HasFeedback = submission?.Status == SubmissionStatus.Graded,
                RemainingDays = RemainingDays(assignment.Deadline, now)
            });
        }

        return result;
    }

    public async Task<StudentAssignmentDetailDto> GetMyAssignmentAsync(Guid assignmentId)
    {
        var studentId = CurrentUserId;
        var assignment = await _assignmentRepository.GetAsync(assignmentId);

        if (assignment.Status == AssignmentStatus.Draft)
        {
            throw new UserFriendlyException("作业不存在或未发布");
        }

        await EnsureEnrolledAsync(assignment.CourseId, studentId);

        var submission = await GetMySubmissionAsync(assignmentId, studentId);
        var criteria = await AsyncExecuter.ToListAsync(
            (await _criterionRepository.GetQueryableAsync())
                .Where(x => x.AssignmentId == assignmentId)
                .OrderBy(x => x.SortOrder));

        var attachments = await AsyncExecuter.ToListAsync(
            (await _fileRepository.GetQueryableAsync())
                .Where(x => x.AssignmentId == assignmentId && x.Type == AssignmentFileType.Requirement)
                .OrderBy(x => x.SortOrder));

        var myFiles = submission == null
            ? new List<AssignmentFile>()
            : await AsyncExecuter.ToListAsync(
                (await _fileRepository.GetQueryableAsync())
                    .Where(x => x.SubmissionId == submission.Id)
                    .OrderBy(x => x.Version)
                    .ThenBy(x => x.SortOrder));

        var teacherName = await ResolveUserNamesAsync(new List<Guid> { assignment.TeacherId });

        return new StudentAssignmentDetailDto
        {
            AssignmentId = assignment.Id,
            CourseId = assignment.CourseId,
            CourseTitle = await ResolveCourseTitleAsync(assignment.CourseId),
            Title = assignment.Title,
            Description = assignment.Description,
            Type = assignment.Type,
            MaxScore = assignment.MaxScore,
            Deadline = assignment.Deadline,
            AllowLate = assignment.AllowLate,
            LatePenaltyPercent = assignment.LatePenaltyPercent,
            AllowedFileTypes = assignment.AllowedFileTypes,
            MaxFileSizeMB = assignment.MaxFileSizeMB,
            MaxFileCount = assignment.MaxFileCount,
            AllowResubmit = assignment.AllowResubmit,
            TeacherName = teacherName.TryGetValue(assignment.TeacherId, out var tn) ? tn : null,
            RemainingDays = RemainingDays(assignment.Deadline, Clock.Now),
            Criteria = criteria.Select(x => new AssignmentCriterionDto
            {
                Id = x.Id,
                Name = x.Name,
                MaxScore = x.MaxScore,
                SortOrder = x.SortOrder,
                Description = x.Description
            }).ToList(),
            Attachments = attachments.Select(MapFile).ToList(),
            SubmissionId = submission?.Id,
            MyStatus = submission?.Status ?? SubmissionStatus.Draft,
            Submitted = submission != null && submission.Status != SubmissionStatus.Draft,
            IsLate = submission?.IsLate ?? false,
            MyNote = submission?.Note,
            HonorPledge = submission?.HonorPledge ?? false,
            SubmittedAt = submission?.SubmittedAt,
            Version = submission?.Version ?? 0,
            MyFiles = myFiles.Select(MapFile).ToList(),
            CanSubmit = CanSubmit(assignment, submission)
        };
    }

    public async Task<StudentAssignmentDetailDto> SubmitAsync(StudentSubmitAssignmentDto input)
    {
        var studentId = CurrentUserId;
        var assignment = await _assignmentRepository.GetAsync(input.AssignmentId);

        if (assignment.Status == AssignmentStatus.Draft)
        {
            throw new UserFriendlyException("作业不存在或未发布");
        }

        await EnsureEnrolledAsync(assignment.CourseId, studentId);

        var submission = await GetMySubmissionAsync(assignment.Id, studentId);
        if (submission == null)
        {
            var studentName = await ResolveUserNamesAsync(new List<Guid> { studentId });
            submission = new AssignmentSubmission(GuidGenerator.Create(), assignment.Id, studentId,
                studentName.TryGetValue(studentId, out var sn) ? sn : (CurrentUser.Name ?? CurrentUser.UserName ?? string.Empty))
            {
                TenantId = CurrentTenant.Id
            };
            await _submissionRepository.InsertAsync(submission);
        }

        // 关联本次提交的文件
        var fileIds = (input.FileIds ?? new List<Guid>()).Distinct().ToList();
        if (fileIds.Count > 0)
        {
            var files = await AsyncExecuter.ToListAsync(
                (await _fileRepository.GetQueryableAsync())
                    .Where(x => fileIds.Contains(x.Id) && x.AssignmentId == assignment.Id && x.Type == AssignmentFileType.Submission));

            foreach (var file in files)
            {
                file.SubmissionId = submission.Id;
                await _fileRepository.UpdateAsync(file);
            }

            // 先落盘：下面的文件数统计走 SQL，不落盘会数不到刚关联的文件
            await SaveNowAsync();
        }

        submission.Note = input.Note?.Trim();
        submission.HonorPledge = input.HonorPledge;

        if (!input.IsDraft)
        {
            if (!CanSubmit(assignment, submission))
            {
                throw new UserFriendlyException(assignment.Deadline < Clock.Now
                    ? "作业已截止，无法提交"
                    : "当前不允许提交（教师已关闭重新提交）");
            }

            if (!input.HonorPledge)
            {
                throw new UserFriendlyException("请勾选学术诚信承诺后再提交");
            }

            var fileCount = await AsyncExecuter.CountAsync(
                (await _fileRepository.GetQueryableAsync())
                    .Where(x => x.AssignmentId == assignment.Id && x.SubmissionId == submission.Id));

            if (fileCount == 0)
            {
                throw new UserFriendlyException("请至少上传一个文件");
            }

            var now = Clock.Now;
            var isLate = now > assignment.Deadline;
            submission.Submit(now, isLate);
        }
        else
        {
            submission.Status = SubmissionStatus.Draft;
        }

        await _submissionRepository.UpdateAsync(submission);
        await SaveNowAsync();

        return await GetMyAssignmentAsync(assignment.Id);
    }

    public async Task<StudentAssignmentFeedbackDto> GetMyFeedbackAsync(Guid assignmentId)
    {
        var studentId = CurrentUserId;
        var assignment = await _assignmentRepository.GetAsync(assignmentId);
        await EnsureEnrolledAsync(assignment.CourseId, studentId);

        var submission = await GetMySubmissionAsync(assignmentId, studentId);
        if (submission == null)
        {
            throw new UserFriendlyException("还没有提交记录");
        }

        var scores = await AsyncExecuter.ToListAsync(
            (await _scoreRepository.GetQueryableAsync())
                .Where(x => x.SubmissionId == submission.Id)
                .OrderBy(x => x.SortOrder));

        var files = await AsyncExecuter.ToListAsync(
            (await _fileRepository.GetQueryableAsync())
                .Where(x => x.SubmissionId == submission.Id)
                .OrderBy(x => x.Version)
                .ThenBy(x => x.SortOrder));

        var annotated = await AsyncExecuter.ToListAsync(
            (await _fileRepository.GetQueryableAsync())
                .Where(x => x.AssignmentId == assignmentId && x.Type == AssignmentFileType.Annotated && x.SubmissionId == submission.Id));

        // 班级平均分：同作业所有已批阅提交的平均分
        var graded = await AsyncExecuter.ToListAsync(
            (await _submissionRepository.GetQueryableAsync())
                .Where(x => x.AssignmentId == assignmentId && x.Status == SubmissionStatus.Graded && x.Score != null));

        var classAverage = graded.Count == 0
            ? 0m
            : Math.Round(graded.Average(x => x.Score!.Value), 1);

        var timeline = new List<StudentSubmissionTimelineDto>();
        if (submission.ReviewedAt.HasValue)
        {
            timeline.Add(new StudentSubmissionTimelineDto
            {
                Label = "批阅完成",
                Time = submission.ReviewedAt.Value,
                Extra = submission.ReviewerName
            });
        }

        foreach (var group in files.GroupBy(x => x.Version).OrderByDescending(x => x.Key))
        {
            timeline.Add(new StudentSubmissionTimelineDto
            {
                Label = group.Key >= submission.Version && group.Key == submission.Version
                    ? $"提交第 {group.Key} 版（当前版）"
                    : $"提交第 {group.Key} 版",
                Time = group.Min(x => x.CreationTime)
            });
        }

        return new StudentAssignmentFeedbackDto
        {
            AssignmentId = assignmentId,
            Title = assignment.Title,
            MaxScore = assignment.MaxScore,
            Score = submission.Score,
            ClassAverage = classAverage,
            Graded = submission.Status == SubmissionStatus.Graded,
            Comment = submission.Comment,
            ReviewerName = submission.ReviewerName,
            SubmittedAt = submission.SubmittedAt,
            ReviewedAt = submission.ReviewedAt,
            IsLate = submission.IsLate,
            Scores = scores.Select(x => new SubmissionScoreDto
            {
                Id = x.Id,
                CriterionId = x.CriterionId,
                CriterionName = x.CriterionName,
                MaxScore = x.MaxScore,
                Score = x.Score,
                Comment = x.Comment,
                SortOrder = x.SortOrder
            }).ToList(),
            Files = files.Select(MapFile).ToList(),
            AnnotatedFiles = annotated.Select(MapFile).ToList(),
            Timeline = timeline
        };
    }

    // ══════════════════════════════════════════════════════════
    //  内部方法
    // ══════════════════════════════════════════════════════════

    private void ValidateInput(CreateUpdateAssignmentDto input)
    {
        if (input.CourseId == Guid.Empty)
        {
            throw new UserFriendlyException("请先选择课程");
        }

        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("请填写作业标题");
        }

        if (input.Deadline == default)
        {
            throw new UserFriendlyException("请设置截止时间");
        }

        if (input.MaxScore <= 0)
        {
            throw new UserFriendlyException("满分必须大于 0");
        }

        if (input.LatePenaltyPercent < 0 || input.LatePenaltyPercent > 100)
        {
            throw new UserFriendlyException("迟交扣分百分比应在 0~100 之间");
        }

        if ((input.Criteria?.Count ?? 0) > 0)
        {
            var sum = input.Criteria.Sum(x => x.MaxScore);
            if (sum <= 0)
            {
                throw new UserFriendlyException("评分细则的满分合计必须大于 0");
            }

            input.MaxScore = sum;
        }
    }

    private async Task SaveCriteriaAsync(CourseAssignment assignment, List<AssignmentCriterionDto>? criteria)
    {
        var existing = await AsyncExecuter.ToListAsync(
            (await _criterionRepository.GetQueryableAsync()).Where(x => x.AssignmentId == assignment.Id));

        var incoming = criteria ?? new List<AssignmentCriterionDto>();

        foreach (var item in existing)
        {
            await _criterionRepository.DeleteAsync(item);
        }

        var sort = 0;
        foreach (var item in incoming.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
        {
            await _criterionRepository.InsertAsync(new AssignmentCriterion(
                GuidGenerator.Create(),
                assignment.Id,
                item.Name.Trim(),
                item.MaxScore,
                item.SortOrder > 0 ? item.SortOrder : sort));
            sort++;
        }
    }

    private async Task AttachRequirementFilesAsync(CourseAssignment assignment, List<Guid>? attachmentFileIds)
    {
        var ids = (attachmentFileIds ?? new List<Guid>()).Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var files = await AsyncExecuter.ToListAsync(
            (await _fileRepository.GetQueryableAsync())
                .Where(x => ids.Contains(x.Id) && x.AssignmentId == assignment.Id && x.Type == AssignmentFileType.Requirement));

        var sort = 0;
        foreach (var file in files)
        {
            file.SortOrder = sort++;
            await _fileRepository.UpdateAsync(file);
        }
    }

    private async Task<AssignmentListItemDto> MapListItemAsync(CourseAssignment assignment)
    {
        var submissions = await AsyncExecuter.ToListAsync(
            (await _submissionRepository.GetQueryableAsync()).Where(x => x.AssignmentId == assignment.Id));

        var totalStudents = await CountEnrolledStudentsAsync(assignment.CourseId);
        var submitted = submissions.Count(x => x.Status != SubmissionStatus.Draft);
        var graded = submissions.Count(x => x.Status == SubmissionStatus.Graded);
        var pending = submissions.Count(x => x.Status == SubmissionStatus.Submitted);
        var gradedScores = submissions.Where(x => x.Status == SubmissionStatus.Graded && x.Score.HasValue).ToList();

        return new AssignmentListItemDto
        {
            Id = assignment.Id,
            CourseId = assignment.CourseId,
            CourseTitle = await ResolveCourseTitleAsync(assignment.CourseId),
            Title = assignment.Title,
            Description = assignment.Description,
            Type = assignment.Type,
            Status = assignment.Status,
            MaxScore = assignment.MaxScore,
            Deadline = assignment.Deadline,
            AllowLate = assignment.AllowLate,
            LatePenaltyPercent = assignment.LatePenaltyPercent,
            TotalStudents = totalStudents,
            SubmittedCount = submitted,
            GradedCount = graded,
            PendingGradeCount = pending,
            NotSubmittedCount = Math.Max(0, totalStudents - submitted),
            LateCount = submissions.Count(x => x.IsLate),
            AverageScore = gradedScores.Count == 0 ? 0m : Math.Round(gradedScores.Average(x => x.Score!.Value), 1),
            CreationTime = assignment.CreationTime,
            PublishTime = assignment.PublishTime
        };
    }

    private async Task<AssignmentDetailDto> MapDetailAsync(CourseAssignment assignment)
    {
        var criteria = await AsyncExecuter.ToListAsync(
            (await _criterionRepository.GetQueryableAsync())
                .Where(x => x.AssignmentId == assignment.Id)
                .OrderBy(x => x.SortOrder));

        var attachments = await AsyncExecuter.ToListAsync(
            (await _fileRepository.GetQueryableAsync())
                .Where(x => x.AssignmentId == assignment.Id && x.Type == AssignmentFileType.Requirement)
                .OrderBy(x => x.SortOrder));

        var submissions = await AsyncExecuter.ToListAsync(
            (await _submissionRepository.GetQueryableAsync()).Where(x => x.AssignmentId == assignment.Id));

        var totalStudents = await CountEnrolledStudentsAsync(assignment.CourseId);
        var submitted = submissions.Count(x => x.Status != SubmissionStatus.Draft);
        var graded = submissions.Count(x => x.Status == SubmissionStatus.Graded);
        var pending = submissions.Count(x => x.Status == SubmissionStatus.Submitted);
        var gradedScores = submissions.Where(x => x.Status == SubmissionStatus.Graded && x.Score.HasValue).ToList();
        var teacherNames = await ResolveUserNamesAsync(new List<Guid> { assignment.TeacherId });

        return new AssignmentDetailDto
        {
            Id = assignment.Id,
            CourseId = assignment.CourseId,
            CourseTitle = await ResolveCourseTitleAsync(assignment.CourseId),
            ChapterId = assignment.ChapterId,
            Title = assignment.Title,
            Description = assignment.Description,
            Type = assignment.Type,
            Status = assignment.Status,
            MaxScore = assignment.MaxScore,
            Deadline = assignment.Deadline,
            AllowLate = assignment.AllowLate,
            LatePenaltyPercent = assignment.LatePenaltyPercent,
            AllowedFileTypes = assignment.AllowedFileTypes,
            MaxFileSizeMB = assignment.MaxFileSizeMB,
            MaxFileCount = assignment.MaxFileCount,
            AllowResubmit = assignment.AllowResubmit,
            TeacherId = assignment.TeacherId,
            TeacherName = teacherNames.TryGetValue(assignment.TeacherId, out var tn) ? tn : null,
            CreationTime = assignment.CreationTime,
            PublishTime = assignment.PublishTime,
            ClosedTime = assignment.ClosedTime,
            Criteria = criteria.Select(x => new AssignmentCriterionDto
            {
                Id = x.Id,
                Name = x.Name,
                MaxScore = x.MaxScore,
                SortOrder = x.SortOrder,
                Description = x.Description
            }).ToList(),
            Attachments = attachments.Select(MapFile).ToList(),
            TotalStudents = totalStudents,
            SubmittedCount = submitted,
            GradedCount = graded,
            PendingGradeCount = pending,
            NotSubmittedCount = Math.Max(0, totalStudents - submitted),
            LateCount = submissions.Count(x => x.IsLate),
            AverageScore = gradedScores.Count == 0 ? 0m : Math.Round(gradedScores.Average(x => x.Score!.Value), 1)
        };
    }

    private async Task<AssignmentSubmissionDetailDto> MapSubmissionDetailAsync(AssignmentSubmission submission)
    {
        var assignment = await _assignmentRepository.GetAsync(submission.AssignmentId);

        var criteria = await AsyncExecuter.ToListAsync(
            (await _criterionRepository.GetQueryableAsync())
                .Where(x => x.AssignmentId == assignment.Id)
                .OrderBy(x => x.SortOrder));

        var scores = await AsyncExecuter.ToListAsync(
            (await _scoreRepository.GetQueryableAsync())
                .Where(x => x.SubmissionId == submission.Id)
                .OrderBy(x => x.SortOrder));

        var files = await AsyncExecuter.ToListAsync(
            (await _fileRepository.GetQueryableAsync())
                .Where(x => x.SubmissionId == submission.Id)
                .OrderBy(x => x.Version)
                .ThenBy(x => x.SortOrder));

        var annotated = await AsyncExecuter.ToListAsync(
            (await _fileRepository.GetQueryableAsync())
                .Where(x => x.SubmissionId == submission.Id && x.Type == AssignmentFileType.Annotated));

        return new AssignmentSubmissionDetailDto
        {
            SubmissionId = submission.Id,
            AssignmentId = assignment.Id,
            AssignmentTitle = assignment.Title,
            StudentId = submission.StudentId,
            StudentName = submission.StudentName,
            StudentNumber = submission.StudentNumber,
            Status = submission.Status,
            MaxScore = assignment.MaxScore,
            Score = submission.Score,
            Note = submission.Note,
            HonorPledge = submission.HonorPledge,
            IsLate = submission.IsLate,
            SubmittedAt = submission.SubmittedAt,
            ReviewedAt = submission.ReviewedAt,
            ReviewerName = submission.ReviewerName,
            Comment = submission.Comment,
            AllowResubmit = submission.AllowResubmit,
            Version = submission.Version,
            Files = files.Select(MapFile).ToList(),
            AnnotatedFiles = annotated.Select(MapFile).ToList(),
            Scores = scores.Select(x => new SubmissionScoreDto
            {
                Id = x.Id,
                CriterionId = x.CriterionId,
                CriterionName = x.CriterionName,
                MaxScore = x.MaxScore,
                Score = x.Score,
                Comment = x.Comment,
                SortOrder = x.SortOrder
            }).ToList(),
            Criteria = criteria.Select(x => new AssignmentCriterionDto
            {
                Id = x.Id,
                Name = x.Name,
                MaxScore = x.MaxScore,
                SortOrder = x.SortOrder,
                Description = x.Description
            }).ToList()
        };
    }

    private static AssignmentFileDto MapFile(AssignmentFile file) => new()
    {
        Id = file.Id,
        FileName = file.FileName,
        FileSize = file.FileSize,
        ContentType = file.ContentType,
        Type = file.Type,
        Version = file.Version,
        SortOrder = file.SortOrder,
        CreationTime = file.CreationTime
    };

    private async Task<AssignmentSubmission?> GetMySubmissionAsync(Guid assignmentId, Guid studentId)
    {
        var submissions = await AsyncExecuter.ToListAsync(
            (await _submissionRepository.GetQueryableAsync())
                .Where(x => x.AssignmentId == assignmentId && x.StudentId == studentId));

        return submissions.OrderByDescending(x => x.Version).FirstOrDefault();
    }

    private async Task<int> NextSubmissionVersionAsync(Guid assignmentId, Guid studentId)
    {
        var submission = await GetMySubmissionAsync(assignmentId, studentId);
        return (submission?.Version ?? 0) + 1;
    }

    private async Task<Dictionary<Guid, int>> GetFileCountsAsync(List<Guid> submissionIds)
    {
        var counts = new Dictionary<Guid, int>();
        if (submissionIds.Count == 0)
        {
            return counts;
        }

        var files = await AsyncExecuter.ToListAsync(
            (await _fileRepository.GetQueryableAsync())
                .Where(x => x.SubmissionId != null && submissionIds.Contains(x.SubmissionId.Value)));

        foreach (var group in files.GroupBy(x => x.SubmissionId!.Value))
        {
            counts[group.Key] = group.Count();
        }

        return counts;
    }

    private async Task<int> CountEnrolledStudentsAsync(Guid courseId)
    {
        if (courseId == Guid.Empty)
        {
            return 0;
        }

        return await AsyncExecuter.CountAsync(
            (await _studentCourseRepository.GetQueryableAsync())
                .Where(x => x.CourseId == courseId && x.Status != StudentCourseStatus.Dropped));
    }

    private async Task<List<EnrolledStudent>> GetEnrolledStudentsAsync(Guid courseId)
    {
        var rows = await AsyncExecuter.ToListAsync(
            (await _studentCourseRepository.GetQueryableAsync())
                .Where(x => x.CourseId == courseId && x.Status != StudentCourseStatus.Dropped)
                .Select(x => new { x.StudentId, x.ClassId }));

        var users = await ResolveUserNamesAsync(rows.Select(x => x.StudentId).ToList());

        return rows.Select(x => new EnrolledStudent
        {
            StudentId = x.StudentId,
            ClassId = x.ClassId,
            StudentName = users.TryGetValue(x.StudentId, out var n) ? n : string.Empty,
            StudentNumber = null
        }).ToList();
    }

    private async Task EnsureEnrolledAsync(Guid courseId, Guid studentId)
    {
        var enrolled = await AsyncExecuter.AnyAsync(
            (await _studentCourseRepository.GetQueryableAsync())
                .Where(x => x.CourseId == courseId &&
                            x.StudentId == studentId &&
                            x.Status != StudentCourseStatus.Dropped));

        if (!enrolled)
        {
            throw new UserFriendlyException("你尚未选修该课程，无法查看或提交作业");
        }
    }

    private async Task<bool> IsTeacherOfCourseAsync(Guid assignmentId)
    {
        var assignment = await _assignmentRepository.GetAsync(assignmentId);
        if (assignment.TeacherId == CurrentUserId)
        {
            return true;
        }

        var course = await _courseRepository.GetAsync(assignment.CourseId);
        return course != null && course.TeacherId == CurrentUserId;
    }

    private async Task<string> ResolveCourseTitleAsync(Guid courseId)
    {
        var course = await _courseRepository.GetAsync(courseId);
        return course?.Title ?? string.Empty;
    }

    private async Task<Dictionary<Guid, string>> ResolveUserNamesAsync(List<Guid> userIds)
    {
        var map = new Dictionary<Guid, string>();
        var ids = userIds.Where(x => x != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return map;
        }

        var users = await AsyncExecuter.ToListAsync(
            (await _userRepository.GetQueryableAsync())
                .Where(x => ids.Contains(x.Id))
                .Select(x => new { x.Id, x.Name, x.UserName }));

        foreach (var user in users)
        {
            map[user.Id] = user.Name ?? user.UserName ?? string.Empty;
        }

        return map;
    }

    private bool CanSubmit(CourseAssignment assignment, AssignmentSubmission? submission)
    {
        if (assignment.Status == AssignmentStatus.Draft || assignment.Status == AssignmentStatus.Closed)
        {
            return false;
        }

        var now = Clock.Now;
        if (now <= assignment.Deadline)
        {
            return true;
        }

        if (!assignment.AllowLate)
        {
            return false;
        }

        // 未提交 / 草稿：迟交也允许（按作业的迟交规则）
        if (submission == null || submission.Status == SubmissionStatus.Draft)
        {
            return true;
        }

        // 已提交未批阅：按作业的「允许重新提交」开关
        if (submission.Status == SubmissionStatus.Submitted)
        {
            return assignment.AllowResubmit;
        }

        // 已批阅：需要教师在批阅时勾选「允许重新提交」
        return assignment.AllowResubmit && submission.AllowResubmit;
    }

    private static int RemainingDays(DateTime deadline, DateTime now)
    {
        var diff = deadline - now;
        if (diff <= TimeSpan.Zero)
        {
            return 0;
        }

        return (int)Math.Ceiling(diff.TotalDays);
    }

    private static string StatusLabel(SubmissionStatus status) => status switch
    {
        SubmissionStatus.Draft => "草稿",
        SubmissionStatus.Submitted => "待批阅",
        SubmissionStatus.Graded => "已批阅",
        _ => ""
    };

    private static string NormalizeFileTypes(string? types)
    {
        if (string.IsNullOrWhiteSpace(types))
        {
            return "pdf,doc,docx,xls,xlsx,ppt,pptx,zip,rar,7z,png,jpg,jpeg,txt,md";
        }

        var list = types.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().TrimStart('.').ToLowerInvariant())
            .Where(x => x.Length > 0)
            .Distinct();

        return string.Join(",", list);
    }

    private static List<string> ParseFileTypes(string? types)
    {
        if (string.IsNullOrWhiteSpace(types))
        {
            return new List<string>();
        }

        return types.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().TrimStart('.').ToLowerInvariant())
            .Where(x => x.Length > 0)
            .ToList();
    }

    private AssignmentFileType ResolveFilePurpose(string? purpose, CourseAssignment assignment)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            return AssignmentFileType.Submission;
        }

        return purpose.ToLowerInvariant() switch
        {
            "requirement" => AssignmentFileType.Requirement,
            "annotated" => AssignmentFileType.Annotated,
            _ => AssignmentFileType.Submission
        };
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n'))
        {
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        return text;
    }

    private class EnrolledStudent
    {
        public Guid StudentId { get; set; }
        public Guid? ClassId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string? StudentNumber { get; set; }
    }
}
