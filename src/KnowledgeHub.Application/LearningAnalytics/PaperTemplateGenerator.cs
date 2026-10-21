using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using KnowledgeHub.Courses;
using KnowledgeHub.Exams;
using KnowledgeHub.Exams.Enums;
using KnowledgeHub.LearningAnalytics.Dtos;
using KnowledgeHub.LearningAnalytics.Enums;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 试卷样张生成器：按课程题库随机抽题、随机打分，生成一份 A4 排版的「纸质试卷」HTML。
///
/// 用途：自测 / 演示「纸质试卷分析」的 OCR 链路——把生成的 HTML 栅格化成 PNG，
/// 当作学生手写卷上传，系统即可走完整的 OCR → 知识点匹配 → 成绩分析流程。
///
/// 可配置项（appsettings → <c>LearningAnalytics:PaperGenerator</c>）：
/// DefaultQuestionCount / FullScorePerQuestion / RandomScore / IncludeAnswer /
/// MaxQuestionCount / ClassName / TitleTemplate
/// </summary>
public class PaperTemplateGenerator : ITransientDependency
{
    private const string InkColor = "#1c3d70";   // 学生蓝黑笔
    private const string RedColor = "#c0392b";   // 老师红笔
    private const string Print = "#1a1a1a";    // 印刷体

    private readonly IRepository<Course, Guid> _courseRepository;
    private readonly IRepository<Chapter, Guid> _chapterRepository;
    private readonly IRepository<Exercise, Guid> _exerciseRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IConfiguration _configuration;

    public PaperTemplateGenerator(
        IRepository<Course, Guid> courseRepository,
        IRepository<Chapter, Guid> chapterRepository,
        IRepository<Exercise, Guid> exerciseRepository,
        IGuidGenerator guidGenerator,
        IConfiguration configuration)
    {
        _courseRepository = courseRepository;
        _chapterRepository = chapterRepository;
        _exerciseRepository = exerciseRepository;
        _guidGenerator = guidGenerator;
        _configuration = configuration;
    }

    /// <summary>当前生效的生成器配置（前端据此预填表单）</summary>
    public PaperGeneratorConfigDto GetConfig() => new()
    {
        DefaultQuestionCount = Math.Clamp(ConfigInt("DefaultQuestionCount", 6), 1, ConfigInt("MaxQuestionCount", 30)),
        FullScorePerQuestion = Math.Clamp(ConfigInt("FullScorePerQuestion", 5), 1, 100),
        RandomScore = ConfigBool("RandomScore", true),
        IncludeAnswer = ConfigBool("IncludeAnswer", true),
        MaxQuestionCount = Math.Clamp(ConfigInt("MaxQuestionCount", 30), 1, 100),
        ClassName = _configuration["LearningAnalytics:PaperGenerator:ClassName"] ?? "计算机应用技术 2301 班",
        TitleTemplate = _configuration["LearningAnalytics:PaperGenerator:TitleTemplate"] ?? "{course} 期末试卷",
    };

