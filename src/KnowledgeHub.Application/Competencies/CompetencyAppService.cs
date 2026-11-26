using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Competencies.Dtos;
using KnowledgeHub.Courses;
using KnowledgeHub.Learning;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Competencies;

/// <summary>
/// 能力体系：能力项维护 + 课程支撑 + 能力-知识点关联 + **达成度计算**。
/// <para>
/// 达成度口径（务必与学情一致）：
/// <list type="bullet">
/// <item>数据源只有一处 —— <c>Learning.KnowledgeMastery</c>（B1 的 Beta 先验算法写入），不再自己算一遍；</item>
/// <item>能力达成度 = Σ(知识点掌握度 × 关联权重) / Σ(权重)，**只统计有证据的知识点**；</item>
/// <item>没有证据时不判 0 分，而是标记 <c>EvidencedKnowledgeCount = 0</c>，前端显示"数据不足"；</item>
/// <item>课程对能力的整体贡献 = 各能力达成度按 <c>SupportWeight</c> 加权。</item>
/// </list>
/// </para>
/// </summary>
[IgnoreAntiforgeryToken]
[Authorize]
public class CompetencyAppService : KnowledgeHubAppService, ICompetencyAppService
{
    private readonly IRepository<Competency, Guid> _competencyRepository;
    private readonly IRepository<CourseCompetencySupport, Guid> _supportRepository;
    private readonly IRepository<CompetencyKnowledgeLink, Guid> _linkRepository;
    private readonly IRepository<KnowledgeResource, Guid> _knowledgeResourceRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<KnowledgeMastery, Guid> _masteryRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentUser _currentUser;

    public CompetencyAppService(
        IRepository<Competency, Guid> competencyRepository,
        IRepository<CourseCompetencySupport, Guid> supportRepository,
        IRepository<CompetencyKnowledgeLink, Guid> linkRepository,
        IRepository<KnowledgeResource, Guid> knowledgeResourceRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<KnowledgeMastery, Guid> masteryRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IDataFilter dataFilter,
        ICurrentUser currentUser)
    {
        _competencyRepository = competencyRepository;
        _supportRepository = supportRepository;
        _linkRepository = linkRepository;
        _knowledgeResourceRepository = knowledgeResourceRepository;
        _courseRepository = courseRepository;
        _masteryRepository = masteryRepository;
        _studentCourseRepository = studentCourseRepository;
        _userRepository = userRepository;
        _dataFilter = dataFilter;
        _currentUser = currentUser;
    }

    // ══════════════════════════════════════════════════════════
    //  能力项（树）
    // ══════════════════════════════════════════════════════════

    public async Task<List<CompetencyDto>> GetTreeAsync(Guid? majorId = null)
    {
        List<Competency> all;
        List<CourseCompetencySupport> supports;
        List<CompetencyKnowledgeLink> links;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _competencyRepository.GetQueryableAsync();
            all = await AsyncExecuter.ToListAsync(
                query.WhereIf(majorId.HasValue, x => x.MajorId == majorId.Value)
                     .OrderBy(x => x.SortOrder)
                     .ThenBy(x => x.Code));

            supports = await AsyncExecuter.ToListAsync(await _supportRepository.GetQueryableAsync());
            links = await AsyncExecuter.ToListAsync(await _linkRepository.GetQueryableAsync());
        }

