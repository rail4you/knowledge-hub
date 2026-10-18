using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace KnowledgeHub.Learning;

/// <summary>
/// 课程习题进度计算（学生维度）—— 学生端「我的课程 / 课程详情 / 学习页」以及仪表盘
/// 的「课程是否完成」共用同一口径，避免各处各算一套导致数字对不上。
///
/// 口径：
/// 分母 = 学生能学到的全部习题：已关联到本课程章节的习题（主章节 Exercise.ChapterId + 复用关联表
///        ChapterExercise 去重，且章节必须属于该习题所在课程）。未挂到任何章节的备选题学生在
///        章节学习页根本打不开，不计入分母（与教师端统计一致）。
/// 分子 = 该生提交记录（CompletedAt 有值）去重后与分母取交集，避免已删除习题导致分子越界。
/// </summary>
public interface ICourseExerciseProgressService
{
    /// <summary>批量计算某学生多门课程的习题进度（已学题数 / 总题数）</summary>
    Task<Dictionary<Guid, (int CompletedCount, int TotalCount)>> GetProgressMapAsync(
        Guid studentId, List<Guid> courseIds);

    /// <summary>课程是否已完成：有习题且全部习题都已作答</summary>
    Task<bool> IsCourseCompletedAsync(Guid studentId, Guid courseId);
}

public class CourseExerciseProgressService : ICourseExerciseProgressService
{
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<ChapterExercise, Guid> _chapterExerciseRepository;
    private readonly IRepository<StudentExerciseRecord, Guid> _exerciseRecordRepository;
    private readonly IDataFilter _dataFilter;

    public CourseExerciseProgressService(
        IRepository<Exercise, Guid> exerciseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<ChapterExercise, Guid> chapterExerciseRepository,
        IRepository<StudentExerciseRecord, Guid> exerciseRecordRepository,
        IDataFilter dataFilter)
    {
        _exerciseRepository = exerciseRepository;
        _chapterRepository = chapterRepository;
        _chapterExerciseRepository = chapterExerciseRepository;
        _exerciseRecordRepository = exerciseRecordRepository;
        _dataFilter = dataFilter;
    }

    public async Task<bool> IsCourseCompletedAsync(Guid studentId, Guid courseId)
    {
        var map = await GetProgressMapAsync(studentId, new List<Guid> { courseId });
        return map.TryGetValue(courseId, out var p) && p.TotalCount > 0 && p.CompletedCount >= p.TotalCount;
    }

    public async Task<Dictionary<Guid, (int CompletedCount, int TotalCount)>> GetProgressMapAsync(
        Guid studentId, List<Guid> courseIds)
    {
        var map = courseIds.Distinct().ToDictionary(id => id, _ => (CompletedCount: 0, TotalCount: 0));
        if (courseIds.Count == 0)
        {
            return map;
        }

        List<Exercise> exercises;
        List<Chapter> chapters;
        List<ChapterExercise> links;
        List<StudentExerciseRecord> records;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var exerciseQuery = await _exerciseRepository.GetQueryableAsync();
            exercises = await exerciseQuery.Where(e => courseIds.Contains(e.CourseId)).ToListAsync();

            var chapterQuery = await _chapterRepository.GetQueryableAsync();
            chapters = await chapterQuery.Where(c => courseIds.Contains(c.CourseId)).ToListAsync();

            var chapterIds = chapters.Select(c => c.Id).ToList();
            var exerciseIds = new HashSet<Guid>(exercises.Select(e => e.Id));
            if (chapterIds.Count > 0)
            {
                var ceQuery = await _chapterExerciseRepository.GetQueryableAsync();
                links = await ceQuery
                    .Where(ce => chapterIds.Contains(ce.ChapterId) || exerciseIds.Contains(ce.ExerciseId))
                    .ToListAsync();
            }
            else
            {
                links = new List<ChapterExercise>();
            }

            var recordQuery = await _exerciseRecordRepository.GetQueryableAsync();
            records = await recordQuery
                .Where(r => r.StudentId == studentId && courseIds.Contains(r.CourseId))
                .ToListAsync();
        }

        var chapterCourseMap = chapters.ToDictionary(c => c.Id, c => c.CourseId);
        var exerciseCourseMap = exercises.ToDictionary(e => e.Id, e => e.CourseId);

        // 题目 -> 所属章节（主章节 + 复用关联表，且章节必须属于同一课程）
        var exerciseChapters = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var e in exercises)
        {
            if (e.ChapterId.HasValue
                && chapterCourseMap.TryGetValue(e.ChapterId.Value, out var cc)
                && cc == e.CourseId)
            {
                if (!exerciseChapters.TryGetValue(e.Id, out var set))
                {
                    set = new HashSet<Guid>();
                    exerciseChapters[e.Id] = set;
                }
                set.Add(e.ChapterId.Value);
            }
        }
        foreach (var ce in links)
        {
            if (!exerciseCourseMap.TryGetValue(ce.ExerciseId, out var ec))
            {
                continue;
            }
            if (!chapterCourseMap.TryGetValue(ce.ChapterId, out var cc) || cc != ec)
            {
                continue;
            }
            if (!exerciseChapters.TryGetValue(ce.ExerciseId, out var set))
            {
                set = new HashSet<Guid>();
                exerciseChapters[ce.ExerciseId] = set;
            }
            set.Add(ce.ChapterId);
        }

        var totalByCourse = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var kv in exerciseChapters)
        {
            var courseId = exerciseCourseMap[kv.Key];
            if (!totalByCourse.TryGetValue(courseId, out var set))
            {
                set = new HashSet<Guid>();
                totalByCourse[courseId] = set;
            }
            set.Add(kv.Key);
        }

        var completedByCourse = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var r in records)
        {
            // 每条提交即视为已学习（SetAnswer 必写 CompletedAt）；仍要求 CompletedAt 有值以对齐教师端口径
            if (!r.CompletedAt.HasValue)
            {
                continue;
            }
            if (!totalByCourse.TryGetValue(r.CourseId, out var totalSet) || !totalSet.Contains(r.ExerciseId))
            {
                continue;
            }
            if (!completedByCourse.TryGetValue(r.CourseId, out var set))
            {
                set = new HashSet<Guid>();
                completedByCourse[r.CourseId] = set;
            }
            set.Add(r.ExerciseId);
        }

        foreach (var courseId in map.Keys.ToList())
        {
            var total = totalByCourse.TryGetValue(courseId, out var t) ? t.Count : 0;
            var completed = completedByCourse.TryGetValue(courseId, out var c) ? c.Count : 0;
            map[courseId] = (Math.Min(completed, total), total);
        }

        return map;
    }
}
