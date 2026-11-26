using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Competencies;
using KnowledgeHub.Competencies.Dtos;
using KnowledgeHub.Employment.Dtos;
using KnowledgeHub.Employment.Enums;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Employment;

/// <summary>
/// 岗位能力图谱：岗位 → 核心任务 → 能力（复用能力体系）→ 课程知识点 → 掌握度/达成度。
/// </summary>
[IgnoreAntiforgeryToken]
[Authorize]
public class JobCompetencyGraphAppService : KnowledgeHubAppService, IJobCompetencyGraphAppService
{
    private readonly IRepository<JobCompetencyGraph, Guid> _graphRepository;
    private readonly IRepository<JobCoreTask, Guid> _taskRepository;
    private readonly IRepository<JobTaskCompetency, Guid> _linkRepository;
    private readonly IRepository<JobPosting, Guid> _jobPostingRepository;
    private readonly IRepository<Competency, Guid> _competencyRepository;
    private readonly IRepository<CompetencyKnowledgeLink, Guid> _competencyLinkRepository;
    /// <summary>课程（「能力 → 课程」方向要展示课程名）。</summary>
    private readonly IRepository<Courses.Course, Guid> _courseRepository;
    /// <summary>课程 → 能力支撑（课程侧配置，决定"这个岗位能力由哪些课支撑"）。</summary>
    private readonly IRepository<CourseCompetencySupport, Guid> _courseCompetencyRepository;
    private readonly ICompetencyAppService _competencyAppService;
    private readonly IDataFilter _dataFilter;

    public JobCompetencyGraphAppService(
        IRepository<JobCompetencyGraph, Guid> graphRepository,
        IRepository<JobCoreTask, Guid> taskRepository,
        IRepository<JobTaskCompetency, Guid> linkRepository,
        IRepository<JobPosting, Guid> jobPostingRepository,
        IRepository<Competency, Guid> competencyRepository,
        IRepository<CompetencyKnowledgeLink, Guid> competencyLinkRepository,
        IRepository<Courses.Course, Guid> courseRepository,
        IRepository<CourseCompetencySupport, Guid> courseCompetencyRepository,
        ICompetencyAppService competencyAppService,
        IDataFilter dataFilter)
    {
        _graphRepository = graphRepository;
        _taskRepository = taskRepository;
        _linkRepository = linkRepository;
        _jobPostingRepository = jobPostingRepository;
        _competencyRepository = competencyRepository;
        _competencyLinkRepository = competencyLinkRepository;
        _courseRepository = courseRepository;
        _courseCompetencyRepository = courseCompetencyRepository;
        _competencyAppService = competencyAppService;
        _dataFilter = dataFilter;
    }

    public async Task<List<JobCompetencyGraphDto>> GetListAsync(
        Guid? majorId = null, string? filter = null, Guid? jobPostingId = null)
    {
        List<JobCompetencyGraph> graphs;
        List<JobCoreTask> tasks;
        List<JobTaskCompetency> links;
        Dictionary<Guid, int> competencyKnowledgeCount;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _graphRepository.GetQueryableAsync();
            graphs = await AsyncExecuter.ToListAsync(
                query.WhereIf(majorId.HasValue, x => x.MajorId == majorId!.Value)
                     .WhereIf(jobPostingId.HasValue, x => x.JobPostingId == jobPostingId!.Value)
                     .WhereIf(!string.IsNullOrWhiteSpace(filter), x => x.Title.Contains(filter!))
                     .OrderByDescending(x => x.Version)
                     .ThenBy(x => x.Title));

            tasks = await AsyncExecuter.ToListAsync(await _taskRepository.GetQueryableAsync());
            links = await AsyncExecuter.ToListAsync(await _linkRepository.GetQueryableAsync());
            competencyKnowledgeCount = await GetCompetencyKnowledgeCountAsync();
        }

        var taskByGraph = tasks.GroupBy(x => x.GraphId).ToDictionary(g => g.Key, g => g.ToList());
        var linkByTask = links.GroupBy(x => x.CoreTaskId).ToDictionary(g => g.Key, g => g.ToList());

