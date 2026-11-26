using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Employment.Dtos;

/// <summary>
/// 岗位能力图谱（岗位 → 核心任务 → 能力 → 课程知识点 → 掌握度）。
///
/// <para>
/// 来源：kg-edu 的 <c>job_competency_graph / job_core_task / job_task_ability / ability_knowledge_link</c>。
/// 移植时做了一处关键简化：**能力项复用 <c>Competencies.Competency</c>**，
/// 不再单独维护"岗位能力"表 —— 否则岗位能力和毕业要求指标点会成为两套互不相干的概念，
/// 达成度也就无从计算（知识点只挂在能力体系那一边）。
/// </para>
/// </summary>
public interface IJobCompetencyGraphAppService : IApplicationService
{
    Task<List<JobCompetencyGraphDto>> GetListAsync(Guid? majorId = null, string? filter = null, Guid? jobPostingId = null);

    /// <summary>图谱详情：岗位 + 核心任务 + 每项任务要求的能力（含知识点数与达成度）。</summary>
    Task<JobCompetencyGraphDetailDto> GetDetailAsync(Guid id, Guid? courseId = null);

    Task<JobCompetencyGraphDto> CreateAsync(SaveJobCompetencyGraphInput input);

    /// <summary>
    /// 岗位候选（图谱入口用）：列出就业模块里的岗位 + 是否已建图谱。
    /// 刻意不受 <c>Employment.PublishJob</c> 限制 —— 老师没有发布岗位的权限，但需要能挑岗位建图谱。
    /// </summary>
    Task<List<JobOptionDto>> GetJobOptionsAsync(string? filter = null);

    /// <summary>
    /// 从**就业模块已有岗位**（JobPosting）创建图谱（幂等：同一岗位已有图谱则直接返回）。
    /// 这是「岗位图谱连通已有数据」的入口：岗位不手填，从岗位库选。
    /// </summary>
    Task<JobCompetencyGraphDetailDto> CreateFromJobAsync(Guid jobPostingId);

    Task<JobCompetencyGraphDto> UpdateAsync(Guid id, SaveJobCompetencyGraphInput input);

    Task DeleteAsync(Guid id);

    // ── 核心任务 ──
    Task<JobCoreTaskDto> CreateTaskAsync(SaveJobCoreTaskInput input);

    Task<JobCoreTaskDto> UpdateTaskAsync(Guid taskId, SaveJobCoreTaskInput input);

    Task DeleteTaskAsync(Guid taskId);

    // ── 任务 → 能力 ──
    Task<JobTaskCompetencyDto> LinkCompetencyAsync(LinkJobTaskCompetencyInput input);

    Task UnlinkCompetencyAsync(Guid linkId);

    /// <summary>覆盖度统计：能力覆盖率、有知识点的能力占比、（可选）课程达成度。</summary>
    Task<JobGraphCoverageDto> GetCoverageAsync(Guid id, Guid? courseId = null);
}

/// <summary>岗位图谱入口用的岗位候选。</summary>
public class JobOptionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? SkillTags { get; set; }
    public string StatusLabel { get; set; } = string.Empty;

    /// <summary>该岗位是否已经有图谱（有则前端提示"打开"而不是"创建"）。</summary>
    public bool HasGraph { get; set; }
    public Guid? GraphId { get; set; }
}

/// <summary>关联岗位的精简信息（来自就业模块的 JobPosting）。</summary>
public class JobPostingBriefDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Industry { get; set; }
    public string? SalaryRange { get; set; }
    public string? Location { get; set; }
    public int Status { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public string? SkillTags { get; set; }
    public DateTime? PublishedAt { get; set; }
}

/// <summary>某能力由哪门课程支撑（课程侧来自 <c>Competencies.CourseCompetencySupport</c>）。</summary>
public class JobCourseSupportDto
{
    public Guid CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;

    /// <summary>课程对该能力的支撑强度（0~1）。</summary>
    public double Weight { get; set; }

    /// <summary>当前查询指定的课程（前端高亮用）。</summary>
    public bool IsCurrent { get; set; }
}

/// <summary>某门课程覆盖了本岗位多少能力（「课程 → 岗位」方向）。</summary>
public class JobCourseCoverageDto
{
    public Guid CourseId { get; set; }
    public string CourseTitle { get; set; } = string.Empty;

