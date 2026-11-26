using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using KnowledgeHub.Exams.Dtos;
using KnowledgeHub.Exams.Enums;

namespace KnowledgeHub.Exams;

/// <summary>
/// 试卷 / 成绩单的打印 HTML（A4 纵向）。
/// <para>
/// 单独抽一层的原因：排版逻辑（题型标签、答题区高度、分页、页眉页脚）与业务取数无关，
/// 放在 AppService 里会把本来就不短的类再撑大，也不方便以后改成 Razor/模板文件。
/// </para>
/// </summary>
internal static class ExamPrintHtmlBuilder
{
    /// <summary>试卷：题目 + 答题区（不含答案与解析）。</summary>
    public static string BuildPaper(ExamDetailDto exam)
    {
        var sb = new StringBuilder();
        sb.Append(Head($"试卷 · {Escape(exam.Title)}"));

        sb.Append("<div class=\"hdr\">");
        sb.Append($"<div class=\"hdr__title\">{Escape(exam.Title)}</div>");
        sb.Append("<div class=\"hdr__meta\">");
        sb.Append($"<span>课程：{Escape(exam.CourseName ?? "—")}</span>");
        sb.Append($"<span>时长：{exam.DurationMinutes} 分钟</span>");
        sb.Append($"<span>总分：{exam.TotalScore} 分</span>");
        sb.Append($"<span>及格：{exam.PassingScore} 分</span>");
        sb.Append("</div>");
        sb.Append("<div class=\"hdr__blank\">姓名：____________　　学号：____________　　班级：____________　　得分：________</div>");
        sb.Append("</div>");

        var index = 1;
        foreach (var q in exam.Questions.OrderBy(x => x.SortOrder))
        {
            sb.Append("<div class=\"q\">");
            sb.Append($"<div class=\"q__head\"><span class=\"q__no\">{index}.</span>");
            sb.Append($"<span class=\"q__type\">[{TypeLabel(q.Type)}]</span>");
            sb.Append($"<span class=\"q__score\">({q.Score} 分)</span></div>");
            sb.Append($"<div class=\"q__body\">{FormatText(q.QuestionContent)}</div>");

            var options = ParseOptions(q.Options);
            if (options.Count > 0)
            {
                sb.Append("<div class=\"q__options\">");
                for (var i = 0; i < options.Count; i++)
                {
                    sb.Append($"<div class=\"q__opt\">{Letter(i)}. {FormatText(options[i])}</div>");
                }
                sb.Append("</div>");
            }

            sb.Append($"<div class=\"q__answer-area\" style=\"min-height:{AnswerHeight(q.Type)}\"></div>");
            sb.Append("</div>");
            index++;
        }

        sb.Append("<div class=\"foot\">—— 试卷结束 ——</div>");
        sb.Append(Tail());
        return sb.ToString();
    }

