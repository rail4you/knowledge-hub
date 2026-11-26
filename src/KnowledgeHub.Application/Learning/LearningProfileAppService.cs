using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using KnowledgeHub.Courses;
using KnowledgeHub.Learning.Dtos;
using KnowledgeHub.Learning.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace KnowledgeHub.Learning;

/// <summary>
/// 学习画像 + 数据洞察（对齐 kg-edu `student_profile` / `insights/engine`）。
/// <para>
/// 关键口径（与学情保持一致，不另算一套）：
/// ① 行为只来自 <c>StudentActivityLog</c>（B2 埋点）；
/// ② 掌握度只读 <c>KnowledgeMastery</c>（B1 统一算法）；
/// ③ 连续性 / 近期活跃度 / 偏好类型 / 活跃时段 四个算法直接照搬 kg-edu 的
///    <c>recommendation_engine.ex</c>（见各方法的注释），保证口径可解释。
/// </para>
/// </summary>
[Authorize]
[IgnoreAntiforgeryToken]
public class LearningProfileAppService : KnowledgeHubAppService, ILearningProfileAppService
{
    /// <summary>画像默认回看天数。</summary>
    private const int DefaultDays = 30;

    private readonly IRepository<StudentActivityLog, Guid> _activityRepository;
    private readonly IRepository<KnowledgeMastery, Guid> _masteryRepository;
    private readonly IRepository<StudentCourse, Guid> _studentCourseRepository;
    private readonly IRepository<KnowledgeResource, Guid> _knowledgeResourceRepository;
    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentUser _currentUser;

    public LearningProfileAppService(
        IRepository<StudentActivityLog, Guid> activityRepository,
        IRepository<KnowledgeMastery, Guid> masteryRepository,
        IRepository<StudentCourse, Guid> studentCourseRepository,
        IRepository<KnowledgeResource, Guid> knowledgeResourceRepository,
        IRepository<Course, Guid> courseRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IDataFilter dataFilter,
        ICurrentUser currentUser)
    {
        _activityRepository = activityRepository;
        _masteryRepository = masteryRepository;
        _studentCourseRepository = studentCourseRepository;
        _knowledgeResourceRepository = knowledgeResourceRepository;
        _courseRepository = courseRepository;
        _userRepository = userRepository;
        _dataFilter = dataFilter;
        _currentUser = currentUser;
    }

    public async Task<StudentLearningProfileDto> GetMyProfileAsync(Guid? courseId = null)
    {
        var studentId = _currentUser.Id ?? throw new UserFriendlyException("请先登录。");
        return await GetStudentProfileAsync(studentId, courseId);
    }

