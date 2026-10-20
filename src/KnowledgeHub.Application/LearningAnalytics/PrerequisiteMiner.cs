using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KnowledgeHub.LearningAnalytics.Dtos;
using KnowledgeHub.LearningAnalytics.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 先修关系挖掘：对「前置课程 → 后续课程」共同修读的学生，
/// 计算前置知识点掌握度与后续知识点掌握度的皮尔逊相关。
/// 样本量 ≥ MinSamples 且 |R| ≥ MinCorrelation 的候选写成
/// <see cref="KpPrerequisite"/>（Source=Mined，Status=Proposed）。
///
/// **相关不等于因果**：结果必须老师确认（Confirmed）才参与衔接复习包计算。
/// </summary>
public class PrerequisiteMiner : ITransientDependency
{
    private readonly IRepository<CourseLink, Guid> _courseLinkRepository;
    private readonly IRepository<KnowledgePoint, Guid> _kpRepository;
    private readonly IRepository<StudentKpMastery, Guid> _masteryRepository;
    private readonly IRepository<KpPrerequisite, Guid> _prerequisiteRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PrerequisiteMiner> _logger;

    public PrerequisiteMiner(
        IRepository<CourseLink, Guid> courseLinkRepository,
        IRepository<KnowledgePoint, Guid> kpRepository,
        IRepository<StudentKpMastery, Guid> masteryRepository,
        IRepository<KpPrerequisite, Guid> prerequisiteRepository,
        IGuidGenerator guidGenerator,
        IConfiguration configuration,
        ILogger<PrerequisiteMiner> logger)
    {
        _courseLinkRepository = courseLinkRepository;
        _kpRepository = kpRepository;
        _masteryRepository = masteryRepository;
        _prerequisiteRepository = prerequisiteRepository;
        _guidGenerator = guidGenerator;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<MiningResultDto> MineAsync(Guid? successorCourseId = null, CancellationToken cancellationToken = default)
    {
        if (!_configuration.GetValue("LearningAnalytics:PrerequisiteMiningEnabled", true))
        {
            return new MiningResultDto { Message = "先修挖掘已关闭（LearningAnalytics:PrerequisiteMiningEnabled=false）" };
        }

        var minSamples = _configuration.GetValue("LearningAnalytics:MiningMinSamples", 30);
        var minCorrelation = _configuration.GetValue("LearningAnalytics:MiningMinCorrelation", 0.3);
        var maxPairs = _configuration.GetValue("LearningAnalytics:MiningMaxPairsPerLink", 20);

        var links = await _courseLinkRepository.GetListAsync(
            l => l.Status == PrerequisiteStatus.Confirmed, cancellationToken: cancellationToken);
        if (successorCourseId.HasValue)
        {
            // 传入的课程可能是后续课程，也可能是前置课程，两种都要挖掘
            links = links.Where(l =>
                l.SuccessorCourseId == successorCourseId.Value ||
                l.PredecessorCourseId == successorCourseId.Value).ToList();
        }

        if (!links.Any())
        {
            return new MiningResultDto { Message = "尚未配置课程前后关系（CourseLink），无法挖掘先修知识点" };
        }

        var proposed = 0;
        var evaluatedPairs = 0;

        foreach (var link in links)
        {
            var predecessorKps = (await _kpRepository.GetListAsync(
                k => k.CourseId == link.PredecessorCourseId, cancellationToken: cancellationToken)).ToList();
            var successorKps = (await _kpRepository.GetListAsync(
                k => k.CourseId == link.SuccessorCourseId, cancellationToken: cancellationToken)).ToList();

            if (!predecessorKps.Any() || !successorKps.Any())
            {
                continue;
            }

            var predKpIds = predecessorKps.Select(k => k.Id).ToHashSet();
            var succKpIds = successorKps.Select(k => k.Id).ToHashSet();

            var allMasteries = await _masteryRepository.GetListAsync(
                m => m.Evidence >= 0.5, cancellationToken: cancellationToken);
            var relevant = allMasteries
                .Where(m => predKpIds.Contains(m.KnowledgePointId) || succKpIds.Contains(m.KnowledgePointId))
                .ToList();

            if (!relevant.Any())
            {
                continue;
            }

            // student → (kpId → mastery)
            var byStudent = relevant
                .GroupBy(m => m.StudentId)
                .ToDictionary(
                    g => g.Key,
                    g => g.GroupBy(x => x.KnowledgePointId)
                        .ToDictionary(x => x.Key, x => x.Average(y => y.Mastery)));

            // 只要同时有两门课数据的学生
            var commonStudentIds = byStudent
                .Where(kv => kv.Value.Keys.Any(predKpIds.Contains) && kv.Value.Keys.Any(succKpIds.Contains))
                .Select(kv => kv.Key)
                .ToList();

            if (commonStudentIds.Count < minSamples)
            {
                _logger.LogInformation(
                    "先修挖掘：{Pre} → {Succ} 共同样本 {Count} < {Min}，跳过",
                    link.PredecessorCourseId, link.SuccessorCourseId, commonStudentIds.Count, minSamples);
                continue;
            }

            var candidates = new List<(Guid From, Guid To, double R, int N)>();

            foreach (var fromKp in predecessorKps)
            {
                var fromVector = commonStudentIds
                    .Where(id => byStudent[id].ContainsKey(fromKp.Id))
                    .ToDictionary(id => id, id => byStudent[id][fromKp.Id]);

                if (fromVector.Count == 0) continue;

                foreach (var toKp in successorKps)
                {
                    var toVector = commonStudentIds
                        .Where(id => byStudent[id].ContainsKey(toKp.Id))
                        .ToDictionary(id => id, id => byStudent[id][toKp.Id]);

                    if (toVector.Count == 0) continue;

                    var (r, n) = MasteryCalculator.Correlation(fromVector, toVector);
                    evaluatedPairs++;

                    if (n >= minSamples && r >= minCorrelation)
                    {
                        candidates.Add((fromKp.Id, toKp.Id, r, n));
                    }
                }
            }

            foreach (var candidate in candidates.OrderByDescending(c => c.R).Take(maxPairs))
            {
                var existing = await _prerequisiteRepository.GetListAsync(
                    e => e.FromKpId == candidate.From && e.ToKpId == candidate.To,
                    cancellationToken: cancellationToken);
                var edge = existing.FirstOrDefault();

                if (edge != null)
                {
                    // 老师已确认的边不覆盖，只刷新强度
                    if (edge.Status == PrerequisiteStatus.Proposed)
                    {
                        edge.Strength = Math.Round(candidate.R, 4);
                        edge.Source = PrerequisiteSource.Mined;
                        edge.Condition ??= "掌握率 < 60%";
                        await _prerequisiteRepository.UpdateAsync(edge, autoSave: true, cancellationToken: cancellationToken);
                    }

                    continue;
                }

                var created = new KpPrerequisite(_guidGenerator.Create(), candidate.From, candidate.To)
                {
                    Strength = Math.Round(candidate.R, 4),
                    Source = PrerequisiteSource.Mined,
                    Status = PrerequisiteStatus.Proposed,
                    Condition = "掌握率 < 60%",
                };
                await _prerequisiteRepository.InsertAsync(created, autoSave: true, cancellationToken: cancellationToken);
                proposed++;
            }
        }

        var message = proposed > 0
            ? $"挖掘出 {proposed} 条先修候选（共评估 {evaluatedPairs} 组），需在「课程联动」中确认后生效"
            : $"未发现新的先修候选（共评估 {evaluatedPairs} 组）";

        _logger.LogInformation("先修挖掘完成：{Message}", message);

        return new MiningResultDto
        {
            ProposedCount = proposed,
            SamplePairs = evaluatedPairs,
            Message = message,
        };
    }
}