    /// <summary>成绩单：一行一个学生，按分数倒序。</summary>
    public static string BuildScores(ExamDetailDto exam, IReadOnlyList<ExamSubmissionItemDto> submissions)
    {
        var graded = submissions
            .Where(x => x.Status == ExamStatus.Graded)
            .OrderByDescending(x => x.TotalScore ?? 0)
            .ToList();

        var maxScore = graded.Count > 0
            ? Math.Max(graded.Max(x => x.MaxScore), exam.TotalScore)
            : exam.TotalScore;

        var avg = graded.Count > 0 ? graded.Average(x => x.TotalScore ?? 0) : 0;
        var passCount = graded.Count(x => (x.TotalScore ?? 0) >= exam.PassingScore);
        var passRate = graded.Count > 0 ? (double)passCount / graded.Count : 0;

        var sb = new StringBuilder();
        sb.Append(Head($"成绩单 · {Escape(exam.Title)}"));

        sb.Append("<div class=\"hdr\">");
        sb.Append($"<div class=\"hdr__title\">{Escape(exam.Title)} · 成绩单</div>");
        sb.Append("<div class=\"hdr__meta\">");
        sb.Append($"<span>课程：{Escape(exam.CourseName ?? "—")}</span>");
        sb.Append($"<span>满分：{maxScore}</span>");
        sb.Append($"<span>及格线：{exam.PassingScore}</span>");
        sb.Append($"<span>已交卷：{submissions.Count}</span>");
        sb.Append($"<span>已批阅：{graded.Count}</span>");
        sb.Append("</div>");
        sb.Append("<div class=\"hdr__blank\">");
        sb.Append($"平均分：{avg:F1}　　及格人数：{passCount}　　及格率：{passRate:P0}　　");
        sb.Append($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.Append("</div>");
        sb.Append("</div>");

        sb.Append("<table class=\"tbl\"><thead><tr>");
        sb.Append("<th style=\"width:48px\">序号</th>");
        sb.Append("<th>学生</th>");
        sb.Append("<th style=\"width:80px\">得分</th>");
        sb.Append("<th style=\"width:80px\">结果</th>");
        sb.Append("<th style=\"width:150px\">交卷时间</th>");
        sb.Append("<th style=\"width:150px\">批阅时间</th>");
        sb.Append("</tr></thead><tbody>");

        var index = 1;
        foreach (var item in graded)
        {
            var passed = (item.TotalScore ?? 0) >= exam.PassingScore;
            sb.Append("<tr>");
            sb.Append($"<td class=\"c\">{index}</td>");
            sb.Append($"<td>{Escape(item.StudentName ?? "—")}</td>");
            sb.Append($"<td class=\"c b\">{item.TotalScore ?? 0}</td>");
            sb.Append($"<td class=\"c {(passed ? "pass" : "fail")}\">{(passed ? "及格" : "不及格")}</td>");
            sb.Append($"<td class=\"c\">{item.SubmittedAt:MM-dd HH:mm}</td>");
            sb.Append($"<td class=\"c\">{item.GradedAt:MM-dd HH:mm}</td>");
            sb.Append("</tr>");
            index++;
        }

        if (graded.Count == 0)
        {
            sb.Append("<tr><td colspan=\"6\" class=\"c\">暂无已批阅的答卷</td></tr>");
        }

        sb.Append("</tbody></table>");

        // 未批阅 / 未交卷单独列出，避免"成绩单看起来人很少"被误读
        var pending = submissions.Where(x => x.Status != ExamStatus.Graded).ToList();
        if (pending.Count > 0)
        {
            sb.Append("<div class=\"note\">");
            sb.Append($"<div class=\"note__title\">待处理（{pending.Count} 人，未计入统计）</div>");
            sb.Append("<div class=\"note__list\">");
            foreach (var item in pending)
            {
                var status = item.SubmittedAt.HasValue ? "待批阅" : "未交卷";
                sb.Append($"<span class=\"note__chip\">{Escape(item.StudentName ?? "—")}（{status}）</span>");
            }
            sb.Append("</div></div>");
        }

        sb.Append(Tail());
        return sb.ToString();
    }

    // ── 样式与骨架 ────────────────────────────────────────────────

    private static string Head(string title) => $@"<!DOCTYPE html>
<html lang=""zh-CN""><head><meta charset=""utf-8"" /><title>{title}</title>
<style>
  * {{ box-sizing: border-box; }}
  body {{ font-family: ""Noto Sans CJK SC"", ""Noto Sans SC"", ""Source Han Sans SC"", ""Microsoft YaHei"", sans-serif;
         font-size: 12pt; line-height: 1.6; color: #111; margin: 0; }}
  .hdr {{ border: 1px solid #999; border-radius: 6px; padding: 10px 12px; margin-bottom: 14px; }}
  .hdr__title {{ font-size: 16pt; font-weight: 700; text-align: center; margin-bottom: 6px; }}
  .hdr__meta {{ display: flex; flex-wrap: wrap; gap: 14px; font-size: 10.5pt; color: #333; justify-content: center; }}
  .hdr__blank {{ margin-top: 8px; font-size: 11pt; text-align: center; color: #222; }}
  .q {{ margin: 0 0 12px; page-break-inside: avoid; }}
  .q__head {{ display: flex; gap: 6px; align-items: baseline; font-weight: 600; }}
  .q__no {{ min-width: 20px; }}
  .q__type {{ font-weight: 400; color: #555; font-size: 10.5pt; }}
  .q__score {{ color: #666; font-weight: 400; font-size: 10pt; }}
  .q__body {{ margin: 4px 0 6px; white-space: pre-wrap; }}
  .q__options {{ margin: 0 0 6px 18px; }}
  .q__opt {{ margin: 2px 0; }}
  .q__answer-area {{ border-bottom: 1px dotted #bbb; }}
  .tbl {{ width: 100%; border-collapse: collapse; font-size: 11pt; }}
  .tbl th, .tbl td {{ border: 1px solid #999; padding: 5px 6px; }}
  .tbl th {{ background: #f2f4f6; font-weight: 600; }}
  .tbl .c {{ text-align: center; }}
  .tbl .b {{ font-weight: 700; }}
  .tbl .pass {{ color: #1a7f43; }}
  .tbl .fail {{ color: #b3261e; }}
  .note {{ margin-top: 14px; font-size: 10.5pt; }}
  .note__title {{ font-weight: 600; margin-bottom: 6px; }}
  .note__list {{ display: flex; flex-wrap: wrap; gap: 6px; }}
  .note__chip {{ border: 1px solid #ccc; border-radius: 10px; padding: 1px 8px; }}
  .foot {{ margin-top: 18px; text-align: center; color: #777; font-size: 10pt; }}
  @page {{ size: A4 portrait; margin: 12mm; }}
</style></head><body>";

    private static string Tail() => "</body></html>";

    private static string TypeLabel(ExerciseType type) => type switch
    {
        ExerciseType.SingleChoice => "单选题",
        ExerciseType.MultiChoice => "多选题",
        ExerciseType.TrueFalse => "判断题",
        ExerciseType.FillBlank => "填空题",
        ExerciseType.ShortAnswer => "简答题",
        ExerciseType.Essay => "论述题",
        ExerciseType.CaseAnalysis => "案例分析",
        ExerciseType.TermExplanation => "名词解释",
        ExerciseType.Calculation => "计算题",
        ExerciseType.Programming => "编程题",
        _ => "题目"
    };

    /// <summary>按题型给答题区高度：客观题不留白，主观题按分值放大。</summary>
    private static string AnswerHeight(ExerciseType type) => type switch
    {
        ExerciseType.SingleChoice or ExerciseType.MultiChoice or ExerciseType.TrueFalse => "22px",
        ExerciseType.FillBlank => "26px",
        _ => "96px"
    };

    private static string Letter(int index) => index < 26 ? ((char)('A' + index)).ToString() : index.ToString();

    /// <summary>习题的 options 是 JSON 数组字符串（历史数据可能是换行文本）。</summary>
    private static List<string> ParseOptions(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<string>();
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                var arr = System.Text.Json.JsonSerializer.Deserialize<List<string>>(trimmed);
                return arr?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        return trimmed
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }

    private static string FormatText(string? text) =>
        Escape(text ?? string.Empty).Replace("\n", "<br/>");

    private static string Escape(string? value) => string.IsNullOrEmpty(value)
        ? string.Empty
        : value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
}
