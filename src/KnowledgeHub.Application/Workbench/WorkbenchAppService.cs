using System;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.AI;
using KnowledgeHub.Application.AI;
using KnowledgeHub.Courses;
using KnowledgeHub.Courses.Enums;
using KnowledgeHub.Employment;
using KnowledgeHub.Employment.Enums;
using KnowledgeHub.Exams;
using KnowledgeHub.Learning;
using KnowledgeHub.Learning.Enums;
using KnowledgeHub.Majors;
using KnowledgeHub.MicroMajors;
using KnowledgeHub.MicroMajors.Enums;
using KnowledgeHub.News;
using KnowledgeHub.News.Enums;
using KnowledgeHub.Practicums;
using KnowledgeHub.Practicums.Enums;
using KnowledgeHub.Resources;
using KnowledgeHub.Resources.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace KnowledgeHub.Workbench;

/// <summary>
/// 系统工作台统计服务。
///
/// 数据口径：
/// - 租户上下文用户：固定统计当前租户（忽略入参），保证租户数据隔离；
/// - host 全局管理员：传 TenantId 统计指定租户，传空则汇总全部租户。
/// 所有查询都显式关闭 <see cref="IMultiTenant"/> 数据过滤器后按 TenantId 过滤，
/// 以便 host 既能跨租户汇总、也能精确落到某个租户。
/// </summary>
[Authorize]
public class WorkbenchAppService : KnowledgeHubAppService, IWorkbenchAppService
{
    private readonly IRepository<Resource, Guid> _resourceRepository;
    private readonly IRepository<ResourceCategory, Guid> _resourceCategoryRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<Major, Guid> _majorRepository;
    private readonly IRepository<MicroMajor, Guid> _microMajorRepository;
    private readonly IRepository<MicroMajorEnrollment, Guid> _microMajorEnrollmentRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<AiUsageRecord, Guid> _aiUsageRepository;
    private readonly IRepository<AiGenerationTask, Guid> _aiTaskRepository;
    private readonly IRepository<JobPosting, Guid> _jobPostingRepository;
    private readonly IRepository<JobApplication, Guid> _jobApplicationRepository;
    private readonly IRepository<InterviewSchedule, Guid> _interviewRepository;
    private readonly IRepository<EmploymentOutcome, Guid> _employmentOutcomeRepository;
    private readonly IRepository<PracticumProject, Guid> _practicumProjectRepository;
    private readonly IRepository<PracticumTask, Guid> _practicumTaskRepository;
    private readonly IRepository<PracticumEnrollment, Guid> _practicumEnrollmentRepository;
    private readonly IRepository<PracticumSubmission, Guid> _practicumSubmissionRepository;
    private readonly IRepository<NewsArticle, Guid> _newsRepository;
    private readonly IRepository<KnowledgeHub.Domain.Search.SearchQuery, Guid> _searchQueryRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IDataFilter _dataFilter;

    public WorkbenchAppService(
        IRepository<Resource, Guid> resourceRepository,
        IRepository<ResourceCategory, Guid> resourceCategoryRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<Major, Guid> majorRepository,
        IRepository<MicroMajor, Guid> microMajorRepository,
        IRepository<MicroMajorEnrollment, Guid> microMajorEnrollmentRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<AiUsageRecord, Guid> aiUsageRepository,
        IRepository<AiGenerationTask, Guid> aiTaskRepository,
        IRepository<JobPosting, Guid> jobPostingRepository,
        IRepository<JobApplication, Guid> jobApplicationRepository,
        IRepository<InterviewSchedule, Guid> interviewRepository,
        IRepository<EmploymentOutcome, Guid> employmentOutcomeRepository,
        IRepository<PracticumProject, Guid> practicumProjectRepository,
        IRepository<PracticumTask, Guid> practicumTaskRepository,
        IRepository<PracticumEnrollment, Guid> practicumEnrollmentRepository,
        IRepository<PracticumSubmission, Guid> practicumSubmissionRepository,
        IRepository<NewsArticle, Guid> newsRepository,
        IRepository<KnowledgeHub.Domain.Search.SearchQuery, Guid> searchQueryRepository,
        ITenantRepository tenantRepository,
        IDataFilter dataFilter)
    {
        _resourceRepository = resourceRepository;
        _resourceCategoryRepository = resourceCategoryRepository;
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
        _exerciseRepository = exerciseRepository;
        _majorRepository = majorRepository;
        _microMajorRepository = microMajorRepository;
        _microMajorEnrollmentRepository = microMajorEnrollmentRepository;
        _studentCourseRepository = studentCourseRepository;
        _aiUsageRepository = aiUsageRepository;
        _aiTaskRepository = aiTaskRepository;
        _jobPostingRepository = jobPostingRepository;
        _jobApplicationRepository = jobApplicationRepository;
        _interviewRepository = interviewRepository;
        _employmentOutcomeRepository = employmentOutcomeRepository;
        _practicumProjectRepository = practicumProjectRepository;
        _practicumTaskRepository = practicumTaskRepository;
        _practicumEnrollmentRepository = practicumEnrollmentRepository;
        _practicumSubmissionRepository = practicumSubmissionRepository;
        _newsRepository = newsRepository;
        _searchQueryRepository = searchQueryRepository;
        _tenantRepository = tenantRepository;
        _dataFilter = dataFilter;
    }

