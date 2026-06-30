using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.Courses;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Permissions;
using KnowledgeHub.Practicums.Dtos;
using KnowledgeHub.Practicums.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Practicums;

[IgnoreAntiforgeryToken]
public class PracticumAppService : KnowledgeHubAppService, IPracticumAppService
{
    private readonly IRepository<PracticumProject, Guid> _projectRepository;
    private readonly IRepository<PracticumTask, Guid> _taskRepository;
    private readonly IRepository<PracticumMaterial, Guid> _materialRepository;
    private readonly IRepository<PracticumEnrollment, Guid> _enrollmentRepository;
    private readonly IRepository<PracticumSubmission, Guid> _submissionRepository;
    private readonly IRepository<PracticumGuidanceRecord, Guid> _guidanceRepository;
    private readonly IRepository<PracticumAssessment, Guid> _assessmentRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;

    public PracticumAppService(
        IRepository<PracticumProject, Guid> projectRepository,
        IRepository<PracticumTask, Guid> taskRepository,
        IRepository<PracticumMaterial, Guid> materialRepository,
        IRepository<PracticumEnrollment, Guid> enrollmentRepository,
        IRepository<PracticumSubmission, Guid> submissionRepository,
        IRepository<PracticumGuidanceRecord, Guid> guidanceRepository,
        IRepository<PracticumAssessment, Guid> assessmentRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<IdentityUser, Guid> userRepository,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant)
    {
        _projectRepository = projectRepository;
        _taskRepository = taskRepository;
        _materialRepository = materialRepository;
        _enrollmentRepository = enrollmentRepository;
        _submissionRepository = submissionRepository;
        _guidanceRepository = guidanceRepository;
        _assessmentRepository = assessmentRepository;
        _courseRepository = courseRepository;
        _studentCourseRepository = studentCourseRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
    }

    public async Task<PracticumProjectDto> GetAsync(Guid id)
    {
        var entity = await _projectRepository.GetAsync(id);
        return await MapProjectDtoAsync(entity);
    }

    public async Task<PracticumProjectDetailDto> GetDetailAsync(Guid id)
    {
        var entity = await _projectRepository.GetAsync(id);
        var dto = await MapProjectDetailDtoAsync(entity);
        // 未报名学生只能浏览项目介绍/任务数/资料数等元信息，
        // 不返回任务明细和资料下载链接，避免诱导后 403
        if (dto.IsCurrentUserEnrolled)
        {
            dto.Tasks = await GetTaskDtosAsync(id);
            dto.Materials = await GetMaterialDtosAsync(id);
        }
        return dto;
    }

