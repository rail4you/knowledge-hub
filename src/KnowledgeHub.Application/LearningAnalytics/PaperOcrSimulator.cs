using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Exams;
using KnowledgeHub.Learning;
using KnowledgeHub.LearningAnalytics.Enums;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 纸质试卷 OCR 的 <b>Mock</b> 实现（不接真实 OCR）。
///
/// 目的：让「上传 → 识别 → 题目匹配题库 → 校对 → 写作答记录 → 分析」整条链路可用。
/// 行为：
/// - 按课程已选学生生成试卷（真实学生，非伪造姓名）；
/// - 从课程真实习题库抽取题目，按学生「能力值」生成确定性得分（同批次结果稳定）；
/// - 每道题映射到应用内真实习题，部分题目被刻意标记为低置信度 / 未匹配，用于演示人工校对。
///
/// 接入真实 OCR 时，替换本类即可（其余逻辑不变）。
/// </summary>
public class PaperOcrSimulator : ITransientDependency
{
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<PaperScript, Guid> _scriptRepository;
    private readonly IRepository<PaperItemScore, Guid> _itemRepository;
    private readonly IRepository<PaperBatch, Guid> _batchRepository;
    private readonly IIdentityUserRepository _userRepository;
    private readonly IGuidGenerator _guidGenerator;

    public PaperOcrSimulator(
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<PaperScript, Guid> scriptRepository,
        IRepository<PaperItemScore, Guid> itemRepository,
        IRepository<PaperBatch, Guid> batchRepository,
        IIdentityUserRepository userRepository,
        IGuidGenerator guidGenerator)
    {
        _studentCourseRepository = studentCourseRepository;
        _exerciseRepository = exerciseRepository;
        _scriptRepository = scriptRepository;
        _itemRepository = itemRepository;
        _batchRepository = batchRepository;
        _userRepository = userRepository;
        _guidGenerator = guidGenerator;
    }