    /// <summary>
    /// 生成一份试卷样张：题目从课程题库随机抽取，得分随机（可配置为全满分）。
    /// </summary>
    public async Task<PaperTemplateDto> GenerateAsync(GeneratePaperTemplateInput input)
    {
        if (input == null || input.CourseId == Guid.Empty)
        {
            throw new UserFriendlyException("请先选择课程");
        }

        var config = GetConfig();
        var course = await _courseRepository.FindAsync(input.CourseId);
        if (course == null)
        {
            throw new UserFriendlyException("课程不存在");
        }

        var courseName = string.IsNullOrWhiteSpace(course.Title) ? "未知课程" : course.Title;
        var title = string.IsNullOrWhiteSpace(input.Title)
            ? config.TitleTemplate.Replace("{course}", courseName)
            : input.Title!.Trim();

        var examDate = (input.ExamDate ?? DateTime.Today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var className = string.IsNullOrWhiteSpace(input.ClassName) ? config.ClassName : input.ClassName!.Trim();

        // 题目：课程题库（按章节顺序 + 建题时间），数量不足时全取，多余时随机抽取
        var exercises = await LoadExercisesAsync(input.CourseId);
        if (!exercises.Any())
        {
            throw new UserFriendlyException($"课程「{courseName}」还没有习题，无法生成试卷，请先建题");
        }

        var questionCount = Math.Clamp(
            input.QuestionCount ?? config.DefaultQuestionCount,
            1,
            Math.Max(1, Math.Min(config.MaxQuestionCount, exercises.Count)));

        var seed = input.Seed ?? Random.Shared.Next(1, int.MaxValue);
        var random = new Random(seed);

        var pool = exercises.ToList();
        // Fisher-Yates 部分洗牌：只洗前 questionCount 个
        for (var i = 0; i < questionCount && i < pool.Count; i++)
        {
            var j = random.Next(i, pool.Count);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        var picked = pool.Take(questionCount).ToList();

        var fullScoreBase = Math.Clamp(input.FullScorePerQuestion ?? config.FullScorePerQuestion, 1, 100);
        var randomScore = input.RandomScore ?? config.RandomScore;
        var includeAnswer = input.IncludeAnswer ?? config.IncludeAnswer;

        var questions = new List<PaperTemplateQuestionDto>();
        var sectionIndex = 0;
        string? lastType = null;
        var no = 0;

        foreach (var exercise in picked)
        {
            var typeName = TypeName(exercise.Type);
            if (typeName != lastType)
            {
                sectionIndex++;
                lastType = typeName;
            }

            no++;
            var fullScore = FullScoreOf(exercise, fullScoreBase);
            var score = randomScore ? random.Next(0, fullScore + 1) : fullScore;

            questions.Add(new PaperTemplateQuestionDto
            {
                No = no,
                Section = $"{SectionName(sectionIndex)}、{typeName}",
                TypeName = typeName,
                Text = QuestionText(exercise),
                Options = ParseOptions(exercise.Options),
                FullScore = fullScore,
                Score = score,
                Answer = AnswerText(exercise),
            });
        }

        // 全部得 0 分时，至少让一道题拿满分，避免卷面看起来像识别失败
        if (randomScore && questions.All(q => q.Score == 0))
        {
            questions[random.Next(questions.Count)].Score = questions.First().FullScore;
        }

        var studentName = input.StudentName?.Trim() ?? string.Empty;
        var studentNumber = input.StudentNumber?.Trim() ?? string.Empty;

        var template = new PaperTemplateDto
        {
            Title = title,
            CourseId = course.Id,
            CourseName = courseName,
            StudentName = studentName,
            StudentNumber = studentNumber,
            ClassName = className,
            ExamDate = examDate,
            QuestionCount = questions.Count,
            TotalFullScore = questions.Sum(q => q.FullScore),
            TotalScore = questions.Sum(q => q.Score),
            Questions = questions,
        };

        template.Html = BuildHtml(template, seed, includeAnswer);
        return template;
    }

    /// <summary>课程题库：按章节 sortOrder + 建题时间排序（与手工出卷习惯一致）</summary>
    private async Task<List<Exercise>> LoadExercisesAsync(Guid courseId)
    {
        var exercises = await _exerciseRepository.GetListAsync(x => x.CourseId == courseId);
        if (!exercises.Any())
        {
            return exercises;
        }

        var chapterIds = exercises
            .Where(x => x.ChapterId.HasValue)
            .Select(x => x.ChapterId!.Value)
            .Distinct()
            .ToList();

        var chapters = chapterIds.Any()
            ? (await _chapterRepository.GetListAsync(c => chapterIds.Contains(c.Id)))
                .ToDictionary(c => c.Id, c => c.SortOrder)
            : new Dictionary<Guid, int>();

        return exercises
            .OrderBy(x => x.ChapterId.HasValue && chapters.TryGetValue(x.ChapterId.Value, out var order) ? order : int.MaxValue)
            .ThenBy(x => x.CreationTime)
            .ToList();
    }

    /// <summary>分区序号：1 → 一、2 → 二、10 → 十、11 → 十一…（不能直接用码点加法，汉字数字在 Unicode 里不连续）</summary>
    private static string SectionName(int index)
    {
        string[] digits = { "一", "二", "三", "四", "五", "六", "七", "八", "九" };
        if (index <= 0) return string.Empty;
        if (index <= 9) return digits[index - 1];
        if (index == 10) return "十";
        if (index < 20) return "十" + digits[index - 11];
        return "二十" + digits[index - 21];
    }

    /// <summary>分值：基础分值（主观题翻倍）；不取题库的 Score（那是练习权重，不是卷面分值）</summary>
    private static int FullScoreOf(Exercise exercise, int baseScore)
    {
        return IsOpenEnded(exercise.Type) ? baseScore * 2 : baseScore;
    }

    private static bool IsOpenEnded(ExerciseType type) =>
        type is ExerciseType.ShortAnswer or ExerciseType.Essay or ExerciseType.CaseAnalysis
            or ExerciseType.TermExplanation or ExerciseType.Calculation;

    private static string TypeName(ExerciseType type) => type switch
    {
        ExerciseType.SingleChoice => "单选题",
        ExerciseType.MultiChoice => "多选题",
        ExerciseType.TrueFalse => "判断题",
        ExerciseType.FillBlank => "填空题",
        ExerciseType.ShortAnswer => "简答题",
        ExerciseType.Essay => "论述题",
        ExerciseType.CaseAnalysis => "案例分析题",
        ExerciseType.TermExplanation => "名词解释",
        ExerciseType.Calculation => "计算题",
        ExerciseType.Programming => "编程题",
        _ => "题目",
    };

    /// <summary>题干：优先用题目内容，没有就用标题</summary>
    private static string QuestionText(Exercise exercise)
    {
        var content = (exercise.QuestionContent ?? string.Empty).Trim();
        if (content.Length == 0)
        {
            content = (exercise.Title ?? string.Empty).Trim();
        }

        // 题库里常见的「答案：xxx / 解析：xxx」尾巴去掉，避免污染卷面
        content = Regex.Replace(content, @"\s*(答案|正确答案|解析|答案解析)[:：].*$", string.Empty, RegexOptions.Multiline);
        return string.IsNullOrWhiteSpace(content) ? "（未填写题干）" : content;
    }

    private static List<string> ParseOptions(string? options)
    {
        if (string.IsNullOrWhiteSpace(options))
        {
            return new List<string>();
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(options);
            return list?.Where(o => !string.IsNullOrWhiteSpace(o)).Select(NormalizeOption).ToList() ?? new List<string>();
        }
        catch
        {
            // 兼容 "A. xxx\nB. xxx" 这种纯文本选项
            return options
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => NormalizeOption(l.Trim()))
                .Where(l => l.Length > 0)
                .ToList();
        }
    }

    /// <summary>去掉选项自带的 "A. " 前缀，避免卷面出现 "A. A. xxx"</summary>
    private static string NormalizeOption(string option)
    {
        return Regex.Replace(option, @"^\s*[A-Ha-h]\s*[\.、\)）:：]\s*", string.Empty).Trim();
    }

    /// <summary>作答：选择题取正确选项字母，主观题取参考答案</summary>
    private static string AnswerText(Exercise exercise)
    {
        var answer = (exercise.Answer ?? string.Empty).Trim();
        if (answer.Length == 0)
        {
            return string.Empty;
        }

        if (exercise.Type is ExerciseType.SingleChoice or ExerciseType.MultiChoice or ExerciseType.TrueFalse)
        {
            var letters = answer
                .Split(new[] { ',', '，', ' ', '、' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim().ToUpperInvariant())
                .Where(a => a.Length == 1 && a[0] >= 'A' && a[0] <= 'Z')
                .Distinct()
                .ToList();
            if (letters.Any())
            {
                return string.Join("、", letters);
            }
        }

        return answer.Length > 60 ? answer[..60] : answer;
    }

    // ══════════════════════════════════════════════════════════
    //  HTML 渲染
    // ══════════════════════════════════════════════════════════

    private static string BuildHtml(PaperTemplateDto paper, int seed, bool includeAnswer)
    {
        // 手写体抖动：逐字随机旋转 + 垂直位移 + 缩放，模拟手写
        var random = new Random(seed);

        string Hand(string text, string color = InkColor, int? size = null)
        {
            var sb = new StringBuilder();
            sb.Append("<span class=\"hand\">");
            foreach (var ch in text)
            {
                var rot = random.NextDouble() * 5.2 - 2.6;
                var dy = random.NextDouble() * 2.2 - 1.1;
                var scale = 0.985 + random.NextDouble() * 0.03;
                sb.Append("<span style=\"color:").Append(color)
                    .Append(";display:inline-block;transform:rotate(")
                    .Append(rot.ToString("0.00", CultureInfo.InvariantCulture)).Append("deg) translateY(")
                    .Append(dy.ToString("0.00", CultureInfo.InvariantCulture)).Append("px) scale(")
                    .Append(scale.ToString("0.000", CultureInfo.InvariantCulture)).Append(")");
                if (size.HasValue)
                {
                    sb.Append(";font-size:").Append(size.Value).Append("px");
                }

                sb.Append("\">").Append(Html(ch.ToString())).Append("</span>");
            }

            sb.Append("</span>");
            return sb.ToString();
        }

        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\" />")
            .Append("<title>").Append(Html(paper.Title)).Append("</title><style>")
            .Append(@"
  @page { size: A4; margin: 16mm 15mm 14mm; }
  * { box-sizing: border-box; }
  html, body { margin: 0; padding: 0; }
  body {
    font-family: ""Hiragino Sans GB"", ""Songti SC"", ""Microsoft YaHei"", serif;
    color: ").Append(Print).Append(@";
    background: #fff;
    transform: rotate(-0.25deg);
    transform-origin: center;
  }
  .sheet {
    position: relative;
    padding: 6px 4px 10px;
    background: radial-gradient(120% 90% at 50% 40%, #ffffff 60%, #f3f1ec 100%);
  }
  .sheet::after {
    content: """";
    position: absolute; inset: 0;
    pointer-events: none;
    background: linear-gradient(90deg, rgba(0,0,0,.05), rgba(0,0,0,0) 6%, rgba(0,0,0,0) 94%, rgba(0,0,0,.05));
  }
  h1 { text-align: center; font-size: 23px; letter-spacing: 3px; margin: 4px 0 10px; font-weight: 700; }
  .meta {
    display: flex; flex-wrap: wrap; justify-content: space-between; gap: 2px 20px;
    font-size: 13.5px; padding-bottom: 6px; border-bottom: 1px solid #444; margin-bottom: 12px;
  }
  .field { display: inline-flex; gap: 6px; align-items: baseline; white-space: nowrap; }
  .field .label { color: #444; }
  .section-title { font-size: 15px; font-weight: 700; margin: 16px 0 8px; }
  .q { margin: 0 0 18px; page-break-inside: avoid; }
  .q-head { font-size: 14.5px; line-height: 1.95; }
  .q-score { margin-left: 30px; white-space: nowrap; }
  .opts { font-size: 13.5px; margin: 2px 0 0 30px; display: flex; gap: 30px; color: #333; flex-wrap: wrap; }
  .answer { margin: 4px 0 0 30px; font-size: 15px; line-height: 1.9; }
  .answer .label { color: #666; font-size: 13px; margin-right: 6px; }
  .hand { font-family: ""Hiragino Sans GB"", ""Songti SC"", serif; }
  .foot {
    margin-top: 18px; padding-top: 10px; border-top: 1px dashed #999;
    display: flex; justify-content: space-between; font-size: 13.5px; color: #555;
  }
").Append("</style></head><body><div class=\"sheet\">");

        sb.Append("<h1>").Append(Html(paper.Title)).Append("</h1>");
        sb.Append("<div class=\"meta\">")
            .Append("<span class=\"field\"><span class=\"label\">学号：</span>").Append(Html(paper.StudentNumber)).Append("</span>")
            .Append("<span class=\"field\"><span class=\"label\">姓名：</span>").Append(Hand(paper.StudentName)).Append("</span>")
            .Append("<span class=\"field\"><span class=\"label\">班级：</span>").Append(Html(paper.ClassName)).Append("</span>")
            .Append("<span class=\"field\"><span class=\"label\">考试日期：</span>").Append(Html(paper.ExamDate)).Append("</span>")
            .Append("<span class=\"field\"><span class=\"label\">课程：</span>").Append(Html(paper.CourseName)).Append("</span>")
            .Append("</div>");

        string? lastSection = null;
        foreach (var q in paper.Questions)
        {
            if (q.Section != lastSection)
            {
                sb.Append("<div class=\"section-title\">").Append(Html(q.Section)).Append("</div>");
                lastSection = q.Section;
            }

            sb.Append("<div class=\"q\"><div class=\"q-head\">")
                .Append("<span class=\"q-text\">").Append(q.No).Append(". ").Append(Html(q.Text))
                .Append("（").Append(q.FullScore).Append("分）</span>")
                .Append("<span class=\"q-score\">").Append(Hand($"得分：{q.Score}分", RedColor, 15)).Append("</span>")
                .Append("</div>");

            if (q.Options.Any())
            {
                sb.Append("<div class=\"opts\">");
                for (var i = 0; i < q.Options.Count; i++)
                {
                    sb.Append("<span>").Append((char)('A' + i)).Append(". ").Append(Html(q.Options[i])).Append("</span>");
                }

                sb.Append("</div>");
            }

            if (includeAnswer && !string.IsNullOrWhiteSpace(q.Answer))
            {
                sb.Append("<div class=\"answer\"><span class=\"label\">作答：</span>")
                    .Append(Hand(q.Answer, InkColor, 15)).Append("</div>");
            }

            sb.Append("</div>");
        }

        sb.Append("<div class=\"foot\">")
            .Append("<span>阅卷人签字：").Append(Hand("王老师", RedColor, 13)).Append("</span>")
            .Append("<span>总分：").Append(Hand($"{paper.TotalScore}分", RedColor, 13)).Append(" / ").Append(paper.TotalFullScore).Append(" 分</span>")
            .Append("</div>");

        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    private static string Html(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return System.Net.WebUtility.HtmlEncode(text);
    }

    // ── 配置读取 ──
    private int ConfigInt(string key, int fallback) =>
        _configuration.GetValue($"LearningAnalytics:PaperGenerator:{key}", fallback);

    private bool ConfigBool(string key, bool fallback) =>
        _configuration.GetValue($"LearningAnalytics:PaperGenerator:{key}", fallback);
}
