using System;
using System.Collections.Generic;
using System.IO;
using KnowledgeHub.Application.AI.Dtos;
using NPOI.XWPF.UserModel;

namespace KnowledgeHub.Application.AI;

public static class LessonPlanDocxGenerator
{
    public static byte[] Generate(LessonPlanDto lessonPlan)
    {
        using var ms = new MemoryStream();
        var doc = new XWPFDocument();

        AppendTitle(doc, lessonPlan.Title);
        AppendInfoTable(doc, lessonPlan.Subject, lessonPlan.Grade, lessonPlan.Duration);
        AppendLessonPlanBody(doc, lessonPlan);

        doc.Write(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// 生成""课程总览 + 每章独立教案""的整体教案 DOCX。
    /// </summary>
    public static byte[] GenerateMultiChapter(MultiChapterLessonPlanDto plan)
    {
        using var ms = new MemoryStream();
        var doc = new XWPFDocument();

        // 封面标题
        AppendTitle(doc, string.IsNullOrWhiteSpace(plan.CourseTitle) ? "课程教案" : plan.CourseTitle);
        AppendInfoTable(doc, plan.Subject, plan.Grade, plan.Duration);

        // 课程总览
        if (plan.CourseObjectives.Count > 0)
        {
            AddSection(doc, "课程总体教学目标", plan.CourseObjectives);
        }

        // 各章节
        for (var i = 0; i < plan.Chapters.Count; i++)
        {
            var chapter = plan.Chapters[i];

            if (i > 0)
            {
                var breakPara = doc.CreateParagraph();
                breakPara.CreateRun().AddBreak(BreakType.PAGE);
            }

            var headingText = string.IsNullOrWhiteSpace(chapter.ChapterTitle)
                ? $"第 {chapter.Order} 章"
                : $"第 {chapter.Order} 章  {chapter.ChapterTitle}";

            var headingPara = doc.CreateParagraph();
            headingPara.SpacingBefore = 200;
            headingPara.SpacingAfter = 100;
            var headingRun = headingPara.CreateRun();
            headingRun.SetText(headingText);
            headingRun.FontSize = 16;
            headingRun.IsBold = true;
            headingRun.FontFamily = "微软雅黑";

            AppendLessonPlanBody(doc, chapter.LessonPlan);
        }

        doc.Write(ms);
        return ms.ToArray();
    }

    private static void AppendTitle(XWPFDocument doc, string title)
    {
        var titlePara = doc.CreateParagraph();
        titlePara.Alignment = ParagraphAlignment.CENTER;
        var titleRun = titlePara.CreateRun();
        titleRun.SetText(title);
        titleRun.FontSize = 22;
        titleRun.IsBold = true;
        titleRun.FontFamily = "微软雅黑";
    }

    private static void AppendInfoTable(XWPFDocument doc, string subject, string grade, int duration)
    {
        doc.CreateParagraph(); // blank line
        var infoTable = doc.CreateTable(1, 4);
        infoTable.SetColumnWidth(0, 3000);
        infoTable.SetColumnWidth(1, 3000);
        infoTable.SetColumnWidth(2, 3000);
        infoTable.SetColumnWidth(3, 3000);

        var row = infoTable.GetRow(0);
        SetCellText(row.GetCell(0), $"学科：{subject}");
        SetCellText(row.GetCell(1), $"年级：{grade}");
        SetCellText(row.GetCell(2), $"课时：{duration}分钟");
        SetCellText(row.GetCell(3), $"生成日期：{DateTime.Now:yyyy-MM-dd}");
    }

    private static void AppendLessonPlanBody(XWPFDocument doc, LessonPlanDto lessonPlan)
    {
        AddSection(doc, "教学目标", lessonPlan.Objectives);
        AddSection(doc, "教学重点", lessonPlan.KeyPoints);
        AddSection(doc, "教学难点", lessonPlan.Difficulties);

        AddHeading(doc, "教学环节");
        foreach (var section in lessonPlan.Sections)
        {
            var sectionPara = doc.CreateParagraph();
            var sectionRun = sectionPara.CreateRun();
            sectionRun.SetText($"{section.Name}（{section.Duration}分钟）");
            sectionRun.IsBold = true;
            sectionRun.FontSize = 12;
            sectionRun.FontFamily = "微软雅黑";

            var contentPara = doc.CreateParagraph();
            contentPara.CreateRun().SetText(section.Content);

            if (section.Activities.Count > 0)
            {
                var actPara = doc.CreateParagraph();
                var actRun = actPara.CreateRun();
                actRun.SetText("活动：");
                actRun.IsBold = true;

                foreach (var activity in section.Activities)
                {
                    AddBulletItem(doc, activity);
                }
            }

            doc.CreateParagraph(); // blank line
        }

        AddSection(doc, "教学方法", lessonPlan.Methods);
        AddSection(doc, "教学资源", lessonPlan.Resources);
        AddSection(doc, "评估方法", lessonPlan.Assessment);
        AddSection(doc, "课后作业", lessonPlan.Homework);
    }

    private static void AddHeading(XWPFDocument doc, string text)
    {
        var para = doc.CreateParagraph();
        para.SpacingAfter = 100;
        var run = para.CreateRun();
        run.SetText(text);
        run.FontSize = 14;
        run.IsBold = true;
        run.FontFamily = "微软雅黑";

        // Add a thin horizontal line
        var borderPara = doc.CreateParagraph();
        borderPara.SpacingAfter = 100;
        var borderRun = borderPara.CreateRun();
        borderRun.SetText(new string('─', 60));
        borderRun.FontSize = 8;
        borderRun.SetColor("CCCCCC");
    }

    private static void AddSection(XWPFDocument doc, string title, List<string> items)
    {
        AddHeading(doc, title);
        foreach (var item in items)
        {
            AddBulletItem(doc, item);
        }
        doc.CreateParagraph(); // blank line
    }

    private static void AddBulletItem(XWPFDocument doc, string text)
    {
        var para = doc.CreateParagraph();
        para.IndentationLeft = 360;
        var run = para.CreateRun();
        run.SetText($"• {text}");
        run.FontSize = 11;
        run.FontFamily = "微软雅黑";
    }

    private static void SetCellText(XWPFTableCell cell, string text)
    {
        var para = cell.Paragraphs[0];
        para.Alignment = ParagraphAlignment.CENTER;
        var run = para.CreateRun();
        run.SetText(text);
        run.FontSize = 11;
        run.FontFamily = "微软雅黑";
    }
}
