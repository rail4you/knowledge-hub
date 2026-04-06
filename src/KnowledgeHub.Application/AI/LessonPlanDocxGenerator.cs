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

        // Title
        var titlePara = doc.CreateParagraph();
        titlePara.Alignment = ParagraphAlignment.CENTER;
        var titleRun = titlePara.CreateRun();
        titleRun.SetText(lessonPlan.Title);
        titleRun.FontSize = 22;
        titleRun.IsBold = true;
        titleRun.FontFamily = "微软雅黑";

        // Basic info table
        doc.CreateParagraph(); // blank line
        var infoTable = doc.CreateTable(1, 4);
        infoTable.SetColumnWidth(0, 3000);
        infoTable.SetColumnWidth(1, 3000);
        infoTable.SetColumnWidth(2, 3000);
        infoTable.SetColumnWidth(3, 3000);

        var row = infoTable.GetRow(0);
        SetCellText(row.GetCell(0), $"学科：{lessonPlan.Subject}");
        SetCellText(row.GetCell(1), $"年级：{lessonPlan.Grade}");
        SetCellText(row.GetCell(2), $"课时：{lessonPlan.Duration}分钟");
        SetCellText(row.GetCell(3), $"生成日期：{DateTime.Now:yyyy-MM-dd}");

        // Objectives
        AddSection(doc, "教学目标", lessonPlan.Objectives);

        // Key Points
        AddSection(doc, "教学重点", lessonPlan.KeyPoints);

        // Difficulties
        AddSection(doc, "教学难点", lessonPlan.Difficulties);

        // Teaching sections
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

        // Methods
        AddSection(doc, "教学方法", lessonPlan.Methods);

        // Resources
        AddSection(doc, "教学资源", lessonPlan.Resources);

        // Assessment
        AddSection(doc, "评估方法", lessonPlan.Assessment);

        // Homework
        AddSection(doc, "课后作业", lessonPlan.Homework);

        doc.Write(ms);
        return ms.ToArray();
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