    public async Task<WorkbenchStatsDto> GetStatsAsync(WorkbenchQueryDto input)
    {
        // 租户上下文用户只能看当前租户；host 可指定租户或汇总全部。
        var scope = CurrentTenant.Id ?? input.TenantId;

        var dto = new WorkbenchStatsDto
        {
            TenantId = scope,
            GeneratedAt = Clock.Now,
            TenantName = scope.HasValue
                ? (await _tenantRepository.FindAsync(scope.Value))?.Name
                : "全部租户",
        };

        using (_dataFilter.Disable<IMultiTenant>())
        {
            dto.Resources = await BuildResourceStatsAsync(scope);
            dto.Courses = await BuildCourseStatsAsync(scope);
            dto.Ai = await BuildAiStatsAsync(scope);
            dto.Employment = await BuildEmploymentStatsAsync(scope);
            dto.Practicum = await BuildPracticumStatsAsync(scope);
            dto.News = await BuildNewsStatsAsync(scope);
            dto.Search = await BuildSearchStatsAsync(scope);
            dto.Users = await BuildUserStatsAsync(scope);
        }

        return dto;
    }

    private async Task<WorkbenchResourceStatsDto> BuildResourceStatsAsync(Guid? tenantId)
    {
        var query = await _resourceRepository.GetQueryableAsync();
        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        var categoryQuery = await _resourceCategoryRepository.GetQueryableAsync();
        if (tenantId.HasValue)
        {
            categoryQuery = categoryQuery.Where(x => x.TenantId == tenantId.Value);
        }

        return new WorkbenchResourceStatsDto
        {
            Total = await query.LongCountAsync(),
            Draft = await query.LongCountAsync(x => x.Status == ResourceStatus.Draft),
            PendingReview = await query.LongCountAsync(x => x.Status == ResourceStatus.PendingReview),
            Approved = await query.LongCountAsync(x =>
                x.Status == ResourceStatus.SchoolApproved || x.Status == ResourceStatus.LeagueApproved),
            Rejected = await query.LongCountAsync(x => x.Status == ResourceStatus.Rejected),
            CategoryCount = await categoryQuery.LongCountAsync(),
            TotalDownloads = await query.SumAsync(x => (long?)x.DownloadCount) ?? 0,
            TotalViews = await query.SumAsync(x => (long?)x.ViewCount) ?? 0,
            TotalCollections = await query.SumAsync(x => (long?)x.CollectionCount) ?? 0,
        };
    }