    public async Task<StudentLearningProfileDto> GetStudentProfileAsync(Guid studentId, Guid? courseId = null)
    {
        var since = DateTime.UtcNow.AddDays(-DefaultDays);
        var logs = await LoadLogsAsync(since, courseId: courseId, studentId: studentId);

        var dto = new StudentLearningProfileDto
        {
            StudentId = studentId,
            StudentName = await GetUserNameAsync(studentId),
            CourseId = courseId,
            ActivityCount = logs.Count,
            LastActiveAt = logs.Count == 0 ? null : logs.Max(x => x.CreationTime)
        };

        var now = DateTime.UtcNow;

        // ① 偏好学习方式：活动类型众数（kg-edu: determine_preferred_learning_type）
        if (logs.Count > 0)
        {
            var preferred = logs.GroupBy(x => x.ActionType)
                .OrderByDescending(g => g.Count())
                .First();
            dto.PreferredAction = preferred.Key;
            dto.PreferredActionLabel = ActionLabel(preferred.Key);
        }

        // ② 活跃时段：按小时分桶后取众数（kg-edu: determine_active_hours）
        if (logs.Count > 0)
        {
            var byHour = logs.GroupBy(x => x.CreationTime.ToLocalTime().Hour)
                .OrderByDescending(g => g.Count())
                .First().Key;
            dto.ActivePeriod = byHour switch
            {
                >= 0 and < 6 => "深夜（0-6 点）",
                >= 6 and < 12 => "上午（6-12 点）",
                >= 12 and < 18 => "下午（12-18 点）",
                _ => "晚上（18-24 点）"
            };
        }

        // ③ 学习连续性：近 30 天有活动的天数 / 30（kg-edu: calculate_consistency_score）
        var activeDays = logs.Select(x => x.CreationTime.Date).Distinct().Count();
        dto.ConsistencyScore = Math.Round(Math.Min(1, activeDays / (double)DefaultDays), 3);

        // ④ 近期活跃度：近 7 天 / 前 7 天（kg-edu: calculate_recent_activity_level）
        var last7 = logs.Count(x => (now - x.CreationTime).TotalDays <= 7);
        var prev7 = logs.Count(x => (now - x.CreationTime).TotalDays is > 7 and <= 14);
        dto.RecentActivityLevel = prev7 == 0
            ? (last7 > 0 ? 2 : 0)
            : Math.Round(last7 / (double)prev7, 2);

        dto.ActionDistribution = logs
            .GroupBy(x => x.ActionType)
            .OrderByDescending(g => g.Count())
            .Select(g => new ProfileActionCountDto
            {
                Action = g.Key,
                Label = ActionLabel(g.Key),
                Count = g.Count()
            })
            .ToList();

        dto.Timeline = BuildTimeline(logs, since, DefaultDays);

        // ⑤ 薄弱知识点：掌握度从低到高，只取有证据的（无证据不算薄弱）
        var weakPoints = await LoadWeakPointsAsync(studentId, courseId);
        dto.WeakPoints = weakPoints;

        dto.Insights = BuildStudentInsights(dto, weakPoints);
        return dto;
    }

    public async Task<CourseLearningProfileDto> GetCourseProfileAsync(Guid courseId, int days = DefaultDays)
    {
        days = Math.Clamp(days, 7, 180);
        var since = DateTime.UtcNow.AddDays(-days);

        var course = await FindCourseAsync(courseId);
        var enrolled = await GetEnrolledStudentIdsAsync(courseId);
        var logs = await LoadLogsAsync(since, courseId: courseId);
        var names = await GetUserNamesAsync(enrolled);

        var byStudent = logs.GroupBy(x => x.StudentId).ToDictionary(g => g.Key, g => g.ToList());
        var now = DateTime.UtcNow;

        var summaries = enrolled.Select(id =>
        {
            var mine = byStudent.GetValueOrDefault(id) ?? new List<StudentActivityLog>();
            return new StudentActivitySummaryDto
            {
                StudentId = id,
                StudentName = names.GetValueOrDefault(id),
                ActivityCount = mine.Count,
                LastActiveAt = mine.Count == 0 ? null : mine.Max(x => x.CreationTime)
            };
        }).ToList();

        var dto = new CourseLearningProfileDto
        {
            CourseId = courseId,
            CourseTitle = course?.Title,
            Days = days,
            StudentCount = enrolled.Count,
            ActiveStudentCount = byStudent.Count,
            ActivityCount = logs.Count,
            ActivityPerStudent = enrolled.Count == 0 ? 0 : Math.Round(logs.Count / (double)enrolled.Count, 1),
            Timeline = BuildTimeline(logs, since, days),
            ActionDistribution = logs.GroupBy(x => x.ActionType)
                .OrderByDescending(g => g.Count())
                .Select(g => new ProfileActionCountDto { Action = g.Key, Label = ActionLabel(g.Key), Count = g.Count() })
                .ToList(),
            TopActiveStudents = summaries.Where(x => x.ActivityCount > 0)
                .OrderByDescending(x => x.ActivityCount).Take(10).ToList(),
            InactiveStudents = summaries.Where(x => x.ActivityCount == 0)
                .OrderBy(x => x.StudentName).Take(50).ToList()
        };

        // 班级平均连续性：每个学生"有活动的天数 / 区间天数"，再取平均
        if (byStudent.Count > 0)
        {
            var perStudentConsistency = byStudent.Values
                .Select(list => list.Select(x => x.CreationTime.Date).Distinct().Count() / (double)days)
                .ToList();
            dto.AverageConsistency = Math.Round(perStudentConsistency.Average(), 3);
        }

        var last7 = logs.Count(x => (now - x.CreationTime).TotalDays <= 7);
        var prev7 = logs.Count(x => (now - x.CreationTime).TotalDays is > 7 and <= 14);
        dto.RecentActivityLevel = prev7 == 0
            ? (last7 > 0 ? 2 : 0)
            : Math.Round(last7 / (double)prev7, 2);

        return dto;
    }