        var courseCountByCompetency = supports.GroupBy(x => x.CompetencyId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.CourseId).Distinct().Count());
        var linkCountByCompetency = links.GroupBy(x => x.CompetencyId)
            .ToDictionary(g => g.Key, g => g.Count());

        var dtos = all.Select(c => new CompetencyDto
        {
            Id = c.Id,
            MajorId = c.MajorId,
            ParentId = c.ParentId,
            Level = c.Level,
            Code = c.Code,
            Name = c.Name,
            Description = c.Description,
            SortOrder = c.SortOrder,
            IsEnabled = c.IsEnabled,
            CourseCount = courseCountByCompetency.GetValueOrDefault(c.Id),
            // 能力的关联知识点数 = 它自己 + 所有子孙的（达成度按子树汇总，展示口径要一致）
            KnowledgeCount = linkCountByCompetency.GetValueOrDefault(c.Id)
        }).ToList();

        return BuildTree(dtos);
    }

    [Authorize(KnowledgeHubPermissions.Competencies.Manage)]
    public async Task<CompetencyDto> CreateAsync(SaveCompetencyInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new UserFriendlyException("请填写能力名称。");
        }

        if (input.ParentId.HasValue)
        {
            var parent = await _competencyRepository.FindAsync(input.ParentId.Value)
                ?? throw new UserFriendlyException("上级能力不存在。");
            if (parent.MajorId != input.MajorId)
            {
                throw new UserFriendlyException("上级能力与当前专业不一致。");
            }
        }

        var entity = new Competency(GuidGenerator.Create(), input.Name.Trim())
        {
            MajorId = input.MajorId,
            ParentId = input.ParentId,
            Level = Math.Clamp(input.Level, 0, 5),
            Code = string.IsNullOrWhiteSpace(input.Code) ? null : input.Code.Trim(),
            Description = input.Description,
            SortOrder = input.SortOrder,
            IsEnabled = input.IsEnabled
        };

        await _competencyRepository.InsertAsync(entity);
        return MapDto(entity);
    }

    [Authorize(KnowledgeHubPermissions.Competencies.Manage)]
    public async Task<CompetencyDto> UpdateAsync(Guid id, SaveCompetencyInput input)
    {
        var entity = await _competencyRepository.GetAsync(id);

        if (input.ParentId.HasValue)
        {
            if (input.ParentId.Value == id)
            {
                throw new UserFriendlyException("不能把能力挂到它自己下面。");
            }

            await EnsureNotDescendantAsync(id, input.ParentId.Value);
        }

        entity.ParentId = input.ParentId;
        entity.Level = Math.Clamp(input.Level, 0, 5);
        entity.Code = string.IsNullOrWhiteSpace(input.Code) ? null : input.Code.Trim();
        entity.Name = input.Name.Trim();
        entity.Description = input.Description;
        entity.SortOrder = input.SortOrder;
        entity.IsEnabled = input.IsEnabled;

        await _competencyRepository.UpdateAsync(entity);
        return MapDto(entity);
    }

    [Authorize(KnowledgeHubPermissions.Competencies.Manage)]
    public async Task DeleteAsync(Guid id)
    {
        var children = await _competencyRepository.GetListAsync(x => x.ParentId == id);
        if (children.Count > 0)
        {
            throw new UserFriendlyException("该能力下还有子能力，请先删除或移动子能力。");
        }

        var supports = await _supportRepository.GetListAsync(x => x.CompetencyId == id);
        if (supports.Count > 0)
        {
            throw new UserFriendlyException($"该能力已被 {supports.Count} 门课程支撑，请先在课程里取消支撑。");
        }

        var links = await _linkRepository.GetListAsync(x => x.CompetencyId == id);
        foreach (var link in links)
        {
            await _linkRepository.DeleteAsync(link);
        }

        await _competencyRepository.DeleteAsync(id);
    }

    // ══════════════════════════════════════════════════════════
    //  课程 ↔ 能力
    // ══════════════════════════════════════════════════════════

    public async Task<List<CourseCompetencyDto>> GetByCourseAsync(Guid courseId)
    {
        var achievement = await GetAchievementAsync(courseId);
        return achievement.Items;
    }

    [Authorize(KnowledgeHubPermissions.Competencies.Manage)]
    public async Task<List<CourseCompetencyDto>> SetCourseSupportAsync(SetCourseCompetencyInput input)
    {
        Course? course;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            course = await _courseRepository.FindAsync(input.CourseId);
        }

        if (course == null)
        {
            throw new UserFriendlyException("课程不存在。");
        }

        var existing = await _supportRepository.GetListAsync(x => x.CourseId == input.CourseId);
        var keep = input.Items.Where(x => x.CompetencyId != Guid.Empty).GroupBy(x => x.CompetencyId)
            .ToDictionary(g => g.Key, g => g.First());
        var keepIds = keep.Keys.ToHashSet();

        // 覆盖式：不在入参里的移除
        foreach (var row in existing.Where(x => !keepIds.Contains(x.CompetencyId)))
        {
            await _supportRepository.DeleteAsync(row);
        }

        var existingByCompetency = existing.ToDictionary(x => x.CompetencyId);
        foreach (var (competencyId, item) in keep)
        {
            var weight = Math.Clamp(item.Weight, 0, 1);
            if (existingByCompetency.TryGetValue(competencyId, out var row))
            {
                row.Weight = weight;
                row.Note = item.Note;
                await _supportRepository.UpdateAsync(row);
            }
            else
            {
                await _supportRepository.InsertAsync(new CourseCompetencySupport(
                    GuidGenerator.Create(), input.CourseId, competencyId)
                {
                    Weight = weight,
                    Note = item.Note,
                    TenantId = course.TenantId
                });
            }
        }

        if (CurrentUnitOfWork is not null)
        {
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        return await GetByCourseAsync(input.CourseId);
    }

    // ══════════════════════════════════════════════════════════
    //  能力 ↔ 知识点
    // ══════════════════════════════════════════════════════════

    public async Task<List<CompetencyKnowledgeDto>> GetKnowledgeLinksAsync(Guid competencyId)
    {
        List<CompetencyKnowledgeLink> links;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            links = await _linkRepository.GetListAsync(x => x.CompetencyId == competencyId);
        }

        if (links.Count == 0)
        {
            return new List<CompetencyKnowledgeDto>();
        }

        var resourceIds = links.Select(x => x.KnowledgeResourceId).Distinct().ToList();
        var resources = await _knowledgeResourceRepository.GetListAsync(x => resourceIds.Contains(x.Id));
        var byId = resources.ToDictionary(x => x.Id);

        return links.Select(l => new CompetencyKnowledgeDto
        {
            Id = l.Id,
            CompetencyId = l.CompetencyId,
            KnowledgeResourceId = l.KnowledgeResourceId,
            KnowledgeResourceName = byId.GetValueOrDefault(l.KnowledgeResourceId)?.Name,
            ChapterId = byId.GetValueOrDefault(l.KnowledgeResourceId)?.ChapterId,
            Weight = l.Weight
        }).OrderBy(x => x.KnowledgeResourceName).ToList();
    }

    [Authorize(KnowledgeHubPermissions.Competencies.Manage)]
    public async Task<CompetencyKnowledgeDto> LinkKnowledgeAsync(LinkCompetencyKnowledgeInput input)
    {
        var competency = await _competencyRepository.GetAsync(input.CompetencyId);
        var resource = await _knowledgeResourceRepository.FindAsync(input.KnowledgeResourceId)
                       ?? throw new UserFriendlyException("课程知识点不存在。");

        // 同一份 (能力, 知识点) 只保留一行：重复点选＝更新权重
        var existing = await _linkRepository.FirstOrDefaultAsync(x =>
            x.CompetencyId == input.CompetencyId && x.KnowledgeResourceId == input.KnowledgeResourceId);

        if (existing != null)
        {
            existing.Weight = Math.Max(0.01, input.Weight);
            await _linkRepository.UpdateAsync(existing);
            return MapLink(existing, resource);
        }

        var link = new CompetencyKnowledgeLink(
            GuidGenerator.Create(), input.CompetencyId, input.KnowledgeResourceId)
        {
            Weight = Math.Max(0.01, input.Weight),
            TenantId = competency.TenantId
        };
        await _linkRepository.InsertAsync(link);
        return MapLink(link, resource);
    }

    [Authorize(KnowledgeHubPermissions.Competencies.Manage)]
    public async Task UnlinkKnowledgeAsync(Guid linkId)
    {
        await _linkRepository.DeleteAsync(linkId);
    }

    public async Task<List<Guid>> GetLinkedKnowledgeResourceIdsAsync(Guid courseId)
    {
        var resources = await _knowledgeResourceRepository.GetListAsync(x => x.CourseId == courseId);
        if (resources.Count == 0)
        {
            return new List<Guid>();
        }

        var ids = resources.Select(x => x.Id).ToList();
        List<CompetencyKnowledgeLink> links;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            links = await _linkRepository.GetListAsync(x => ids.Contains(x.KnowledgeResourceId));
        }

        return links.Select(x => x.KnowledgeResourceId).Distinct().ToList();
    }

    // ══════════════════════════════════════════════════════════
    //  达成度
    // ══════════════════════════════════════════════════════════

    public async Task<CourseAchievementDto> GetAchievementAsync(Guid courseId)
    {
        var course = await FindCourseAsync(courseId);
        var supports = await _supportRepository.GetListAsync(x => x.CourseId == courseId);

        var result = new CourseAchievementDto
        {
            CourseId = courseId,
            CourseName = course?.Title
        };

        if (supports.Count == 0)
        {
            return result;
        }

        // 本课知识点 + 能力关联 + 全班掌握度（一次取全，避免逐能力查库）
        var resources = await _knowledgeResourceRepository.GetListAsync(x => x.CourseId == courseId);
        var resourceById = resources.ToDictionary(x => x.Id);
        var resourceIds = resourceById.Keys.ToList();

        List<CompetencyKnowledgeLink> links;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            links = resourceIds.Count == 0
                ? new List<CompetencyKnowledgeLink>()
                : await _linkRepository.GetListAsync(x => resourceIds.Contains(x.KnowledgeResourceId));
        }

        var masteryByStudentAndResource = await LoadMasteryAsync(courseId, resourceIds);

        var competencyIds = supports.Select(x => x.CompetencyId).ToList();
        List<Competency> competencies;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            competencies = await _competencyRepository.GetListAsync(x => competencyIds.Contains(x.Id));
        }

        var competencyById = competencies.ToDictionary(x => x.Id);

        foreach (var support in supports.OrderByDescending(x => x.Weight))
        {
            if (!competencyById.TryGetValue(support.CompetencyId, out var competency))
            {
                continue;
            }

            var myLinks = links.Where(x => x.CompetencyId == support.CompetencyId).ToList();
            var (avg, students) = AverageAchievement(masteryByStudentAndResource, myLinks);

            result.Items.Add(new CourseCompetencyDto
            {
                CompetencyId = competency.Id,
                Code = competency.Code,
                Name = competency.Name,
                Level = competency.Level,
                Weight = support.Weight,
                KnowledgeCount = myLinks.Count,
                Achievement = avg,
                StudentCount = students,
                Note = support.Note
            });
        }

        result.MeasurableCount = result.Items.Count(x => x.KnowledgeCount > 0);
        result.StudentCount = masteryByStudentAndResource.Count;

        var weightSum = result.Items.Where(x => x.KnowledgeCount > 0).Sum(x => x.Weight);
        result.OverallAchievement = weightSum <= 0
            ? 0
            : result.Items.Where(x => x.KnowledgeCount > 0).Sum(x => x.Achievement * x.Weight) / weightSum;

        return result;
    }

    public async Task<StudentAchievementDto> GetStudentAchievementAsync(Guid courseId, Guid studentId)
    {
        var course = await FindCourseAsync(courseId);
        var resources = await _knowledgeResourceRepository.GetListAsync(x => x.CourseId == courseId);
        var resourceIds = resources.Select(x => x.Id).ToList();

        List<CompetencyKnowledgeLink> links;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            links = resourceIds.Count == 0
                ? new List<CompetencyKnowledgeLink>()
                : await _linkRepository.GetListAsync(x => resourceIds.Contains(x.KnowledgeResourceId));
        }

        var supports = await _supportRepository.GetListAsync(x => x.CourseId == courseId);
        var mastery = await LoadMasteryForStudentAsync(studentId, resourceIds);

        var result = new StudentAchievementDto
        {
            CourseId = courseId,
            StudentId = studentId,
            StudentName = await GetStudentNameAsync(studentId)
        };

        var competencyIds = supports.Select(x => x.CompetencyId).ToList();
        if (competencyIds.Count == 0)
        {
            return result;
        }

        List<Competency> competencies;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            competencies = await _competencyRepository.GetListAsync(x => competencyIds.Contains(x.Id));
        }

        var byId = competencies.ToDictionary(x => x.Id);
        var weightSum = 0d;
        var weighted = 0d;

        foreach (var support in supports)
        {
            if (!byId.TryGetValue(support.CompetencyId, out var competency))
            {
                continue;
            }

            var myLinks = links.Where(x => x.CompetencyId == support.CompetencyId).ToList();
            var (achievement, evidenced) = SingleStudentAchievement(mastery, myLinks);

            result.Items.Add(new StudentCompetencyAchievementDto
            {
                CompetencyId = competency.Id,
                Code = competency.Code,
                Name = competency.Name,
                Weight = support.Weight,
                Achievement = achievement,
                KnowledgeCount = myLinks.Count,
                EvidencedKnowledgeCount = evidenced
            });

            if (evidenced > 0)
            {
                weightSum += support.Weight;
                weighted += achievement * support.Weight;
            }
        }

        result.OverallAchievement = weightSum <= 0 ? 0 : weighted / weightSum;
        return result;
    }

    // ── 内部 ─────────────────────────────────────────────────────

    /// <summary>全班 × 本课知识点 的掌握度（只取有证据的行）。</summary>
    private async Task<Dictionary<Guid, Dictionary<Guid, double>>> LoadMasteryAsync(
        Guid courseId, List<Guid> resourceIds)
    {
        if (resourceIds.Count == 0)
        {
            return new Dictionary<Guid, Dictionary<Guid, double>>();
        }

        var studentIds = await GetEnrolledStudentIdsAsync(courseId);

        List<KnowledgeMastery> rows;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            rows = await _masteryRepository.GetListAsync(x =>
                resourceIds.Contains(x.KnowledgeResourceId) &&
                x.Evidence > 0 &&
                studentIds.Contains(x.StudentId));
        }

        return rows
            .GroupBy(x => x.StudentId)
            .ToDictionary(
                g => g.Key,
                g => g.ToDictionary(x => x.KnowledgeResourceId, x => x.Mastery));
    }

    private async Task<Dictionary<Guid, double>> LoadMasteryForStudentAsync(Guid studentId, List<Guid> resourceIds)
    {
        if (resourceIds.Count == 0)
        {
            return new Dictionary<Guid, double>();
        }

        List<KnowledgeMastery> rows;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            rows = await _masteryRepository.GetListAsync(x =>
                x.StudentId == studentId &&
                resourceIds.Contains(x.KnowledgeResourceId) &&
                x.Evidence > 0);
        }

        return rows.ToDictionary(x => x.KnowledgeResourceId, x => x.Mastery);
    }

    /// <summary>班级达成度：先算每个有证据学生的达成度，再取平均（不是把所有行混在一起平均）。</summary>
    private static (double Average, int StudentCount) AverageAchievement(
        Dictionary<Guid, Dictionary<Guid, double>> masteryByStudent,
        List<CompetencyKnowledgeLink> links)
    {
        if (links.Count == 0 || masteryByStudent.Count == 0)
        {
            return (0, 0);
        }

        var total = 0d;
        var counted = 0;
        foreach (var (_, mastery) in masteryByStudent)
        {
            var (achievement, evidenced) = SingleStudentAchievement(mastery, links);
            if (evidenced == 0)
            {
                continue;
            }

            total += achievement;
            counted++;
        }

        return counted == 0 ? (0, 0) : (total / counted, counted);
    }

    private static (double Achievement, int EvidencedCount) SingleStudentAchievement(
        Dictionary<Guid, double> mastery,
        List<CompetencyKnowledgeLink> links)
    {
        var weightSum = 0d;
        var weighted = 0d;
        var evidenced = 0;

        foreach (var link in links)
        {
            if (!mastery.TryGetValue(link.KnowledgeResourceId, out var value))
            {
                continue; // 无证据的知识点不参与（不按 0 分拉低）
            }

            weightSum += link.Weight;
            weighted += value * link.Weight;
            evidenced++;
        }

        return weightSum <= 0 ? (0, 0) : (weighted / weightSum, evidenced);
    }

    private async Task<List<Guid>> GetEnrolledStudentIdsAsync(Guid courseId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _studentCourseRepository.GetQueryableAsync();
            return await AsyncExecuter.ToListAsync(
                query.Where(x => x.CourseId == courseId && x.Status != Learning.Enums.StudentCourseStatus.Dropped)
                     .Select(x => x.StudentId));
        }
    }

    private async Task<Course?> FindCourseAsync(Guid courseId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await _courseRepository.FindAsync(courseId);
        }
    }

    private async Task<string?> GetStudentNameAsync(Guid studentId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var user = await _userRepository.FindAsync(studentId);
            return user == null ? null : (string.IsNullOrWhiteSpace(user.Name) ? user.UserName : user.Name);
        }
    }

    /// <summary>防环：目标父节点不能是自己的子孙。</summary>
    private async Task EnsureNotDescendantAsync(Guid id, Guid candidateParentId)
    {
        var all = await _competencyRepository.GetListAsync();
        var byId = all.ToDictionary(x => x.Id);
        var cursor = byId.GetValueOrDefault(candidateParentId);
        var guard = 0;

        while (cursor != null && guard++ <= 16)
        {
            if (cursor.Id == id)
            {
                throw new UserFriendlyException("不能把能力移动到它自己的子节点下。");
            }

            cursor = cursor.ParentId.HasValue ? byId.GetValueOrDefault(cursor.ParentId.Value) : null;
        }
    }

    private static List<CompetencyDto> BuildTree(List<CompetencyDto> flat)
    {
        var byParent = flat.GroupBy(x => x.ParentId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SortOrder).ThenBy(x => x.Code).ThenBy(x => x.Name).ToList());

        List<CompetencyDto> Walk(Guid parentKey, int depth)
        {
            if (depth > 16 || !byParent.TryGetValue(parentKey, out var children))
            {
                return new List<CompetencyDto>();
            }

            foreach (var child in children)
            {
                child.Children = Walk(child.Id, depth + 1);
            }

            return children;
        }

        var roots = Walk(Guid.Empty, 0);

        // 汇总子能力到主能力的知识点数/课程数（展示时一级能力也能看出规模）
        void Rollup(CompetencyDto node)
        {
            foreach (var child in node.Children)
            {
                Rollup(child);
            }

            if (node.Children.Count > 0)
            {
                node.KnowledgeCount = Math.Max(node.KnowledgeCount, node.Children.Sum(x => x.KnowledgeCount));
                node.CourseCount = Math.Max(node.CourseCount, node.Children.Max(x => x.CourseCount));
            }
        }

        foreach (var root in roots)
        {
            Rollup(root);
        }

        return roots;
    }

    private static CompetencyDto MapDto(Competency c) => new()
    {
        Id = c.Id,
        MajorId = c.MajorId,
        ParentId = c.ParentId,
        Level = c.Level,
        Code = c.Code,
        Name = c.Name,
        Description = c.Description,
        SortOrder = c.SortOrder,
        IsEnabled = c.IsEnabled
    };

    private static CompetencyKnowledgeDto MapLink(CompetencyKnowledgeLink link, KnowledgeResource resource) => new()
    {
        Id = link.Id,
        CompetencyId = link.CompetencyId,
        KnowledgeResourceId = link.KnowledgeResourceId,
        KnowledgeResourceName = resource.Name,
        ChapterId = resource.ChapterId,
        Weight = link.Weight
    };
}