        var postingIds = graphs.Where(x => x.JobPostingId.HasValue).Select(x => x.JobPostingId!.Value).Distinct().ToList();
        var postingsById = new Dictionary<Guid, JobPosting>();
        if (postingIds.Count > 0)
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var postings = await _jobPostingRepository.GetListAsync(x => postingIds.Contains(x.Id));
                postingsById = postings.ToDictionary(x => x.Id);
            }
        }

        // 「岗位 → 课程」：能力 → 课程支撑。一次取全，避免逐能力查库。
        var competencyToCourses = await GetCompetencyCourseMapAsync();

        return graphs.Select(g =>
        {
            var myTasks = taskByGraph.GetValueOrDefault(g.Id) ?? new List<JobCoreTask>();
            var competencyIds = myTasks
                .SelectMany(t => linkByTask.GetValueOrDefault(t.Id) ?? new List<JobTaskCompetency>())
                .Select(x => x.CompetencyId)
                .Distinct()
                .ToList();

            var job = g.JobPostingId.HasValue && postingsById.TryGetValue(g.JobPostingId.Value, out var posting)
                ? MapJobBrief(posting)
                : null;

            var supportingCourses = competencyIds
                .SelectMany(cid => competencyToCourses.GetValueOrDefault(cid) ?? new List<CourseCompetencySupport>())
                .Select(x => x.CourseId)
                .Distinct()
                .Count();

            return new JobCompetencyGraphDto
            {
                Id = g.Id,
                Title = g.Title,
                Description = g.Description,
                MajorId = g.MajorId,
                JobPostingId = g.JobPostingId,
                JobPostingTitle = job?.Title,
                Job = job,
                Version = g.Version,
                IsActive = g.IsActive,
                TaskCount = myTasks.Count,
                CompetencyCount = competencyIds.Count,
                MeasurableCompetencyCount = competencyIds.Count(id => competencyKnowledgeCount.GetValueOrDefault(id) > 0),
                CourseCount = supportingCourses
            };
        }).ToList();
    }

    public async Task<JobCompetencyGraphDetailDto> GetDetailAsync(Guid id, Guid? courseId = null)
    {
        JobCompetencyGraph graph;
        List<JobCoreTask> tasks;
        List<JobTaskCompetency> links;
        List<Competency> competencies;
        Dictionary<Guid, int> knowledgeCount;
        var competencyToCourses = await GetCompetencyCourseMapAsync();
        var courseTitles = await GetCourseTitleMapAsync();
        using (_dataFilter.Disable<IMultiTenant>())
        {
            graph = await _graphRepository.GetAsync(id);
            tasks = (await _taskRepository.GetListAsync(x => x.GraphId == id))
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ToList();
            var taskIds = tasks.Select(x => x.Id).ToList();
            links = taskIds.Count == 0
                ? new List<JobTaskCompetency>()
                : await _linkRepository.GetListAsync(x => taskIds.Contains(x.CoreTaskId));
            competencies = await _competencyRepository.GetListAsync();
            knowledgeCount = await GetCompetencyKnowledgeCountAsync();
        }

        var competencyById = competencies.ToDictionary(x => x.Id);
        var linkByTask = links.GroupBy(x => x.CoreTaskId).ToDictionary(g => g.Key, g => g.ToList());

        // 传了 courseId 才去算达成度（需要课程的能力支撑配置）
        Dictionary<Guid, double>? achievementByCompetency = null;
        if (courseId.HasValue)
        {
            var achievement = await _competencyAppService.GetAchievementAsync(courseId.Value);
            achievementByCompetency = achievement.Items
                .GroupBy(x => x.CompetencyId)
                .ToDictionary(g => g.Key, g => g.First().Achievement);
        }

        JobPosting? jobPosting = null;
        if (graph.JobPostingId.HasValue)
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                jobPosting = await _jobPostingRepository.FindAsync(graph.JobPostingId.Value);
            }
        }

        var detail = new JobCompetencyGraphDetailDto
        {
            Id = graph.Id,
            Title = graph.Title,
            Description = graph.Description,
            MajorId = graph.MajorId,
            JobPostingId = graph.JobPostingId,
            JobPostingTitle = jobPosting?.Title,
            Job = jobPosting == null ? null : MapJobBrief(jobPosting),
            Version = graph.Version,
            IsActive = graph.IsActive,
            CourseId = courseId,
            TaskCount = tasks.Count
        };

        foreach (var task in tasks)
        {
            var dto = new JobCoreTaskDto
            {
                Id = task.Id,
                GraphId = task.GraphId,
                Title = task.Title,
                Description = task.Description,
                Weight = task.Weight,
                SortOrder = task.SortOrder
            };

            foreach (var link in linkByTask.GetValueOrDefault(task.Id) ?? new List<JobTaskCompetency>())
            {
                competencyById.TryGetValue(link.CompetencyId, out var competency);
                dto.Competencies.Add(new JobTaskCompetencyDto
                {
                    Id = link.Id,
                    CoreTaskId = link.CoreTaskId,
                    CompetencyId = link.CompetencyId,
                    Code = competency?.Code,
                    Name = competency?.Name ?? "（能力项已删除）",
                    Weight = link.Weight,
                    RequiredLevel = link.RequiredLevel,
                    KnowledgeCount = knowledgeCount.GetValueOrDefault(link.CompetencyId),
                    Achievement = achievementByCompetency?.GetValueOrDefault(link.CompetencyId),
                    Courses = (competencyToCourses.GetValueOrDefault(link.CompetencyId) ?? new List<CourseCompetencySupport>())
                        .Select(x => new JobCourseSupportDto
                        {
                            CourseId = x.CourseId,
                            CourseTitle = courseTitles.GetValueOrDefault(x.CourseId) ?? "（课程已删除）",
                            Weight = x.Weight,
                            IsCurrent = courseId.HasValue && courseId.Value == x.CourseId
                        })
                        .OrderByDescending(x => x.IsCurrent)
                        .ThenByDescending(x => x.Weight)
                        .ToList()
                });
            }

            detail.Tasks.Add(dto);
        }

        var allCompetencyIds = detail.Tasks.SelectMany(t => t.Competencies).Select(x => x.CompetencyId).Distinct().ToList();
        detail.CompetencyCount = allCompetencyIds.Count;
        detail.CourseCount = allCompetencyIds
            .SelectMany(cid => competencyToCourses.GetValueOrDefault(cid) ?? new List<CourseCompetencySupport>())
            .Select(x => x.CourseId)
            .Distinct()
            .Count();

        // 每门课覆盖了本岗位多少能力
        detail.Courses = allCompetencyIds
            .SelectMany(cid => (competencyToCourses.GetValueOrDefault(cid) ?? new List<CourseCompetencySupport>())
                .Select(x => new { x.CourseId, CompetencyId = cid }))
            .GroupBy(x => x.CourseId)
            .Select(g => new JobCourseCoverageDto
            {
                CourseId = g.Key,
                CourseTitle = courseTitles.GetValueOrDefault(g.Key) ?? "（课程已删除）",
                SupportedCompetencyCount = g.Select(x => x.CompetencyId).Distinct().Count(),
                TotalCompetencyCount = allCompetencyIds.Count,
                Coverage = allCompetencyIds.Count == 0
                    ? 0
                    : (double)g.Select(x => x.CompetencyId).Distinct().Count() / allCompetencyIds.Count
            })
            .OrderByDescending(x => x.Coverage)
            .ThenBy(x => x.CourseTitle)
            .ToList();
        detail.MeasurableCompetencyCount = allCompetencyIds.Count(x => knowledgeCount.GetValueOrDefault(x) > 0);
        detail.OverallAchievement = achievementByCompetency == null
            ? null
            : detail.Tasks.SelectMany(t => t.Competencies)
                .Where(x => x.Achievement.HasValue)
                .Select(x => x.Achievement!.Value)
                .DefaultIfEmpty(0)
                .Average();

        return detail;
    }

    [Authorize(KnowledgeHubPermissions.Employment.ManageJobGraph)]
    public async Task<JobCompetencyGraphDto> CreateAsync(SaveJobCompetencyGraphInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("请填写岗位名称。");
        }

        var graph = new JobCompetencyGraph(GuidGenerator.Create(), input.Title.Trim())
        {
            Description = input.Description,
            MajorId = input.MajorId,
            JobPostingId = input.JobPostingId,
            Version = Math.Max(1, input.Version),
            IsActive = input.IsActive
        };

        await _graphRepository.InsertAsync(graph);
        if (CurrentUnitOfWork is not null) await CurrentUnitOfWork.SaveChangesAsync();

        return (await GetListAsync()).First(x => x.Id == graph.Id);
    }

    [Authorize(KnowledgeHubPermissions.Employment.ManageJobGraph)]
    public async Task<JobCompetencyGraphDto> UpdateAsync(Guid id, SaveJobCompetencyGraphInput input)
    {
        var graph = await _graphRepository.GetAsync(id);
        graph.Title = input.Title.Trim();
        graph.Description = input.Description;
        graph.MajorId = input.MajorId;
        graph.JobPostingId = input.JobPostingId;
        graph.Version = Math.Max(1, input.Version);
        graph.IsActive = input.IsActive;
        await _graphRepository.UpdateAsync(graph);
        if (CurrentUnitOfWork is not null) await CurrentUnitOfWork.SaveChangesAsync();

        return (await GetListAsync()).First(x => x.Id == graph.Id);
    }

    [Authorize(KnowledgeHubPermissions.Employment.ManageJobGraph)]
    public async Task DeleteAsync(Guid id)
    {
        var tasks = await _taskRepository.GetListAsync(x => x.GraphId == id);
        var taskIds = tasks.Select(x => x.Id).ToList();
        if (taskIds.Count > 0)
        {
            var links = await _linkRepository.GetListAsync(x => taskIds.Contains(x.CoreTaskId));
            foreach (var link in links)
            {
                await _linkRepository.DeleteAsync(link);
            }
        }

        foreach (var task in tasks)
        {
            await _taskRepository.DeleteAsync(task);
        }

        await _graphRepository.DeleteAsync(id);
    }

    // ── 核心任务 ────────────────────────────────────────────────

    [Authorize(KnowledgeHubPermissions.Employment.ManageJobGraph)]
    public async Task<JobCoreTaskDto> CreateTaskAsync(SaveJobCoreTaskInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("请填写任务名称。");
        }

        var graph = await _graphRepository.GetAsync(input.GraphId);
        var existing = await _taskRepository.CountAsync(x => x.GraphId == input.GraphId);

        var task = new JobCoreTask(GuidGenerator.Create(), input.GraphId, input.Title.Trim())
        {
            Description = input.Description,
            Weight = Math.Clamp(input.Weight <= 0 ? 1 : input.Weight, 0, 1),
            SortOrder = input.SortOrder > 0 ? input.SortOrder : (int)existing,
            TenantId = graph.TenantId
        };

        await _taskRepository.InsertAsync(task);
        if (CurrentUnitOfWork is not null) await CurrentUnitOfWork.SaveChangesAsync();

        return new JobCoreTaskDto
        {
            Id = task.Id,
            GraphId = task.GraphId,
            Title = task.Title,
            Description = task.Description,
            Weight = task.Weight,
            SortOrder = task.SortOrder
        };
    }

    [Authorize(KnowledgeHubPermissions.Employment.ManageJobGraph)]
    public async Task<JobCoreTaskDto> UpdateTaskAsync(Guid taskId, SaveJobCoreTaskInput input)
    {
        var task = await _taskRepository.GetAsync(taskId);
        task.Title = input.Title.Trim();
        task.Description = input.Description;
        task.Weight = Math.Clamp(input.Weight <= 0 ? 1 : input.Weight, 0, 1);
        task.SortOrder = input.SortOrder;
        await _taskRepository.UpdateAsync(task);

        return new JobCoreTaskDto
        {
            Id = task.Id,
            GraphId = task.GraphId,
            Title = task.Title,
            Description = task.Description,
            Weight = task.Weight,
            SortOrder = task.SortOrder
        };
    }

    [Authorize(KnowledgeHubPermissions.Employment.ManageJobGraph)]
    public async Task DeleteTaskAsync(Guid taskId)
    {
        var links = await _linkRepository.GetListAsync(x => x.CoreTaskId == taskId);
        foreach (var link in links)
        {
            await _linkRepository.DeleteAsync(link);
        }

        await _taskRepository.DeleteAsync(taskId);
    }

    // ── 任务 → 能力 ─────────────────────────────────────────────

    [Authorize(KnowledgeHubPermissions.Employment.ManageJobGraph)]
    public async Task<JobTaskCompetencyDto> LinkCompetencyAsync(LinkJobTaskCompetencyInput input)
    {
        var task = await _taskRepository.GetAsync(input.CoreTaskId);
        var competency = await _competencyRepository.FindAsync(input.CompetencyId)
                         ?? throw new UserFriendlyException("能力项不存在（请先在「能力体系」里创建）。");

        var existing = await _linkRepository.FirstOrDefaultAsync(x =>
            x.CoreTaskId == input.CoreTaskId && x.CompetencyId == input.CompetencyId);

        var weight = Math.Clamp(input.Weight <= 0 ? 1 : input.Weight, 0, 1);
        var level = Math.Clamp(input.RequiredLevel, 0, 2);

        if (existing != null)
        {
            existing.Weight = weight;
            existing.RequiredLevel = level;
            await _linkRepository.UpdateAsync(existing);
            return await MapLinkAsync(existing, competency);
        }

        var link = new JobTaskCompetency(GuidGenerator.Create(), input.CoreTaskId, input.CompetencyId)
        {
            Weight = weight,
            RequiredLevel = level,
            TenantId = task.TenantId
        };
        await _linkRepository.InsertAsync(link);
        if (CurrentUnitOfWork is not null) await CurrentUnitOfWork.SaveChangesAsync();

        return await MapLinkAsync(link, competency);
    }

    [Authorize(KnowledgeHubPermissions.Employment.ManageJobGraph)]
    public async Task UnlinkCompetencyAsync(Guid linkId)
    {
        await _linkRepository.DeleteAsync(linkId);
    }

    public async Task<List<JobOptionDto>> GetJobOptionsAsync(string? filter = null)
    {
        List<JobPosting> postings;
        List<JobCompetencyGraph> graphs;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var jobQuery = await _jobPostingRepository.GetQueryableAsync();
            postings = await AsyncExecuter.ToListAsync(
                jobQuery
                    .WhereIf(!string.IsNullOrWhiteSpace(filter),
                        x => x.Title.Contains(filter!) || (x.CompanyName != null && x.CompanyName.Contains(filter!)))
                    .OrderByDescending(x => x.PublishedAt ?? x.CreationTime)
                    .Take(300));

            graphs = await AsyncExecuter.ToListAsync(await _graphRepository.GetQueryableAsync());
        }

        var graphByJob = graphs
            .Where(x => x.JobPostingId.HasValue)
            .GroupBy(x => x.JobPostingId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Version).First());

        return postings.Select(p =>
        {
            var hasGraph = graphByJob.TryGetValue(p.Id, out var graph);
            return new JobOptionDto
            {
                Id = p.Id,
                Title = p.Title,
                CompanyName = p.CompanyName,
                SkillTags = p.SkillTags,
                StatusLabel = MapJobBrief(p).StatusLabel,
                HasGraph = hasGraph,
                GraphId = graph?.Id
            };
        }).ToList();
    }

    /// <summary>
    /// 从就业模块的岗位（JobPosting）建图谱：岗位不手填，直接选已有招聘岗位。
    /// 幂等：同一岗位已有图谱 → 直接返回详情，不重复创建。
    /// </summary>
    [Authorize(KnowledgeHubPermissions.Employment.ManageJobGraph)]
    public async Task<JobCompetencyGraphDetailDto> CreateFromJobAsync(Guid jobPostingId)
    {
        JobPosting? posting;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            posting = await _jobPostingRepository.FindAsync(jobPostingId);
        }

        if (posting == null)
        {
            throw new UserFriendlyException("岗位不存在（请先在「就业管理 → 岗位管理」里创建岗位）。");
        }

        JobCompetencyGraph? existing;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            existing = await _graphRepository.FirstOrDefaultAsync(x => x.JobPostingId == jobPostingId);
        }

        if (existing != null)
        {
            return await GetDetailAsync(existing.Id);
        }

        var graph = new JobCompetencyGraph(GuidGenerator.Create(), posting.Title)
        {
            // 岗位所属学校：优先岗位自身的租户
            TenantId = posting.TenantId,
            JobPostingId = posting.Id,
            Description = string.IsNullOrWhiteSpace(posting.Summary) ? posting.Description : posting.Summary,
            Version = 1,
            IsActive = true
        };

        await _graphRepository.InsertAsync(graph);
        if (CurrentUnitOfWork is not null) await CurrentUnitOfWork.SaveChangesAsync();

        return await GetDetailAsync(graph.Id);
    }

    // ── 覆盖度 ──────────────────────────────────────────────────

    public async Task<JobGraphCoverageDto> GetCoverageAsync(Guid id, Guid? courseId = null)
    {
        var detail = await GetDetailAsync(id, courseId);
        var knowledgeCount = await GetCompetencyKnowledgeCountAsync();

        var allLinks = detail.Tasks.SelectMany(t => t.Competencies).ToList();
        var competencyIds = allLinks.Select(x => x.CompetencyId).Distinct().ToList();
        var measurable = competencyIds.Where(x => knowledgeCount.GetValueOrDefault(x) > 0).ToList();

        var coverage = new JobGraphCoverageDto
        {
            GraphId = id,
            Title = detail.Title,
            TaskCount = detail.Tasks.Count,
            CompetencyCount = competencyIds.Count,
            MeasurableCompetencyCount = measurable.Count,
            CompetencyCoverage = competencyIds.Count == 0 ? 0 : (double)measurable.Count / competencyIds.Count,
            TasksWithoutCompetency = detail.Tasks.Count(t => t.Competencies.Count == 0),
            OverallAchievement = detail.OverallAchievement
        };

        // 缺口：被任务引用但还没有关联知识点的能力（老师最有用的待办提示）
        var missing = allLinks
            .Where(x => knowledgeCount.GetValueOrDefault(x.CompetencyId) == 0)
            .GroupBy(x => x.Name)
            .Select(g => g.Key)
            .Distinct()
            .ToList();
        coverage.Gaps = missing;

        return coverage;
    }

    // ── 内部 ────────────────────────────────────────────────────

    /// <summary>能力 → 支撑它的课程（课程侧 CourseCompetencySupport）。</summary>
    private async Task<Dictionary<Guid, List<CourseCompetencySupport>>> GetCompetencyCourseMapAsync()
    {
        List<CourseCompetencySupport> supports;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            supports = await AsyncExecuter.ToListAsync(await _courseCompetencyRepository.GetQueryableAsync());
        }

        return supports.GroupBy(x => x.CompetencyId).ToDictionary(g => g.Key, g => g.ToList());
    }

    private async Task<Dictionary<Guid, string>> GetCourseTitleMapAsync()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var courses = await _courseRepository.GetListAsync();
            return courses.ToDictionary(x => x.Id, x => x.Title);
        }
    }

    private static JobPostingBriefDto MapJobBrief(JobPosting posting) => new()
    {
        Id = posting.Id,
        Title = posting.Title,
        CompanyName = posting.CompanyName,
        Industry = posting.Industry,
        SalaryRange = posting.SalaryRange,
        Location = posting.Location,
        Status = (int)posting.Status,
        StatusLabel = posting.Status switch
        {
            EmploymentJobStatus.Draft => "草稿",
            EmploymentJobStatus.PendingReview => "待审核",
            EmploymentJobStatus.Published => "招聘中",
            EmploymentJobStatus.Rejected => "已驳回",
            EmploymentJobStatus.Closed => "已结束",
            _ => "未知"
        },
        SkillTags = posting.SkillTags,
        PublishedAt = posting.PublishedAt
    };

    /// <summary>能力 → 关联知识点数（一次取全，避免逐能力查库）。</summary>
    private async Task<Dictionary<Guid, int>> GetCompetencyKnowledgeCountAsync()
    {
        List<CompetencyKnowledgeLink> links;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            links = await AsyncExecuter.ToListAsync(await _competencyLinkRepository.GetQueryableAsync());
        }

        return links.GroupBy(x => x.CompetencyId).ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<JobTaskCompetencyDto> MapLinkAsync(JobTaskCompetency link, Competency competency)
    {
        var knowledgeCount = await GetCompetencyKnowledgeCountAsync();
        return new JobTaskCompetencyDto
        {
            Id = link.Id,
            CoreTaskId = link.CoreTaskId,
            CompetencyId = link.CompetencyId,
            Code = competency.Code,
            Name = competency.Name,
            Weight = link.Weight,
            RequiredLevel = link.RequiredLevel,
            KnowledgeCount = knowledgeCount.GetValueOrDefault(link.CompetencyId)
        };
    }
}