    [Authorize(KnowledgeHubPermissions.Practicum.Default)]
    public async Task<PagedResultDto<PracticumProjectDto>> GetListAsync(PagedPracticumProjectRequestDto input)
    {
        var query = await _projectRepository.GetQueryableAsync();
        query = query
            .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x =>
                x.Title.Contains(input.Filter!) ||
                (x.Summary != null && x.Summary.Contains(input.Filter!)) ||
                (x.Major != null && x.Major.Contains(input.Filter!)) ||
                (x.ClassName != null && x.ClassName.Contains(input.Filter!)))
            .WhereIf(input.CourseId.HasValue, x => x.CourseId == input.CourseId.Value)
            .WhereIf(input.Status.HasValue, x => x.Status == input.Status.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.CreationTime)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        return new PagedResultDto<PracticumProjectDto>(totalCount, await MapProjectDtosAsync(items));
    }

    public async Task<PagedResultDto<PracticumProjectDto>> GetPublishedAsync(PagedPracticumProjectRequestDto input)
    {
        input.Status = PracticumProjectStatus.Published;
        return await GetListAsync(input);
    }

    [Authorize(KnowledgeHubPermissions.Practicum.Create)]
    public async Task<PracticumProjectDto> CreateAsync(CreateUpdatePracticumProjectDto input)
    {
        await ValidateProjectInputAsync(input);

        var entity = new PracticumProject(GuidGenerator.Create(), input.Title.Trim())
        {
            TenantId = CurrentTenant.Id,
            Summary = input.Summary?.Trim(),
            Description = input.Description?.Trim(),
            CoverImageUrl = input.CoverImageUrl?.Trim(),
            CourseId = input.CourseId,
            Major = input.Major?.Trim(),
            ClassName = input.ClassName?.Trim(),
            Status = input.Status,
            StartTime = input.StartTime,
            EndTime = input.EndTime,
            MaxScore = Math.Clamp(input.MaxScore, 1, 1000),
            AllowResubmission = input.AllowResubmission,
            AgentName = input.AgentName?.Trim(),
            AgentPrompt = input.AgentPrompt?.Trim()
        };

        await _projectRepository.InsertAsync(entity, autoSave: true);
        await ReplaceTasksAsync(entity.Id, input.Tasks);
        await ReplaceMaterialsAsync(entity.Id, input.Materials);
        return await MapProjectDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.Practicum.Edit)]
    public async Task<PracticumProjectDto> UpdateAsync(Guid id, CreateUpdatePracticumProjectDto input)
    {
        await ValidateProjectInputAsync(input);

        var entity = await _projectRepository.GetAsync(id);
        entity.Title = input.Title.Trim();
        entity.Summary = input.Summary?.Trim();
        entity.Description = input.Description?.Trim();
        entity.CoverImageUrl = input.CoverImageUrl?.Trim();
        entity.CourseId = input.CourseId;
        entity.Major = input.Major?.Trim();
        entity.ClassName = input.ClassName?.Trim();
        entity.Status = input.Status;
        entity.StartTime = input.StartTime;
        entity.EndTime = input.EndTime;
        entity.MaxScore = Math.Clamp(input.MaxScore, 1, 1000);
        entity.AllowResubmission = input.AllowResubmission;
        entity.AgentName = input.AgentName?.Trim();
        entity.AgentPrompt = input.AgentPrompt?.Trim();

        await _projectRepository.UpdateAsync(entity, autoSave: true);
        await ReplaceTasksAsync(id, input.Tasks);
        await ReplaceMaterialsAsync(id, input.Materials);
        return await MapProjectDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.Practicum.Edit)]
    public async Task DeleteAsync(Guid id)
    {
        if (await _enrollmentRepository.AnyAsync(x => x.ProjectId == id))
        {
            throw new UserFriendlyException("已有学生参与该实训项目，暂不能删除。");
        }

        var tasks = await _taskRepository.GetListAsync(x => x.ProjectId == id);
        foreach (var task in tasks)
        {
            await _taskRepository.DeleteAsync(task);
        }

        var materials = await _materialRepository.GetListAsync(x => x.ProjectId == id);
        foreach (var material in materials)
        {
            await _materialRepository.DeleteAsync(material);
        }

        await _projectRepository.DeleteAsync(id);
    }

    public async Task EnrollAsync(Guid projectId)
    {
        var studentId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var entity = await _projectRepository.GetAsync(projectId);
        if (entity.Status != PracticumProjectStatus.Published)
        {
            throw new UserFriendlyException("当前实训项目未发布。");
        }

        var existing = await _enrollmentRepository.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.StudentId == studentId);
        if (existing != null && existing.Status != PracticumEnrollmentStatus.Cancelled)
        {
            throw new UserFriendlyException("您已参与该实训项目。");
        }

        if (existing != null)
        {
            existing.Status = PracticumEnrollmentStatus.Enrolled;
            existing.Progress = 0;
            existing.EnrolledAt = DateTime.UtcNow;
            existing.LastSubmittedAt = null;
            existing.FinalScore = null;
            existing.FinalComment = null;
            existing.CompletedAt = null;
            await _enrollmentRepository.UpdateAsync(existing, autoSave: true);
        }
        else
        {
            var enrollment = new PracticumEnrollment(GuidGenerator.Create(), projectId, studentId)
            {
                TenantId = CurrentTenant.Id
            };
            await _enrollmentRepository.InsertAsync(enrollment, autoSave: true);
        }

        await EnsureStudentCourseAsync(entity, studentId);
    }

    public async Task<List<PracticumEnrollmentDto>> GetMyEnrollmentsAsync()
    {
        var studentId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var items = await _enrollmentRepository.GetListAsync(x => x.StudentId == studentId);
        foreach (var item in items)
        {
            await RefreshEnrollmentProgressAsync(item);
        }

        return await MapEnrollmentDtosAsync(items.OrderByDescending(x => x.EnrolledAt).ToList());
    }

    [Authorize(KnowledgeHubPermissions.Practicum.Review)]
    public async Task<PagedResultDto<PracticumEnrollmentDto>> GetEnrollmentListAsync(GetPracticumEnrollmentsInput input)
    {
        var query = await _enrollmentRepository.GetQueryableAsync();
        query = query
            .WhereIf(input.ProjectId.HasValue, x => x.ProjectId == input.ProjectId.Value)
            .WhereIf(input.StudentId.HasValue, x => x.StudentId == input.StudentId.Value)
            .WhereIf(input.Status.HasValue, x => x.Status == input.Status.Value);

        var items = await query
            .OrderByDescending(x => x.EnrolledAt)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        foreach (var item in items)
        {
            await RefreshEnrollmentProgressAsync(item);
        }

        var totalCount = await query.CountAsync();
        return new PagedResultDto<PracticumEnrollmentDto>(totalCount, await MapEnrollmentDtosAsync(items));
    }

    [Authorize(KnowledgeHubPermissions.Practicum.Default)]
    public async Task<PracticumSubmissionDto> CreateSubmissionAsync(CreatePracticumSubmissionDto input)
    {
        var studentId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var project = await _projectRepository.GetAsync(input.ProjectId);
        var task = await _taskRepository.FirstOrDefaultAsync(x => x.Id == input.TaskId && x.ProjectId == input.ProjectId)
            ?? throw new UserFriendlyException("未找到实训任务。");
        var enrollment = await _enrollmentRepository.FirstOrDefaultAsync(x => x.ProjectId == input.ProjectId && x.StudentId == studentId)
            ?? throw new UserFriendlyException("请先参与该实训项目。");

        if (!project.AllowResubmission)
        {
            var hasSubmission = await _submissionRepository.AnyAsync(x => x.EnrollmentId == enrollment.Id && x.TaskId == task.Id);
            if (hasSubmission)
            {
                throw new UserFriendlyException("当前项目不允许重复提交。");
            }
        }

        var query = await _submissionRepository.GetQueryableAsync();
        var versionNo = await query
            .Where(x => x.EnrollmentId == enrollment.Id && x.TaskId == task.Id)
            .CountAsync() + 1;

        var submission = new PracticumSubmission(GuidGenerator.Create(), input.ProjectId, input.TaskId, enrollment.Id, studentId)
        {
            TenantId = CurrentTenant.Id,
            VersionNo = versionNo,
            Content = input.Content?.Trim(),
            AttachmentUrls = input.AttachmentUrls?.Trim(),
            LinkUrl = input.LinkUrl?.Trim(),
            ScreenshotUrls = input.ScreenshotUrls?.Trim()
        };

        await _submissionRepository.InsertAsync(submission, autoSave: true);

        enrollment.LastSubmittedAt = submission.SubmittedAt;
        await RefreshEnrollmentProgressAsync(enrollment);

        return await MapSubmissionDtoAsync(submission);
    }

    [Authorize(KnowledgeHubPermissions.Practicum.Default)]
    public async Task<PagedResultDto<PracticumSubmissionDto>> GetSubmissionListAsync(GetPracticumSubmissionsInput input)
    {
        var currentUserId = _currentUser.Id;
        var canReview = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Practicum.Review);

        var query = await _submissionRepository.GetQueryableAsync();
        query = query
            .WhereIf(input.ProjectId.HasValue, x => x.ProjectId == input.ProjectId.Value)
            .WhereIf(input.TaskId.HasValue, x => x.TaskId == input.TaskId.Value)
            .WhereIf(input.EnrollmentId.HasValue, x => x.EnrollmentId == input.EnrollmentId.Value)
            .WhereIf(input.StudentId.HasValue, x => x.StudentId == input.StudentId.Value)
            .WhereIf(input.Status.HasValue, x => x.Status == input.Status.Value);

        if (!canReview && currentUserId.HasValue)
        {
            query = query.Where(x => x.StudentId == currentUserId.Value);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.SubmittedAt)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToListAsync();

        return new PagedResultDto<PracticumSubmissionDto>(totalCount, await MapSubmissionDtosAsync(items));
    }

    [Authorize(KnowledgeHubPermissions.Practicum.Review)]
    public async Task<PracticumGuidanceRecordDto> AddGuidanceAsync(CreatePracticumGuidanceRecordDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Content))
        {
            throw new UserFriendlyException("指导内容不能为空。");
        }

        var teacherId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var enrollment = await _enrollmentRepository.GetAsync(input.EnrollmentId);
        var entity = new PracticumGuidanceRecord(
            GuidGenerator.Create(),
            enrollment.ProjectId,
            enrollment.Id,
            teacherId,
            input.Content.Trim())
        {
            TenantId = CurrentTenant.Id,
            TaskId = input.TaskId,
            IsVisibleToStudent = input.IsVisibleToStudent
        };

        await _guidanceRepository.InsertAsync(entity, autoSave: true);
        return await MapGuidanceDtoAsync(entity);
    }

    [Authorize(KnowledgeHubPermissions.Practicum.Default)]
    public async Task<List<PracticumGuidanceRecordDto>> GetGuidanceListAsync(Guid enrollmentId)
    {
        await EnsureEnrollmentAccessAsync(enrollmentId);
        var currentUserId = _currentUser.Id;
        var canReview = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Practicum.Review);

        var query = await _guidanceRepository.GetQueryableAsync();
        query = query.Where(x => x.EnrollmentId == enrollmentId);

        if (!canReview && currentUserId.HasValue)
        {
            query = query.Where(x => x.IsVisibleToStudent);
        }

        var items = await query.OrderByDescending(x => x.GuidedAt).ToListAsync();
        return await MapGuidanceDtosAsync(items);
    }

    [Authorize(KnowledgeHubPermissions.Practicum.Score)]
    public async Task<PracticumAssessmentDto> ScoreEnrollmentAsync(Guid enrollmentId, CreatePracticumAssessmentDto input)
    {
        var teacherId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
        var enrollment = await _enrollmentRepository.GetAsync(enrollmentId);
        var project = await _projectRepository.GetAsync(enrollment.ProjectId);
        var score = Math.Clamp(input.Score, 0, project.MaxScore);

        PracticumSubmission? submission = null;
        if (input.SubmissionId.HasValue)
        {
            submission = await _submissionRepository.GetAsync(input.SubmissionId.Value);
            submission.Status = PracticumSubmissionStatus.Reviewed;
            submission.TeacherFeedback = input.Comment?.Trim();
            submission.ReviewedAt = DateTime.UtcNow;
            submission.Score = score;
            await _submissionRepository.UpdateAsync(submission, autoSave: true);
        }

        var assessment = new PracticumAssessment(
            GuidGenerator.Create(),
            enrollment.ProjectId,
            enrollmentId,
            teacherId,
            score)
        {
            TenantId = CurrentTenant.Id,
            SubmissionId = input.SubmissionId,
            GradeLevel = input.GradeLevel?.Trim(),
            Comment = input.Comment?.Trim(),
            RubricJson = input.RubricJson?.Trim()
        };

        await _assessmentRepository.InsertAsync(assessment, autoSave: true);

        enrollment.FinalScore = score;
        enrollment.FinalComment = input.Comment?.Trim();
        await RefreshEnrollmentProgressAsync(enrollment);
        if (enrollment.Progress >= 100)
        {
            enrollment.Status = PracticumEnrollmentStatus.Completed;
            enrollment.CompletedAt = DateTime.UtcNow;
        }
        else
        {
            enrollment.Status = PracticumEnrollmentStatus.Reviewed;
        }

        await _enrollmentRepository.UpdateAsync(enrollment, autoSave: true);
        return await MapAssessmentDtoAsync(assessment);
    }

    [Authorize(KnowledgeHubPermissions.Practicum.Default)]
    public async Task<List<PracticumTimelineItemDto>> GetTimelineAsync(Guid enrollmentId)
    {
        await EnsureEnrollmentAccessAsync(enrollmentId);

        var enrollment = await _enrollmentRepository.GetAsync(enrollmentId);
        var project = await _projectRepository.GetAsync(enrollment.ProjectId);
        var submissions = await _submissionRepository.GetListAsync(x => x.EnrollmentId == enrollmentId);
        var guidances = await _guidanceRepository.GetListAsync(x => x.EnrollmentId == enrollmentId);
        var assessments = await _assessmentRepository.GetListAsync(x => x.EnrollmentId == enrollmentId);

        var operatorIds = new List<Guid> { enrollment.StudentId };
        operatorIds.AddRange(guidances.Select(x => x.TeacherId));
        operatorIds.AddRange(assessments.Select(x => x.TeacherId));
        var userMap = await GetUserNameMapAsync(operatorIds.Distinct().ToList());
        var taskMap = await GetTaskTitleMapAsync(project.Id);

        var timeline = new List<PracticumTimelineItemDto>
        {
            new()
            {
                Type = "Enrollment",
                Title = "参与实训",
                Content = project.Title,
                OperatorName = userMap.GetValueOrDefault(enrollment.StudentId, string.Empty),
                Time = enrollment.EnrolledAt
            }
        };

        timeline.AddRange(submissions.Select(x => new PracticumTimelineItemDto
        {
            Type = "Submission",
            Title = $"提交任务：{taskMap.GetValueOrDefault(x.TaskId, "未命名任务")}",
            Content = x.Content,
            OperatorName = userMap.GetValueOrDefault(x.StudentId, string.Empty),
            Time = x.SubmittedAt,
            Metadata = new Dictionary<string, string?>
            {
                ["Version"] = x.VersionNo.ToString(),
                ["Status"] = x.Status.ToString(),
                ["LinkUrl"] = x.LinkUrl
            }
        }));

        timeline.AddRange(guidances.Select(x => new PracticumTimelineItemDto
        {
            Type = "Guidance",
            Title = "教师指导",
            Content = x.Content,
            OperatorName = userMap.GetValueOrDefault(x.TeacherId, string.Empty),
            Time = x.GuidedAt,
            Metadata = new Dictionary<string, string?>
            {
                ["Task"] = x.TaskId.HasValue ? taskMap.GetValueOrDefault(x.TaskId.Value, string.Empty) : null
            }
        }));

        timeline.AddRange(assessments.Select(x => new PracticumTimelineItemDto
        {
            Type = "Assessment",
            Title = "教师评分",
            Content = x.Comment,
            OperatorName = userMap.GetValueOrDefault(x.TeacherId, string.Empty),
            Time = x.AssessedAt,
            Metadata = new Dictionary<string, string?>
            {
                ["Score"] = x.Score.ToString("0.##"),
                ["GradeLevel"] = x.GradeLevel
            }
        }));

        return timeline.OrderByDescending(x => x.Time).ToList();
    }

    [Authorize(KnowledgeHubPermissions.Practicum.Export)]
    public async Task<IRemoteStreamContent> ExportAssessmentsAsync(Guid? projectId)
    {
        var query = await _enrollmentRepository.GetQueryableAsync();
        query = query.WhereIf(projectId.HasValue, x => x.ProjectId == projectId.Value);
        var enrollments = await query.OrderByDescending(x => x.EnrolledAt).ToListAsync();

        foreach (var enrollment in enrollments)
        {
            await RefreshEnrollmentProgressAsync(enrollment);
        }

        var dtos = await MapEnrollmentDtosAsync(enrollments);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("实训成绩");
        worksheet.Cell(1, 1).Value = "实训项目";
        worksheet.Cell(1, 2).Value = "学生";
        worksheet.Cell(1, 3).Value = "状态";
        worksheet.Cell(1, 4).Value = "进度(%)";
        worksheet.Cell(1, 5).Value = "最终得分";
        worksheet.Cell(1, 6).Value = "最后提交时间";
        worksheet.Cell(1, 7).Value = "结项时间";

        var headerRange = worksheet.Range(1, 1, 1, 7);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

        for (var i = 0; i < dtos.Count; i++)
        {
            var row = i + 2;
            worksheet.Cell(row, 1).Value = dtos[i].ProjectTitle;
            worksheet.Cell(row, 2).Value = dtos[i].StudentName;
            worksheet.Cell(row, 3).Value = dtos[i].Status.ToString();
            worksheet.Cell(row, 4).Value = dtos[i].Progress;
            worksheet.Cell(row, 5).Value = dtos[i].FinalScore;
            worksheet.Cell(row, 6).Value = dtos[i].LastSubmittedAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
            worksheet.Cell(row, 7).Value = dtos[i].CompletedAt?.ToString("yyyy-MM-dd HH:mm") ?? string.Empty;
        }

        worksheet.Columns().AdjustToContents();

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(
            stream,
            $"实训成绩_{DateTime.Now:yyyyMMddHHmmss}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private async Task ValidateProjectInputAsync(CreateUpdatePracticumProjectDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("实训项目名称不能为空。");
        }

        if (input.Tasks.Count == 0)
        {
            throw new UserFriendlyException("至少需要配置一个实训任务。");
        }

        if (input.CourseId.HasValue)
        {
            await _courseRepository.GetAsync(input.CourseId.Value);
        }
    }

    private async Task ReplaceTasksAsync(Guid projectId, List<CreateUpdatePracticumTaskDto> inputs)
    {
        var existing = await _taskRepository.GetListAsync(x => x.ProjectId == projectId);
        foreach (var item in existing)
        {
            await _taskRepository.DeleteAsync(item);
        }

        foreach (var input in inputs.OrderBy(x => x.SortOrder))
        {
            var entity = new PracticumTask(GuidGenerator.Create(), projectId, input.Title.Trim())
            {
                TenantId = CurrentTenant.Id,
                Description = input.Description?.Trim(),
                Requirement = input.Requirement?.Trim(),
                DueTime = input.DueTime,
                ScoreWeight = Math.Clamp(input.ScoreWeight, 0, 100),
                SortOrder = input.SortOrder
            };
            await _taskRepository.InsertAsync(entity);
        }
    }

    private async Task ReplaceMaterialsAsync(Guid projectId, List<CreateUpdatePracticumMaterialDto> inputs)
    {
        var existing = await _materialRepository.GetListAsync(x => x.ProjectId == projectId);
        foreach (var item in existing)
        {
            await _materialRepository.DeleteAsync(item);
        }

        foreach (var input in inputs.OrderBy(x => x.SortOrder))
        {
            if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.ResourceUrl))
            {
                continue;
            }

            var entity = new PracticumMaterial(GuidGenerator.Create(), projectId, input.Title.Trim(), input.ResourceUrl.Trim())
            {
                TenantId = CurrentTenant.Id,
                TaskId = input.TaskId,
                Description = input.Description?.Trim(),
                MaterialType = input.MaterialType,
                SortOrder = input.SortOrder
            };
            await _materialRepository.InsertAsync(entity);
        }
    }

    private async Task EnsureStudentCourseAsync(PracticumProject project, Guid studentId)
    {
        if (!project.CourseId.HasValue)
        {
            return;
        }

        StudentCourse? existing;
        using (DataFilter.Disable<IMultiTenant>())
        {
            existing = await _studentCourseRepository.FirstOrDefaultAsync(
                x => x.CourseId == project.CourseId.Value && x.StudentId == studentId);
        }

        if (existing != null)
        {
            return;
        }

        Guid? studentTenantId;
        using (DataFilter.Disable<IMultiTenant>())
        {
            var user = await _userRepository.GetAsync(studentId);
            studentTenantId = user.TenantId;
        }

        using (_currentTenant.Change(studentTenantId))
        {
            var studentCourse = new StudentCourse(GuidGenerator.Create(), studentId, project.CourseId.Value);
            await _studentCourseRepository.InsertAsync(studentCourse, autoSave: true);
        }
    }

    private async Task RefreshEnrollmentProgressAsync(PracticumEnrollment enrollment)
    {
        var tasks = await _taskRepository.GetListAsync(x => x.ProjectId == enrollment.ProjectId);
        var totalTasks = tasks.Count;
        var submissions = await _submissionRepository.GetListAsync(x => x.EnrollmentId == enrollment.Id);
        var submittedTaskCount = submissions.Select(x => x.TaskId).Distinct().Count();
        enrollment.Progress = totalTasks == 0 ? 0 : Math.Round((decimal)submittedTaskCount / totalTasks * 100, 1);

        if (submittedTaskCount == 0)
        {
            enrollment.Status = PracticumEnrollmentStatus.Enrolled;
        }
        else if (submittedTaskCount < totalTasks)
        {
            enrollment.Status = PracticumEnrollmentStatus.InProgress;
        }
        else
        {
            enrollment.Status = enrollment.FinalScore.HasValue
                ? PracticumEnrollmentStatus.Completed
                : PracticumEnrollmentStatus.Submitted;
        }

        await _enrollmentRepository.UpdateAsync(enrollment, autoSave: true);
    }

    private async Task EnsureEnrollmentAccessAsync(Guid enrollmentId)
    {
        var enrollment = await _enrollmentRepository.GetAsync(enrollmentId);
        var canReview = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Practicum.Review);
        var currentUserId = _currentUser.Id;
        if (!canReview && (!currentUserId.HasValue || enrollment.StudentId != currentUserId.Value))
        {
            throw new AbpAuthorizationException("无权查看该实训记录。");
        }
    }

    private async Task<List<PracticumTaskDto>> GetTaskDtosAsync(Guid projectId)
    {
        var items = await _taskRepository.GetListAsync(x => x.ProjectId == projectId);
        return items
            .OrderBy(x => x.SortOrder)
            .Select(x => new PracticumTaskDto
            {
                Id = x.Id,
                ProjectId = x.ProjectId,
                Title = x.Title,
                Description = x.Description,
                Requirement = x.Requirement,
                DueTime = x.DueTime,
                ScoreWeight = x.ScoreWeight,
                SortOrder = x.SortOrder
            })
            .ToList();
    }

    private async Task<List<PracticumMaterialDto>> GetMaterialDtosAsync(Guid projectId)
    {
        var items = await _materialRepository.GetListAsync(x => x.ProjectId == projectId);
        return items
            .OrderBy(x => x.SortOrder)
            .Select(x => new PracticumMaterialDto
            {
                Id = x.Id,
                ProjectId = x.ProjectId,
                TaskId = x.TaskId,
                Title = x.Title,
                Description = x.Description,
                MaterialType = x.MaterialType,
                ResourceUrl = x.ResourceUrl,
                SortOrder = x.SortOrder
            })
            .ToList();
    }

    private async Task<PracticumProjectDto> MapProjectDtoAsync(PracticumProject entity)
    {
        var dto = new PracticumProjectDto();
        CopyProjectDto(entity, dto);
        dto.TaskCount = await _taskRepository.CountAsync(x => x.ProjectId == entity.Id);
        dto.MaterialCount = await _materialRepository.CountAsync(x => x.ProjectId == entity.Id);
        dto.EnrollmentCount = await _enrollmentRepository.CountAsync(x => x.ProjectId == entity.Id);

        if (entity.CourseId.HasValue)
        {
            dto.CourseTitle = (await _courseRepository.FindAsync(entity.CourseId.Value))?.Title;
        }

        if (_currentUser.Id.HasValue)
        {
            var enrollment = await _enrollmentRepository.FirstOrDefaultAsync(x => x.ProjectId == entity.Id && x.StudentId == _currentUser.Id.Value);
            dto.IsCurrentUserEnrolled = enrollment != null && enrollment.Status != PracticumEnrollmentStatus.Cancelled;
            dto.CurrentUserProgress = enrollment?.Progress;
        }

        return dto;
    }

    private async Task<List<PracticumProjectDto>> MapProjectDtosAsync(List<PracticumProject> entities)
    {
        var result = new List<PracticumProjectDto>();
        foreach (var entity in entities)
        {
            result.Add(await MapProjectDtoAsync(entity));
        }

        return result;
    }

    private async Task<PracticumProjectDetailDto> MapProjectDetailDtoAsync(PracticumProject entity)
    {
        var source = await MapProjectDtoAsync(entity);
        return new PracticumProjectDetailDto
        {
            Id = source.Id,
            Title = source.Title,
            Summary = source.Summary,
            Description = source.Description,
            CoverImageUrl = source.CoverImageUrl,
            CourseId = source.CourseId,
            CourseTitle = source.CourseTitle,
            Major = source.Major,
            ClassName = source.ClassName,
            Status = source.Status,
            StartTime = source.StartTime,
            EndTime = source.EndTime,
            MaxScore = source.MaxScore,
            AllowResubmission = source.AllowResubmission,
            TaskCount = source.TaskCount,
            MaterialCount = source.MaterialCount,
            EnrollmentCount = source.EnrollmentCount,
            IsCurrentUserEnrolled = source.IsCurrentUserEnrolled,
            CurrentUserProgress = source.CurrentUserProgress,
            AgentName = source.AgentName,
            AgentPrompt = source.AgentPrompt,
            CreationTime = source.CreationTime,
            CreatorId = source.CreatorId,
            LastModificationTime = source.LastModificationTime,
            LastModifierId = source.LastModifierId
        };
    }

    private static void CopyProjectDto(PracticumProject source, PracticumProjectDto target)
    {
        target.Id = source.Id;
        target.Title = source.Title;
        target.Summary = source.Summary;
        target.Description = source.Description;
        target.CoverImageUrl = source.CoverImageUrl;
        target.CourseId = source.CourseId;
        target.Major = source.Major;
        target.ClassName = source.ClassName;
        target.Status = source.Status;
        target.StartTime = source.StartTime;
        target.EndTime = source.EndTime;
        target.MaxScore = source.MaxScore;
        target.AllowResubmission = source.AllowResubmission;
        target.AgentName = source.AgentName;
        target.AgentPrompt = source.AgentPrompt;
        target.CreationTime = source.CreationTime;
        target.CreatorId = source.CreatorId;
        target.LastModificationTime = source.LastModificationTime;
        target.LastModifierId = source.LastModifierId;
    }

    private async Task<Dictionary<Guid, string>> GetProjectTitleMapAsync(List<Guid> projectIds)
    {
        if (projectIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var query = await _projectRepository.GetQueryableAsync();
        return (await query.Where(x => projectIds.Contains(x.Id)).ToListAsync())
            .ToDictionary(x => x.Id, x => x.Title);
    }

    private async Task<Dictionary<Guid, string>> GetTaskTitleMapAsync(Guid projectId)
    {
        var query = await _taskRepository.GetQueryableAsync();
        return (await query.Where(x => x.ProjectId == projectId).ToListAsync())
            .ToDictionary(x => x.Id, x => x.Title);
    }

    private async Task<Dictionary<Guid, string>> GetUserNameMapAsync(List<Guid> userIds)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        using (DataFilter.Disable<IMultiTenant>())
        {
            var query = await _userRepository.GetQueryableAsync();
            return (await query.Where(x => userIds.Contains(x.Id)).ToListAsync())
                .ToDictionary(x => x.Id, x => string.IsNullOrWhiteSpace(x.Name) ? x.UserName : x.Name);
        }
    }

    private async Task<List<PracticumEnrollmentDto>> MapEnrollmentDtosAsync(List<PracticumEnrollment> entities)
    {
        if (entities.Count == 0)
        {
            return new List<PracticumEnrollmentDto>();
        }

        var projectMap = await GetProjectTitleMapAsync(entities.Select(x => x.ProjectId).Distinct().ToList());
        var userMap = await GetUserNameMapAsync(entities.Select(x => x.StudentId).Distinct().ToList());

        return entities.Select(x => new PracticumEnrollmentDto
        {
            Id = x.Id,
            ProjectId = x.ProjectId,
            ProjectTitle = projectMap.GetValueOrDefault(x.ProjectId, string.Empty),
            StudentId = x.StudentId,
            StudentName = userMap.GetValueOrDefault(x.StudentId, string.Empty),
            Status = x.Status,
            Progress = x.Progress,
            EnrolledAt = x.EnrolledAt,
            LastSubmittedAt = x.LastSubmittedAt,
            FinalScore = x.FinalScore,
            FinalComment = x.FinalComment,
            CompletedAt = x.CompletedAt,
            CreationTime = x.CreationTime,
            CreatorId = x.CreatorId,
            LastModificationTime = x.LastModificationTime,
            LastModifierId = x.LastModifierId
        }).ToList();
    }

    private async Task<PracticumSubmissionDto> MapSubmissionDtoAsync(PracticumSubmission entity)
    {
        return (await MapSubmissionDtosAsync(new List<PracticumSubmission> { entity })).First();
    }

    private async Task<List<PracticumSubmissionDto>> MapSubmissionDtosAsync(List<PracticumSubmission> entities)
    {
        if (entities.Count == 0)
        {
            return new List<PracticumSubmissionDto>();
        }

        var projectMap = await GetProjectTitleMapAsync(entities.Select(x => x.ProjectId).Distinct().ToList());
        var userMap = await GetUserNameMapAsync(entities.Select(x => x.StudentId).Distinct().ToList());
        var taskIds = entities.Select(x => x.TaskId).Distinct().ToList();
        var taskQuery = await _taskRepository.GetQueryableAsync();
        var taskMap = (await taskQuery.Where(x => taskIds.Contains(x.Id)).ToListAsync())
            .ToDictionary(x => x.Id, x => x.Title);

        return entities.Select(x => new PracticumSubmissionDto
        {
            Id = x.Id,
            ProjectId = x.ProjectId,
            ProjectTitle = projectMap.GetValueOrDefault(x.ProjectId, string.Empty),
            TaskId = x.TaskId,
            TaskTitle = taskMap.GetValueOrDefault(x.TaskId, string.Empty),
            EnrollmentId = x.EnrollmentId,
            StudentId = x.StudentId,
            StudentName = userMap.GetValueOrDefault(x.StudentId, string.Empty),
            VersionNo = x.VersionNo,
            Content = x.Content,
            AttachmentUrls = x.AttachmentUrls,
            LinkUrl = x.LinkUrl,
            ScreenshotUrls = x.ScreenshotUrls,
            Status = x.Status,
            SubmittedAt = x.SubmittedAt,
            TeacherFeedback = x.TeacherFeedback,
            ReviewedAt = x.ReviewedAt,
            Score = x.Score,
            CreationTime = x.CreationTime,
            CreatorId = x.CreatorId,
            LastModificationTime = x.LastModificationTime,
            LastModifierId = x.LastModifierId
        }).ToList();
    }

    private async Task<PracticumGuidanceRecordDto> MapGuidanceDtoAsync(PracticumGuidanceRecord entity)
    {
        return (await MapGuidanceDtosAsync(new List<PracticumGuidanceRecord> { entity })).First();
    }

    private async Task<List<PracticumGuidanceRecordDto>> MapGuidanceDtosAsync(List<PracticumGuidanceRecord> entities)
    {
        if (entities.Count == 0)
        {
            return new List<PracticumGuidanceRecordDto>();
        }

        var userMap = await GetUserNameMapAsync(entities.Select(x => x.TeacherId).Distinct().ToList());
        var taskIds = entities.Where(x => x.TaskId.HasValue).Select(x => x.TaskId!.Value).Distinct().ToList();
        var taskQuery = await _taskRepository.GetQueryableAsync();
        var taskMap = taskIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await taskQuery.Where(x => taskIds.Contains(x.Id)).ToListAsync())
                .ToDictionary(x => x.Id, x => x.Title);

        return entities.Select(x => new PracticumGuidanceRecordDto
        {
            Id = x.Id,
            ProjectId = x.ProjectId,
            EnrollmentId = x.EnrollmentId,
            TaskId = x.TaskId,
            TaskTitle = x.TaskId.HasValue ? taskMap.GetValueOrDefault(x.TaskId.Value, string.Empty) : null,
            TeacherId = x.TeacherId,
            TeacherName = userMap.GetValueOrDefault(x.TeacherId, string.Empty),
            Content = x.Content,
            IsVisibleToStudent = x.IsVisibleToStudent,
            GuidedAt = x.GuidedAt,
            CreationTime = x.CreationTime,
            CreatorId = x.CreatorId,
            LastModificationTime = x.LastModificationTime,
            LastModifierId = x.LastModifierId
        }).ToList();
    }

    private async Task<PracticumAssessmentDto> MapAssessmentDtoAsync(PracticumAssessment entity)
    {
        var userMap = await GetUserNameMapAsync(new List<Guid> { entity.TeacherId });
        return new PracticumAssessmentDto
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            EnrollmentId = entity.EnrollmentId,
            SubmissionId = entity.SubmissionId,
            TeacherId = entity.TeacherId,
            TeacherName = userMap.GetValueOrDefault(entity.TeacherId, string.Empty),
            Score = entity.Score,
            GradeLevel = entity.GradeLevel,
            Comment = entity.Comment,
            RubricJson = entity.RubricJson,
            AssessedAt = entity.AssessedAt,
            CreationTime = entity.CreationTime,
            CreatorId = entity.CreatorId,
            LastModificationTime = entity.LastModificationTime,
            LastModifierId = entity.LastModifierId
        };
    }

    public async Task<PracticumAgentConfigDto> GetAgentConfigAsync(Guid projectId)
    {
        var entity = await _projectRepository.GetAsync(projectId);

        // 所有已认证用户可读取 agentName（聊天页面 @提及用）
        // AgentPrompt 仅对编辑者可见（管理员/教师后台配置用）
        var canEdit = await AuthorizationService.IsGrantedAsync(KnowledgeHubPermissions.Practicum.Edit);
        return new PracticumAgentConfigDto
        {
            AgentName = entity.AgentName,
            AgentPrompt = canEdit ? entity.AgentPrompt : null
        };
    }

    [Authorize(KnowledgeHubPermissions.Practicum.Edit)]
    public async Task UpdateAgentConfigAsync(Guid projectId, UpdatePracticumAgentConfigDto input)
    {
        var entity = await _projectRepository.GetAsync(projectId);
        entity.AgentName = input.AgentName?.Trim();
        entity.AgentPrompt = input.AgentPrompt?.Trim();
        await _projectRepository.UpdateAsync(entity, autoSave: true);
    }
}