    private async Task<WorkbenchCourseStatsDto> BuildCourseStatsAsync(Guid? tenantId)
    {
        var courseQuery = await _courseRepository.GetQueryableAsync();
        if (tenantId.HasValue)
        {
            courseQuery = courseQuery.Where(x => x.TenantId == tenantId.Value);
        }

        var exerciseQuery = await _exerciseRepository.GetQueryableAsync();
        if (tenantId.HasValue)
        {
            exerciseQuery = exerciseQuery.Where(x => x.TenantId == tenantId.Value);
        }

        var majorQuery = await _majorRepository.GetQueryableAsync();
        if (tenantId.HasValue)
        {
            majorQuery = majorQuery.Where(x => x.TenantId == tenantId.Value);
        }

        var microMajorQuery = await _microMajorRepository.GetQueryableAsync();
        if (tenantId.HasValue)
        {
            microMajorQuery = microMajorQuery.Where(x => x.TenantId == tenantId.Value);
        }

        var microMajorEnrollmentQuery = await _microMajorEnrollmentRepository.GetQueryableAsync();
        if (tenantId.HasValue)
        {
            microMajorEnrollmentQuery = microMajorEnrollmentQuery.Where(x => x.TenantId == tenantId.Value);
        }

        var studentCourseQuery = await _studentCourseRepository.GetQueryableAsync();
        if (tenantId.HasValue)
        {
            studentCourseQuery = studentCourseQuery.Where(x => x.TenantId == tenantId.Value);
        }

        // 章节本身不带 TenantId，通过课程子查询归属租户。
        var courseIds = courseQuery.Select(c => c.Id);
        var chapterQuery = (await _chapterRepository.GetQueryableAsync())
            .Where(ch => courseIds.Contains(ch.CourseId));

        return new WorkbenchCourseStatsDto
        {
            Total = await courseQuery.LongCountAsync(),
            Published = await courseQuery.LongCountAsync(x => x.Status == CourseStatus.Published),
            PendingReview = await courseQuery.LongCountAsync(x => x.Status == CourseStatus.PendingReview),
            Draft = await courseQuery.LongCountAsync(x => x.Status == CourseStatus.Draft),
            ChapterCount = await chapterQuery.LongCountAsync(),
            ExerciseCount = await exerciseQuery.LongCountAsync(),
            MajorCount = await majorQuery.LongCountAsync(),
            MicroMajorCount = await microMajorQuery.LongCountAsync(),
            MicroMajorPublished = await microMajorQuery.LongCountAsync(x => x.Status == MicroMajorStatus.Published),
            EnrollmentCount = await studentCourseQuery.LongCountAsync(x => x.Status != StudentCourseStatus.Dropped),
            MicroMajorEnrollmentCount = await microMajorEnrollmentQuery.LongCountAsync(),
        };
    }

    private async Task<WorkbenchAiStatsDto> BuildAiStatsAsync(Guid? tenantId)
    {
        var usageQuery = await _aiUsageRepository.GetQueryableAsync();
        if (tenantId.HasValue)
        {
            usageQuery = usageQuery.Where(x => x.TenantId == tenantId.Value);
        }

        var taskQuery = await _aiTaskRepository.GetQueryableAsync();
        if (tenantId.HasValue)
        {
            taskQuery = taskQuery.Where(x => x.TenantId == tenantId.Value);
        }

        return new WorkbenchAiStatsDto
        {
            TotalCalls = await usageQuery.LongCountAsync(),
            SuccessCalls = await usageQuery.LongCountAsync(x => x.Status == AiUsageStatus.Completed),
            FailedCalls = await usageQuery.LongCountAsync(x => x.Status == AiUsageStatus.Failed),
            RunningCalls = await usageQuery.LongCountAsync(x => x.Status == AiUsageStatus.Running),
            TotalInputTokens = await usageQuery.SumAsync(x => (long?)x.InputTokens) ?? 0,
            TotalOutputTokens = await usageQuery.SumAsync(x => (long?)x.OutputTokens) ?? 0,
            EstimatedCost = await usageQuery.SumAsync(x => (decimal?)x.EstimatedCost) ?? 0,
            TaskCount = await taskQuery.LongCountAsync(),
        };
    }

    private async Task<WorkbenchEmploymentStatsDto> BuildEmploymentStatsAsync(Guid? tenantId)
    {
        var jobQuery = await _jobPostingRepository.GetQueryableAsync();
        var applicationQuery = await _jobApplicationRepository.GetQueryableAsync();
        var interviewQuery = await _interviewRepository.GetQueryableAsync();
        var outcomeQuery = await _employmentOutcomeRepository.GetQueryableAsync();

        if (tenantId.HasValue)
        {
            var id = tenantId.Value;
            jobQuery = jobQuery.Where(x => x.TenantId == id);
            applicationQuery = applicationQuery.Where(x => x.TenantId == id);
            interviewQuery = interviewQuery.Where(x => x.TenantId == id);
            outcomeQuery = outcomeQuery.Where(x => x.TenantId == id);
        }

        return new WorkbenchEmploymentStatsDto
        {
            JobTotal = await jobQuery.LongCountAsync(),
            JobPublished = await jobQuery.LongCountAsync(x => x.Status == EmploymentJobStatus.Published),
            ApplicationCount = await applicationQuery.LongCountAsync(),
            InterviewCount = await interviewQuery.LongCountAsync(),
            OutcomeCount = await outcomeQuery.LongCountAsync(),
            SignedCount = await outcomeQuery.LongCountAsync(x => x.Status == EmploymentOutcomeStatus.Signed),
            EmployedCount = await outcomeQuery.LongCountAsync(x => x.Status == EmploymentOutcomeStatus.Employed),
        };
    }