    public async Task<CourseInsightDto> GetCourseInsightAsync(Guid courseId, int days = DefaultDays)
    {
        var profile = await GetCourseProfileAsync(courseId, days);
        var weak = await GetCourseWeakPointsAsync(courseId);

        var dto = new CourseInsightDto
        {
            CourseId = courseId,
            CourseTitle = profile.CourseTitle,
            Days = profile.Days
        };

        var activeRate = profile.StudentCount == 0
            ? 0
            : profile.ActiveStudentCount / (double)profile.StudentCount;

        dto.Metrics.Add(new InsightMetricDto
        {
            Key = "activeRate",
            Label = "活跃学生占比",
            Value = $"{Math.Round(activeRate * 100)}%",
            Hint = $"{profile.ActiveStudentCount} / {profile.StudentCount} 人在 {profile.Days} 天内有活动"
        });
        dto.Metrics.Add(new InsightMetricDto
        {
            Key = "avgActivity",
            Label = "人均活动数",
            Value = profile.ActivityPerStudent.ToString("0.0"),
            Hint = $"区间内共 {profile.ActivityCount} 次行为"
        });
        dto.Metrics.Add(new InsightMetricDto
        {
            Key = "consistency",
            Label = "平均学习连续性",
            Value = $"{Math.Round(profile.AverageConsistency * 100)}%",
            Hint = "有活动的天数 / 区间天数"
        });
        dto.Metrics.Add(new InsightMetricDto
        {
            Key = "recentLevel",
            Label = "近 7 天活跃变化",
            Value = profile.RecentActivityLevel.ToString("0.00") + "x",
            Hint = profile.RecentActivityLevel >= 1 ? "相比前 7 天在升温" : "相比前 7 天在降温"
        });

        // ── 规则式结论（不需要 LLM 也能给出可执行建议）──
        if (profile.StudentCount == 0)
        {
            dto.Findings.Add(new InsightFindingDto
            {
                Level = "warning",
                Title = "本课还没有学生",
                Detail = "先在「选课管理」里导入或让学生自助选课，画像才有意义。"
            });
        }
        else
        {
            if (activeRate < 0.5)
            {
                dto.Findings.Add(new InsightFindingDto
                {
                    Level = "danger",
                    Title = $"超过一半学生{days} 天内没有学习行为",
                    Detail = $"活跃 {profile.ActiveStudentCount} / 共 {profile.StudentCount} 人。" +
                             "建议：检查作业/任务是否发布到位，或用「学习推荐」给低活跃学生推送任务。"
                });
            }
            else if (activeRate < 0.8)
            {
                dto.Findings.Add(new InsightFindingDto
                {
                    Level = "warning",
                    Title = "近三成学生处于低活跃",
                    Detail = $"活跃 {profile.ActiveStudentCount} / 共 {profile.StudentCount} 人，可对未活跃名单做定向提醒。"
                });
            }

            if (profile.RecentActivityLevel < 0.7)
            {
                dto.Findings.Add(new InsightFindingDto
                {
                    Level = "warning",
                    Title = "近 7 天活跃明显下降",
                    Detail = $"近 7 天活动量是前 7 天的 {profile.RecentActivityLevel:0.00} 倍，建议在课堂上做一次节奏回收。"
                });
            }
            else if (profile.RecentActivityLevel >= 1.5)
            {
                dto.Findings.Add(new InsightFindingDto
                {
                    Level = "info",
                    Title = "近 7 天活跃明显上升",
                    Detail = $"活动量是前 7 天的 {profile.RecentActivityLevel:0.00} 倍，可以趁热推送进阶练习。"
                });
            }

            if (profile.AverageConsistency < 0.2)
            {
                dto.Findings.Add(new InsightFindingDto
                {
                    Level = "warning",
                    Title = "学习多为集中突击，缺少持续性",
                    Detail = "平均连续性低于 20%，建议拆小任务、按周推进（课堂练习/小组任务都适合）。"
                });
            }
        }

        if (weak.Count > 0)
        {
            var top = weak.Take(3).ToList();
            dto.Findings.Add(new InsightFindingDto
            {
                Level = "warning",
                Title = $"薄弱知识点集中在 {top.Count} 个点上",
                Detail = string.Join("、", top.Select(x => $"{x.Name}（掌握度 {Math.Round(x.Mastery * 100)}%）"))
                         + "。可在「学习推荐」里对这些点下发专项练习。"
            });
        }

        return dto;
    }

