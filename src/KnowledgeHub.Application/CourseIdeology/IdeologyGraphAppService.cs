using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.CourseIdeology.Dtos;
using KnowledgeHub.CourseIdeology.Enums;
using KnowledgeHub.Courses;
using KnowledgeHub.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.CourseIdeology;

/// <summary>
/// 思政图谱：章节 ⇄ 案例 ⇄ 思政元素三层关联。
///
/// <para>只读接口 —— 前端"手工增删关联"实际调用案例的编辑接口
/// （改章节 = 改案例的 ChapterId；改元素 = 改案例的 ElementIds），
/// 保持数据只有一份，图谱永远是真实数据的投影。</para>
/// </summary>
[Authorize(KnowledgeHubPermissions.CourseIdeology.Default)]
public class IdeologyGraphAppService : KnowledgeHubAppService, IIdeologyGraphAppService
{
    private readonly IRepository<IdeologyCase, Guid> _caseRepository;
    private readonly IRepository<IdeologyElement, Guid> _elementRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;

    public IdeologyGraphAppService(
        IRepository<IdeologyCase, Guid> caseRepository,
        IRepository<IdeologyElement, Guid> elementRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository)
    {
        _caseRepository = caseRepository;
        _elementRepository = elementRepository;
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
    }

    public async Task<IdeologyGraphDto> GetAsync(Guid courseId)
    {
        var course = await _courseRepository.FindAsync(courseId)
                     ?? throw new UserFriendlyException("课程不存在或无权访问。");

        var chapterQuery = await _chapterRepository.GetQueryableAsync();
        var chapters = await AsyncExecuter.ToListAsync(
            chapterQuery.Where(x => x.CourseId == courseId).OrderBy(x => x.SortOrder));

        var caseQuery = await _caseRepository.WithDetailsAsync(x => x.Elements);
        var cases = await AsyncExecuter.ToListAsync(caseQuery.Where(x => x.CourseId == courseId));

        var elements = await AsyncExecuter.ToListAsync(
            (await _elementRepository.GetQueryableAsync()).Where(x => x.IsEnabled).OrderBy(x => x.SortOrder));

        // 只用「未下架」的案例建图（草稿也画出来，但前端按状态区分颜色，便于发现"只有草稿"的章节）。
        var visibleCases = cases.Where(x => x.Status != IdeologyCaseStatus.Archived).ToList();

        var edges = new List<IdeologyGraphEdgeDto>();
        foreach (var item in visibleCases)
        {
            if (item.ChapterId.HasValue)
            {
                edges.Add(new IdeologyGraphEdgeDto
                {
                    Kind = "case-chapter",
                    From = item.Id,
                    To = item.ChapterId.Value,
                });
            }

            foreach (var element in item.Elements)
            {
                edges.Add(new IdeologyGraphEdgeDto
                {
                    Kind = "case-element",
                    From = item.Id,
                    To = element.ElementId,
                });
            }
        }

        var chapterNodes = chapters.Select(chapter => new IdeologyGraphNodeDto
        {
            Id = chapter.Id,
            Title = chapter.Title,
            Weight = visibleCases.Count(x => x.ChapterId == chapter.Id),
        }).ToList();

        // 课程级通用案例（无章节）单独给一个"课程通用"节点，避免它们在图里消失。
        var courseLevelCount = visibleCases.Count(x => !x.ChapterId.HasValue);
        if (courseLevelCount > 0)
        {
            chapterNodes.Add(new IdeologyGraphNodeDto
            {
                Id = Guid.Empty,
                Title = "课程通用（未绑章节）",
                Weight = courseLevelCount,
            });
        }

        var caseNodes = visibleCases.Select(x => new IdeologyGraphNodeDto
        {
            Id = x.Id,
            Title = x.Title,
            Weight = x.Elements.Count,
            Status = x.Status,
        }).ToList();

        var elementNodes = elements.Select(element => new IdeologyGraphNodeDto
        {
            Id = element.Id,
            Title = element.Name,
            Weight = visibleCases.Count(x => x.Elements.Any(e => e.ElementId == element.Id)),
        }).ToList();

        // 缺口 = 一个已发布案例都没有的章节。
        var gaps = chapterNodes
            .Where(node => node.Id != Guid.Empty)
            .Where(node => !visibleCases.Any(x =>
                x.ChapterId == node.Id && x.Status == IdeologyCaseStatus.Published))
            .ToList();

        return new IdeologyGraphDto
        {
            CourseId = courseId,
            CourseTitle = course.Title,
            Chapters = chapterNodes,
            Cases = caseNodes,
            Elements = elementNodes,
            Edges = edges,
            Gaps = gaps,
        };
    }
}