    private async Task<WorkbenchPracticumStatsDto> BuildPracticumStatsAsync(Guid? tenantId)
    {
        var projectQuery = await _practicumProjectRepository.GetQueryableAsync();
        var taskQuery = await _practicumTaskRepository.GetQueryableAsync();
        var enrollmentQuery = await _practicumEnrollmentRepository.GetQueryableAsync();
        var submissionQuery = await _practicumSubmissionRepository.GetQueryableAsync();

        if (tenantId.HasValue)
        {
            var id = tenantId.Value;
            projectQuery = projectQuery.Where(x => x.TenantId == id);
            taskQuery = taskQuery.Where(x => x.TenantId == id);
            enrollmentQuery = enrollmentQuery.Where(x => x.TenantId == id);
            submissionQuery = submissionQuery.Where(x => x.TenantId == id);
        }

        return new WorkbenchPracticumStatsDto
        {
            ProjectTotal = await projectQuery.LongCountAsync(),
            ProjectPublished = await projectQuery.LongCountAsync(x => x.Status == PracticumProjectStatus.Published),
            TaskCount = await taskQuery.LongCountAsync(),
            EnrollmentCount = await enrollmentQuery.LongCountAsync(),
            SubmissionCount = await submissionQuery.LongCountAsync(),
        };
    }

    private async Task<WorkbenchNewsStatsDto> BuildNewsStatsAsync(Guid? tenantId)
    {
        var query = await _newsRepository.GetQueryableAsync();
        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        return new WorkbenchNewsStatsDto
        {
            Total = await query.LongCountAsync(),
            Published = await query.LongCountAsync(x => x.Status == NewsArticleStatus.Published),
            PendingReview = await query.LongCountAsync(x => x.Status == NewsArticleStatus.PendingReview),
        };
    }

    private async Task<WorkbenchSearchStatsDto> BuildSearchStatsAsync(Guid? tenantId)
    {
        // SearchQuery 显式保存 TenantId（非 IMultiTenant），故在关闭过滤器后仍需手动过滤。
        var query = await _searchQueryRepository.GetQueryableAsync();
        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        var today = Clock.Now.Date;
        var activeUsers = await AsyncExecuter.CountAsync(
            query.Select(x => x.UserId).Distinct());

        return new WorkbenchSearchStatsDto
        {
            TotalSearches = await query.LongCountAsync(),
            TodaySearches = await query.LongCountAsync(x => x.CreationTime >= today),
            ActiveUsers = activeUsers,
        };
    }

    private async Task<WorkbenchUserStatsDto> BuildUserStatsAsync(Guid? tenantId)
    {
        var studentCourseQuery = await _studentCourseRepository.GetQueryableAsync();
        var courseQuery = await _courseRepository.GetQueryableAsync();
        if (tenantId.HasValue)
        {
            var id = tenantId.Value;
            studentCourseQuery = studentCourseQuery.Where(x => x.TenantId == id);
            courseQuery = courseQuery.Where(x => x.TenantId == id);
        }

        var studentCount = await AsyncExecuter.CountAsync(
            studentCourseQuery
                .Where(x => x.Status != StudentCourseStatus.Dropped)
                .Select(x => x.StudentId)
                .Distinct());

        var teacherCount = await AsyncExecuter.CountAsync(
            courseQuery
                .Where(x => x.TeacherId != null)
                .Select(x => x.TeacherId)
                .Distinct());

        return new WorkbenchUserStatsDto
        {
            StudentCount = studentCount,
            TeacherCount = teacherCount,
        };
    }
}