    public async Task<ProfileReportDto> BuildReportAsync(Guid courseId, string template = "monthly", int days = DefaultDays)
    {
        var insight = await GetCourseInsightAsync(courseId, days);
        var profile = await GetCourseProfileAsync(courseId, days);
        var weak = await GetCourseWeakPointsAsync(courseId);

        var label = template switch
        {
            "weekly" => "课程学习周报",
            "student" => "学生学习情况报告",
            _ => "课程学习月报"
        };

        var report = new ProfileReportDto
        {
            CourseId = courseId,
            CourseTitle = profile.CourseTitle,
            Template = template,
            TemplateLabel = label,
            Days = profile.Days,
            GeneratedAt = DateTime.UtcNow
        };

        var order = 0;
        string NextTitle(string title) => $"{ToChineseNumber(++order)}、{title}";

        // ① 概览
        report.Sections.Add(new ProfileReportSectionDto
        {
            Title = NextTitle("学习概况"),
            Paragraphs = new List<string>
            {
                $"统计区间：近 {profile.Days} 天。课程学生 {profile.StudentCount} 人，其中有学习行为 {profile.ActiveStudentCount} 人。",
                $"区间内共产生 {profile.ActivityCount} 次学习行为，人均 {profile.ActivityPerStudent:0.0} 次；" +
                $"平均学习连续性 {Math.Round(profile.AverageConsistency * 100)}%，近 7 天活跃为前 7 天的 {profile.RecentActivityLevel:0.00} 倍。"
            }
        });

        // ② 关键指标
        report.Sections.Add(new ProfileReportSectionDto
        {
            Title = NextTitle("关键指标"),
            Table = new ProfileReportTableDto
            {
                Headers = new List<string> { "指标", "数值", "说明" },
                Rows = insight.Metrics.Select(m => new List<string> { m.Label, m.Value, m.Hint ?? "" }).ToList()
            }
        });

        // ③ 数据结论
        report.Sections.Add(new ProfileReportSectionDto
        {
            Title = NextTitle("数据结论"),
            Paragraphs = insight.Findings.Count == 0
                ? new List<string> { "本期未发现需要特别关注的异常。" }
                : insight.Findings.Select(f => $"[{f.Title}] {f.Detail}").ToList()
        });

        // ④ 薄弱知识点
        if (weak.Count > 0)
        {
            report.Sections.Add(new ProfileReportSectionDto
            {
                Title = NextTitle("薄弱知识点"),
                Paragraphs = new List<string> { "按班级平均掌握度从低到高排列，建议优先安排专项练习。" },
                Table = new ProfileReportTableDto
                {
                    Headers = new List<string> { "知识点", "掌握度", "有证据学生数" },
                    Rows = weak.Select(w => new List<string>
                    {
                        w.Name, $"{Math.Round(w.Mastery * 100)}%", w.Evidence.ToString("0")
                    }).ToList()
                }
            });
        }

        // ⑤ 行为分布
        if (profile.ActionDistribution.Count > 0)
        {
            report.Sections.Add(new ProfileReportSectionDto
            {
                Title = NextTitle("学习行为分布"),
                Table = new ProfileReportTableDto
                {
                    Headers = new List<string> { "行为", "次数" },
                    Rows = profile.ActionDistribution.Select(a => new List<string> { a.Label, a.Count.ToString() }).ToList()
                }
            });
        }

        // ⑥ 未活跃名单（学生报告模板换成个人建议）
        if (template == "student")
        {
            report.Sections.Add(new ProfileReportSectionDto
            {
                Title = NextTitle("教学建议"),
                Paragraphs = new List<string>
                {
                    "1. 对未活跃学生逐一确认原因（账号 / 时间冲突 / 内容难度），优先用「学习推荐」下发低门槛任务。",
                    "2. 薄弱知识点不要堆在一次课里补，按「前置 → 当前」顺序安排。",
                    "3. 连续两次活跃下降时，建议换一种形式（课堂练习 / 小组任务）拉回参与度。"
                }
            });
        }
        else if (profile.InactiveStudents.Count > 0)
        {
            report.Sections.Add(new ProfileReportSectionDto
            {
                Title = NextTitle("未活跃学生名单"),
                Table = new ProfileReportTableDto
                {
                    Headers = new List<string> { "学生" },
                    Rows = profile.InactiveStudents.Select(s => new List<string> { s.StudentName ?? s.StudentId.ToString() }).ToList()
                }
            });
        }

        return report;
    }