    /// <summary>该课程支撑的岗位能力数。</summary>
    public int SupportedCompetencyCount { get; set; }

    /// <summary>岗位能力总数（分母）。</summary>
    public int TotalCompetencyCount { get; set; }

    /// <summary>覆盖比例。</summary>
    public double Coverage { get; set; }
}

public class JobCompetencyGraphDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? MajorId { get; set; }
    public Guid? JobPostingId { get; set; }
    public string? JobPostingTitle { get; set; }

    /// <summary>关联岗位详情（就业模块的 JobPosting）。</summary>
    public JobPostingBriefDto? Job { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }

    /// <summary>核心任务数。</summary>
    public int TaskCount { get; set; }

    /// <summary>涉及的能力项数（去重）。</summary>
    public int CompetencyCount { get; set; }

    /// <summary>有知识点的能力数（能算达成度的能力）。</summary>
    public int MeasurableCompetencyCount { get; set; }

    /// <summary>支撑本岗位能力的课程数（去重）。</summary>
    public int CourseCount { get; set; }
}

public class JobCompetencyGraphDetailDto : JobCompetencyGraphDto
{
    public List<JobCoreTaskDto> Tasks { get; set; } = new();

    /// <summary>支撑本岗位能力的课程（按覆盖能力数降序）——「岗位 → 课程」方向。</summary>
    public List<JobCourseCoverageDto> Courses { get; set; } = new();

    /// <summary>调用时是否带了 courseId（决定有没有达成度数据）。</summary>
    public Guid? CourseId { get; set; }
    public double? OverallAchievement { get; set; }
}

public class JobCoreTaskDto
{
    public Guid Id { get; set; }
    public Guid GraphId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Weight { get; set; }
    public int SortOrder { get; set; }
    public List<JobTaskCompetencyDto> Competencies { get; set; } = new();
}

public class JobTaskCompetencyDto
{
    public Guid Id { get; set; }
    public Guid CoreTaskId { get; set; }
    public Guid CompetencyId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Weight { get; set; }

    /// <summary>要求等级：0 了解 / 1 熟练 / 2 精通。</summary>
    public int RequiredLevel { get; set; }

    /// <summary>该能力关联的课程知识点数（0 表示还没法算达成度）。</summary>
    public int KnowledgeCount { get; set; }

    /// <summary>传入 courseId 时：该课程对该能力的达成度。</summary>
    public double? Achievement { get; set; }

    /// <summary>由哪些课程支撑这个能力（课程侧配置）；空表示还没有课程支撑它。</summary>
    public List<JobCourseSupportDto> Courses { get; set; } = new();
}

public class SaveJobCompetencyGraphInput
{
    /// <summary>关联的岗位（就业模块 JobPosting）。新建时建议必填 —— 图谱要挂在真实岗位上。</summary>
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? MajorId { get; set; }
    public Guid? JobPostingId { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

public class SaveJobCoreTaskInput
{
    public Guid GraphId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Weight { get; set; } = 1;
    public int SortOrder { get; set; }
}

public class LinkJobTaskCompetencyInput
{
    public Guid CoreTaskId { get; set; }
    public Guid CompetencyId { get; set; }
    public double Weight { get; set; } = 1;
    public int RequiredLevel { get; set; } = 1;
}

public class JobGraphCoverageDto
{
    public Guid GraphId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TaskCount { get; set; }
    public int CompetencyCount { get; set; }

    /// <summary>已关联课程知识点的能力数。</summary>
    public int MeasurableCompetencyCount { get; set; }

    /// <summary>能力覆盖率（Measurable / Total）。</summary>
    public double CompetencyCoverage { get; set; }

    /// <summary>没有挂任何能力的核心任务数（图谱缺口，提示老师补）。</summary>
    public int TasksWithoutCompetency { get; set; }

    /// <summary>传入 courseId 时的整体达成度。</summary>
    public double? OverallAchievement { get; set; }

    /// <summary>未覆盖的能力（没有知识点的、或没有任何任务引用的）。</summary>
    public List<string> Gaps { get; set; } = new();
}
