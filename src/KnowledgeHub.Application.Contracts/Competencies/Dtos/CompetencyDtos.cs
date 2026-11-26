using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace KnowledgeHub.Competencies.Dtos;

/// <summary>
/// 能力体系（专业认证 / 岗位能力 的达成度）。
/// <para>
/// 数据链路：能力项（树）← <c>CourseCompetencySupport</c>（课程支撑强度）
/// ← <c>CompetencyKnowledgeLink</c>（能力 ↔ 课程知识点）← 学生掌握度（B1 统一算法）。
/// 所以「达成度」不是手填的，而是**从学生作答数据倒推出来的**。
/// </para>
/// </summary>
public interface ICompetencyAppService : IApplicationService
{
    /// <summary>能力树（按专业；majorId 为空时返回全校通用 + 所有专业的能力）。</summary>
    Task<List<CompetencyDto>> GetTreeAsync(Guid? majorId = null);

    Task<CompetencyDto> CreateAsync(SaveCompetencyInput input);
    Task<CompetencyDto> UpdateAsync(Guid id, SaveCompetencyInput input);

    /// <summary>删除能力（有子节点或有课程支撑时拒绝，避免把达成度口径删成孤儿）。</summary>
    Task DeleteAsync(Guid id);

    // ── 课程 ↔ 能力 ──
    /// <summary>某门课支撑的能力（含权重、关联知识点数、达成度）。</summary>
    Task<List<CourseCompetencyDto>> GetByCourseAsync(Guid courseId);

    /// <summary>覆盖式设置课程支撑的能力与权重（只保留传入项）。</summary>
    Task<List<CourseCompetencyDto>> SetCourseSupportAsync(SetCourseCompetencyInput input);

    // ── 能力 ↔ 知识点 ──
    Task<List<CompetencyKnowledgeDto>> GetKnowledgeLinksAsync(Guid competencyId);

    Task<CompetencyKnowledgeDto> LinkKnowledgeAsync(LinkCompetencyKnowledgeInput input);

    Task UnlinkKnowledgeAsync(Guid linkId);

    /// <summary>某门课里「已被能力引用」的知识点（用于在知识点树上打标记）。</summary>
    Task<List<Guid>> GetLinkedKnowledgeResourceIdsAsync(Guid courseId);

    // ── 达成度 ──
    /// <summary>课程维度的能力达成度（班级平均 + 有证据学生数 + 逐能力明细）。</summary>
    Task<CourseAchievementDto> GetAchievementAsync(Guid courseId);

    /// <summary>某个学生在某门课上的能力达成度。</summary>
    Task<StudentAchievementDto> GetStudentAchievementAsync(Guid courseId, Guid studentId);
}

public class CompetencyDto
{
    public Guid Id { get; set; }
    public Guid? MajorId { get; set; }
    public Guid? ParentId { get; set; }
    public int Level { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; }

    /// <summary>被多少门课程支撑。</summary>
    public int CourseCount { get; set; }

    /// <summary>关联的知识点数。</summary>
    public int KnowledgeCount { get; set; }

    public List<CompetencyDto> Children { get; set; } = new();
}

public class SaveCompetencyInput
{
    public Guid? MajorId { get; set; }
    public Guid? ParentId { get; set; }
    public int Level { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class CourseCompetencyDto
{
    public Guid CompetencyId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }

    /// <summary>本课对该能力的支撑强度。</summary>
    public double Weight { get; set; }

    /// <summary>本课里关联到该能力的知识点数。</summary>
    public int KnowledgeCount { get; set; }

    /// <summary>班级达成度（0~1，无证据时为 0）。</summary>
    public double Achievement { get; set; }

    /// <summary>参与统计的学生数（有作答证据的）。</summary>
    public int StudentCount { get; set; }

    public string? Note { get; set; }
}

public class SetCourseCompetencyInput
{
    public Guid CourseId { get; set; }
    public List<CourseCompetencyItemInput> Items { get; set; } = new();
}

public class CourseCompetencyItemInput
{
    public Guid CompetencyId { get; set; }
    public double Weight { get; set; } = 1;
    public string? Note { get; set; }
}

public class CompetencyKnowledgeDto
{
    public Guid Id { get; set; }
    public Guid CompetencyId { get; set; }
    public Guid KnowledgeResourceId { get; set; }
    public string? KnowledgeResourceName { get; set; }
    public Guid? ChapterId { get; set; }
    public double Weight { get; set; }
}

public class LinkCompetencyKnowledgeInput
{
    public Guid CompetencyId { get; set; }
    public Guid KnowledgeResourceId { get; set; }
    public double Weight { get; set; } = 1;
}

public class CourseAchievementDto
{
    public Guid CourseId { get; set; }
    public string? CourseName { get; set; }

    /// <summary>参与统计的学生数（本课有任一行掌握度且有证据）。</summary>
    public int StudentCount { get; set; }

    /// <summary>关联了知识点的能力项。</summary>
    public int MeasurableCount { get; set; }

    /// <summary>整体达成度（按支撑权重加权）。</summary>
    public double OverallAchievement { get; set; }

    public List<CourseCompetencyDto> Items { get; set; } = new();
}

public class StudentAchievementDto
{
    public Guid CourseId { get; set; }
    public Guid StudentId { get; set; }
    public string? StudentName { get; set; }
    public double OverallAchievement { get; set; }
    public List<StudentCompetencyAchievementDto> Items { get; set; } = new();
}

public class StudentCompetencyAchievementDto
{
    public Guid CompetencyId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Weight { get; set; }
    public double Achievement { get; set; }

    /// <summary>支撑该能力的知识点里，有多少个已有作答证据。</summary>
    public int EvidencedKnowledgeCount { get; set; }

    public int KnowledgeCount { get; set; }
}