    /// <summary>为批次生成 Mock 识别结果，返回生成的试卷份数。</summary>
    public async Task<int> GenerateAsync(PaperBatch batch)
    {
        // 重跑时先清空旧结果，保证幂等
        var oldScripts = await _scriptRepository.GetListAsync(x => x.BatchId == batch.Id);
        if (oldScripts.Any())
        {
            var oldScriptIds = oldScripts.Select(s => s.Id).ToList();
            await _itemRepository.DeleteAsync(x => oldScriptIds.Contains(x.ScriptId));
            await _scriptRepository.DeleteAsync(x => x.BatchId == batch.Id);
        }

        var enrollments = await _studentCourseRepository.GetListAsync(x => x.CourseId == batch.CourseId);
        var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
        if (!studentIds.Any())
        {
            batch.ScriptCount = 0;
            batch.NeedsConfirmCount = 0;
            batch.AverageScore = 0;
            batch.Status = PaperBatchStatus.NeedConfirm;
            await _batchRepository.UpdateAsync(batch, autoSave: true);
            return 0;
        }

        var exercises = (await _exerciseRepository.GetListAsync(x => x.CourseId == batch.CourseId))
            .OrderBy(e => e.ChapterId)
            .ThenBy(e => e.Title)
            .Take(10)
            .ToList();
        if (!exercises.Any())
        {
            // 没有题库原题就无法映射知识点，保持待识别状态，提示老师先建题
            batch.ScriptCount = 0;
            batch.NeedsConfirmCount = 0;
            batch.AverageScore = 0;
            batch.Status = PaperBatchStatus.NeedConfirm;
            await _batchRepository.UpdateAsync(batch, autoSave: true);
            return 0;
        }

        var users = await _userRepository.GetListByIdsAsync(studentIds);
        var userById = users.ToDictionary(u => u.Id);

        double total = 0;
        var needConfirm = 0;
        var created = 0;

        foreach (var studentId in studentIds)
        {
            var name = ResolveName(userById, studentId);
            var number = ResolveStudentNumber(userById, studentId);

            var seed = StableSeed(studentId, batch.Id);
            var rnd = new Random(seed);
            var ability = 0.35 + rnd.NextDouble() * 0.55;

            var script = new PaperScript(_guidGenerator.Create(), batch.Id, number, name)
            {
                StudentId = studentId,
            };

            double scriptTotal = 0;
            var confidences = new List<double>();
            var questionNo = 0;

            foreach (var exercise in exercises)
            {
                questionNo++;
                var fullScore = exercise.Score > 0 ? exercise.Score : (questionNo % 3 == 0 ? 10 : 5);

                var noise = (rnd.NextDouble() - 0.5) * 0.5;
                var ratio = Math.Clamp(ability + noise, 0, 1);
                var score = Math.Round(fullScore * ratio * 2, MidpointRounding.AwayFromZero) / 2;

                // 10% 低置信度、3% 未匹配（模拟识别失败，用于演示人工校对）
                var roll = rnd.NextDouble();
                var confidence = 0.85 + rnd.NextDouble() * 0.14;
                PaperMatchStatus matchStatus;

                if (roll < 0.03)
                {
                    matchStatus = PaperMatchStatus.None;
                    confidence = 0.35 + rnd.NextDouble() * 0.2;
                }
                else if (roll < 0.13)
                {
                    matchStatus = PaperMatchStatus.Low;
                    confidence = 0.55 + rnd.NextDouble() * 0.18;
                }
                else
                {
                    matchStatus = PaperMatchStatus.Matched;
                }

                confidences.Add(confidence);
                scriptTotal += score;

                var item = new PaperItemScore(_guidGenerator.Create(), script.Id, batch.Id, questionNo.ToString())
                {
                    // 未匹配的题目不落到题库原题，避免污染作答记录
                    ExerciseId = matchStatus == PaperMatchStatus.None ? null : exercise.Id,
                    ChapterId = exercise.ChapterId,
                    MatchStatus = matchStatus,
                    Score = score,
                    FullScore = fullScore,
                    OcrConfidence = confidence,
                };
                await _itemRepository.InsertAsync(item, autoSave: true);
            }

            script.TotalScore = scriptTotal;
            script.OcrConfidence = confidences.Any() ? confidences.Average() : 1;
            if (script.OcrConfidence < 0.85)
            {
                needConfirm++;
            }

            await _scriptRepository.InsertAsync(script, autoSave: true);
            total += scriptTotal;
            created++;
        }

        batch.ScriptCount = created;
        batch.NeedsConfirmCount = needConfirm;
        batch.AverageScore = created > 0 ? Math.Round(total / created, 1) : 0;
        batch.Status = PaperBatchStatus.NeedConfirm;
        await _batchRepository.UpdateAsync(batch, autoSave: true);

        return created;
    }

    private static string ResolveName(Dictionary<Guid, IdentityUser> users, Guid studentId)
    {
        if (!users.TryGetValue(studentId, out var user))
        {
            return studentId.ToString("N")[..8];
        }

        var full = $"{user.Name}{user.Surname}".Trim();
        return string.IsNullOrWhiteSpace(full) ? user.UserName ?? "学生" : full;
    }

    private static string ResolveStudentNumber(Dictionary<Guid, IdentityUser> users, Guid studentId)
    {
        if (users.TryGetValue(studentId, out var user))
        {
            if (!string.IsNullOrWhiteSpace(user.UserName))
            {
                return user.UserName!;
            }

            if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                return user.PhoneNumber!;
            }
        }

        return studentId.ToString("N")[..10];
    }

    private static int StableSeed(Guid a, Guid b)
    {
        var ba = a.ToByteArray();
        var bb = b.ToByteArray();
        var hash = 17;
        for (var i = 0; i < 16; i++)
        {
            hash = hash * 31 + ba[i];
            hash = hash * 31 + bb[i];
        }

        return hash & 0x7FFFFFFF;
    }
}
