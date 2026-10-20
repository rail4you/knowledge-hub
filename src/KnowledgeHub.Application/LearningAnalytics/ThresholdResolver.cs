using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>课程判定线集合：课程级默认 + 知识点级覆盖（判断优先级：知识点 > 课程 > 系统默认）。</summary>
public class CourseThresholds
{
    public Thresholds Default { get; }

    public IReadOnlyDictionary<Guid, Thresholds> Overrides { get; }

    public CourseThresholds(Thresholds defaults, IReadOnlyDictionary<Guid, Thresholds> overrides)
    {
        Default = defaults;
        Overrides = overrides;
    }

    public Thresholds For(Guid? knowledgePointId) =>
        knowledgePointId.HasValue && Overrides.TryGetValue(knowledgePointId.Value, out var value)
            ? value
            : Default;

    public MasteryGrade Judge(Guid? knowledgePointId, double mastery) => For(knowledgePointId).Judge(mastery);
}

/// <summary>
/// 判定线解析：知识点覆盖 &gt; 课程级配置 &gt; 系统默认（0.60 / 0.75）。
/// 调整判定线只影响等级划分，不改变底层掌握度。
/// </summary>
public class ThresholdResolver : ITransientDependency
{
    private readonly IRepository<MasteryThreshold, Guid> _thresholdRepository;

    public ThresholdResolver(IRepository<MasteryThreshold, Guid> thresholdRepository)
    {
        _thresholdRepository = thresholdRepository;
    }

    public async Task<Thresholds> ResolveAsync(Guid courseId, Guid? knowledgePointId = null)
    {
        var list = await _thresholdRepository.GetListAsync(x => x.CourseId == courseId);

        if (!list.Any())
        {
            return Thresholds.Default;
        }

        if (knowledgePointId.HasValue)
        {
            var kpLevel = list.FirstOrDefault(x => x.KnowledgePointId == knowledgePointId.Value);
            if (kpLevel != null)
            {
                return new Thresholds(kpLevel.WeakLine, kpLevel.OkLine);
            }
        }

        var courseLevel = list.FirstOrDefault(x => x.KnowledgePointId == null);
        return courseLevel != null
            ? new Thresholds(courseLevel.WeakLine, courseLevel.OkLine)
            : Thresholds.Default;
    }

    /// <summary>
    /// 一次性解析课程下所有判定线：返回课程级默认 + 知识点级覆盖。
    /// 批量查询避免逐个知识点访问数据库（学生端/班级分析都会用到）。
    /// </summary>
    public async Task<CourseThresholds> ResolveAllAsync(Guid courseId)
    {
        var list = await _thresholdRepository.GetListAsync(x => x.CourseId == courseId);
        var courseLevel = list.FirstOrDefault(x => x.KnowledgePointId == null);
        var defaults = courseLevel != null
            ? new Thresholds(courseLevel.WeakLine, courseLevel.OkLine)
            : Thresholds.Default;

        var overrides = list
            .Where(x => x.KnowledgePointId.HasValue)
            .GroupBy(x => x.KnowledgePointId!.Value)
            .ToDictionary(g => g.Key, g => new Thresholds(g.First().WeakLine, g.First().OkLine));

        return new CourseThresholds(defaults, overrides);
    }

    /// <summary>删除某知识点的判定线覆盖（恢复课程级）。</summary>
    public async Task<int> RemoveOverrideAsync(Guid courseId, Guid knowledgePointId)
    {
        var list = await _thresholdRepository.GetListAsync(x =>
            x.CourseId == courseId && x.KnowledgePointId == knowledgePointId);
        var count = 0;
        foreach (var item in list)
        {
            await _thresholdRepository.DeleteAsync(item, autoSave: true);
            count++;
        }

        return count;
    }

    public async Task<MasteryThreshold?> GetRawAsync(Guid courseId)
    {
        var list = await _thresholdRepository.GetListAsync(x => x.CourseId == courseId && x.KnowledgePointId == null);
        return list.FirstOrDefault();
    }

    public async Task<MasteryThreshold> SetAsync(Guid courseId, Guid? knowledgePointId, double weakLine, double okLine)
    {
        var list = await _thresholdRepository.GetListAsync(x =>
            x.CourseId == courseId && x.KnowledgePointId == knowledgePointId);
        var entity = list.FirstOrDefault();

        if (entity == null)
        {
            entity = new MasteryThreshold(Guid.NewGuid(), courseId) { KnowledgePointId = knowledgePointId };
            entity.WeakLine = weakLine;
            entity.OkLine = okLine;
            await _thresholdRepository.InsertAsync(entity);
        }
        else
        {
            entity.WeakLine = weakLine;
            entity.OkLine = okLine;
            await _thresholdRepository.UpdateAsync(entity);
        }

        return entity;
    }
}