    public async Task<IRemoteStreamContent> ExportReportAsync(Guid courseId, string template = "monthly", int days = DefaultDays)
    {
        var report = await BuildReportAsync(courseId, template, days);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("学习报告");

        sheet.Cell(1, 1).Value = $"{report.TemplateLabel} · {report.CourseTitle}";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Range(1, 1, 1, 3).Merge();

        sheet.Cell(2, 1).Value = $"统计区间：近 {report.Days} 天";
        sheet.Cell(2, 3).Value = $"生成时间：{report.GeneratedAt.ToLocalTime():yyyy-MM-dd HH:mm}";
        sheet.Range(2, 1, 2, 3).Merge();

        var row = 4;
        foreach (var section in report.Sections)
        {
            sheet.Cell(row, 1).Value = section.Title;
            sheet.Cell(row, 1).Style.Font.Bold = true;
            sheet.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F4F6");
            sheet.Range(row, 1, row, 3).Merge();
            row++;

            foreach (var paragraph in section.Paragraphs)
            {
                sheet.Cell(row, 1).Value = paragraph;
                sheet.Range(row, 1, row, 3).Merge();
                sheet.Range(row, 1, row, 3).Style.Alignment.WrapText = true;
                row++;
            }

            if (section.Table != null)
            {
                for (var c = 0; c < section.Table.Headers.Count && c < 3; c++)
                {
                    sheet.Cell(row, c + 1).Value = section.Table.Headers[c];
                    sheet.Cell(row, c + 1).Style.Font.Bold = true;
                }
                row++;

                foreach (var dataRow in section.Table.Rows)
                {
                    for (var c = 0; c < dataRow.Count && c < 3; c++)
                    {
                        sheet.Cell(row, c + 1).Value = dataRow[c];
                    }
                    row++;
                }
            }

            row++; // 小节间空一行
        }

        sheet.Column(1).Width = 46;
        sheet.Column(2).Width = 16;
        sheet.Column(3).Width = 46;

        // 注意：不能用 `using var stream` —— 方法返回后 RemoteStreamContent 还要读它，
        // 提前 Dispose 会抛「Cannot access a closed Stream」（导出 500）。
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var safeTitle = string.Concat((report.CourseTitle ?? "course").Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        return new RemoteStreamContent(
            stream,
            $"{report.TemplateLabel}_{safeTitle}_{DateTime.Now:yyyyMMddHHmm}.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    // ── 内部 ─────────────────────────────────────────────────────

    private async Task<List<StudentActivityLog>> LoadLogsAsync(
        DateTime since, Guid? courseId = null, Guid? studentId = null)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _activityRepository.GetQueryableAsync();
            return await AsyncExecuter.ToListAsync(
                query
                    .Where(x => x.CreationTime >= since)
                    .WhereIf(courseId.HasValue, x => x.CourseId == courseId!.Value)
                    .WhereIf(studentId.HasValue, x => x.StudentId == studentId!.Value));
        }
    }

    private static List<ProfileTrendPointDto> BuildTimeline(
        List<StudentActivityLog> logs, DateTime since, int days)
    {
        var byDay = logs.GroupBy(x => x.CreationTime.Date)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = new List<ProfileTrendPointDto>(days);
        for (var i = days - 1; i >= 0; i--)
        {
            var date = since.Date.AddDays(days - 1 - i);
            result.Add(new ProfileTrendPointDto
            {
                Date = date,
                Count = byDay.GetValueOrDefault(date)
            });
        }

        return result;
    }

    private async Task<List<ProfileWeakPointDto>> LoadWeakPointsAsync(Guid studentId, Guid? courseId)
    {
        List<KnowledgeMastery> masteries;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            masteries = await _masteryRepository.GetListAsync(x =>
                x.StudentId == studentId && x.Evidence > 0);
        }

        if (masteries.Count == 0)
        {
            return new List<ProfileWeakPointDto>();
        }

        var resourceIds = masteries.Select(x => x.KnowledgeResourceId).Distinct().ToList();
        var resources = await _knowledgeResourceRepository.GetListAsync(x => resourceIds.Contains(x.Id));
        var resourceById = resources.ToDictionary(x => x.Id);

        return masteries
            .Where(x => !courseId.HasValue || resourceById.GetValueOrDefault(x.KnowledgeResourceId)?.CourseId == courseId)
            .OrderBy(x => x.Mastery)
            .Take(5)
            .Select(x => new ProfileWeakPointDto
            {
                KnowledgeResourceId = x.KnowledgeResourceId,
                Name = resourceById.GetValueOrDefault(x.KnowledgeResourceId)?.Name ?? "未命名知识点",
                Mastery = x.Mastery,
                Evidence = x.Evidence
            })
            .ToList();
    }

    /// <summary>课程维度的薄弱点：按班级平均掌握度从低到高。</summary>
    private async Task<List<ProfileWeakPointDto>> GetCourseWeakPointsAsync(Guid courseId)
    {
        var resources = await _knowledgeResourceRepository.GetListAsync(x => x.CourseId == courseId);
        if (resources.Count == 0)
        {
            return new List<ProfileWeakPointDto>();
        }

        var resourceIds = resources.Select(x => x.Id).ToList();
        var resourceById = resources.ToDictionary(x => x.Id, x => x.Name);

        List<KnowledgeMastery> rows;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            rows = await _masteryRepository.GetListAsync(x =>
                resourceIds.Contains(x.KnowledgeResourceId) && x.Evidence > 0);
        }

        return rows.GroupBy(x => x.KnowledgeResourceId)
            .Select(g => new ProfileWeakPointDto
            {
                KnowledgeResourceId = g.Key,
                Name = resourceById.GetValueOrDefault(g.Key) ?? "未命名知识点",
                Mastery = Math.Round(g.Average(x => x.Mastery), 3),
                Evidence = g.Count()
            })
            .OrderBy(x => x.Mastery)
            .Take(10)
            .ToList();
    }

    private static List<string> BuildStudentInsights(StudentLearningProfileDto dto, List<ProfileWeakPointDto> weak)
    {
        var insights = new List<string>();

        if (dto.ActivityCount == 0)
        {
            insights.Add("近 30 天没有学习行为记录，先从完成一节章节学习开始。");
            return insights;
        }

        if (dto.PreferredActionLabel != null)
        {
            insights.Add($"你更常做「{dto.PreferredActionLabel}」，可以在薄弱知识点上多用习题巩固。");
        }

        if (dto.ActivePeriod != null)
        {
            insights.Add($"你最常在{dto.ActivePeriod}学习，把难点安排在这个时段效率更高。");
        }

        insights.Add(dto.ConsistencyScore switch
        {
            >= 0.6 => "学习连续性很好（近 30 天有 {0} 天在学），保持节奏。".Replace("{0}", Math.Round(dto.ConsistencyScore * 30).ToString("0")),
            >= 0.3 => "学习连续性中等，建议固定每天 20 分钟，比集中突击更有效。",
            _ => "学习偏集中突击，建议把任务拆小、每天推进一点。"
        });

        insights.Add(dto.RecentActivityLevel switch
        {
            >= 1.5 => "近一周比上周更活跃，趁势把薄弱点补上。",
            >= 0.7 => "近两周活跃度基本稳定。",
            _ => "近一周活跃度下降，建议先完成一条「学习推荐」找回节奏。"
        });

        if (weak.Count > 0)
        {
            var top = weak[0];
            insights.Add($"当前最薄弱的点是「{top.Name}」（掌握度 {Math.Round(top.Mastery * 100)}%），建议优先练它。");
        }

        return insights;
    }

    private async Task<Course?> FindCourseAsync(Guid courseId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await _courseRepository.FindAsync(courseId);
        }
    }

    private async Task<List<Guid>> GetEnrolledStudentIdsAsync(Guid courseId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _studentCourseRepository.GetQueryableAsync();
            return await AsyncExecuter.ToListAsync(
                query.Where(x => x.CourseId == courseId && x.Status != StudentCourseStatus.Dropped)
                    .Select(x => x.StudentId));
        }
    }

    private async Task<Dictionary<Guid, string?>> GetUserNamesAsync(IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0)
        {
            return new Dictionary<Guid, string?>();
        }

        List<IdentityUser> users;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            users = await _userRepository.GetListAsync(x => list.Contains(x.Id));
        }

        return users.ToDictionary(
            x => x.Id,
            x => string.IsNullOrWhiteSpace(x.Name) ? x.UserName : x.Name);
    }

    private async Task<string?> GetUserNameAsync(Guid studentId)
    {
        var map = await GetUserNamesAsync(new[] { studentId });
        return map.GetValueOrDefault(studentId);
    }

    /// <summary>行为类型中文标签（画像页与报告共用，避免两处不一致）。</summary>
    internal static string ActionLabel(StudentActivityAction action) => action switch
    {
        StudentActivityAction.StudyChapter => "章节学习",
        StudentActivityAction.AnswerExercise => "作答习题",
        StudentActivityAction.SubmitExam => "参加考试",
        StudentActivityAction.SubmitAssignment => "提交作业",
        StudentActivityAction.WatchVideo => "观看视频",
        StudentActivityAction.CheckIn => "课堂签到",
        StudentActivityAction.PostDiscussion => "讨论答疑",
        StudentActivityAction.SubmitGroupTask => "小组任务",
        StudentActivityAction.Practice => "课堂练习",
        _ => action.ToString()
    };

    /// <summary>报告小节编号：1 → 一、2 → 二…（顺序动态生成，避免缺节时跳号）</summary>
    private static string ToChineseNumber(int value) => value switch
    {
        1 => "一",
        2 => "二",
        3 => "三",
        4 => "四",
        5 => "五",
        6 => "六",
        7 => "七",
        8 => "八",
        9 => "九",
        10 => "十",
        _ => value.ToString()
    };
}
