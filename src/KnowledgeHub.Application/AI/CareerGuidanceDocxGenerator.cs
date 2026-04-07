using System;
using System.Collections.Generic;
using System.IO;
using KnowledgeHub.Application.AI.Dtos;
using NPOI.XWPF.UserModel;

namespace KnowledgeHub.Application.AI;

public static class CareerGuidanceDocxGenerator
{
    public static byte[] Generate(CareerGuidanceDto careerGuidance)
    {
        using var ms = new MemoryStream();
        var doc = new XWPFDocument();

        // Title
        var titlePara = doc.CreateParagraph();
        titlePara.Alignment = ParagraphAlignment.CENTER;
        var titleRun = titlePara.CreateRun();
        titleRun.SetText(careerGuidance.Title);
        titleRun.FontSize = 22;
        titleRun.IsBold = true;
        titleRun.FontFamily = "微软雅黑";

        // Assessment
        doc.CreateParagraph();
        AddHeading(doc, "个人能力评估");

        var scorePara = doc.CreateParagraph();
        scorePara.IndentationLeft = 360;
        var scoreLabel = scorePara.CreateRun();
        scoreLabel.SetText($"职业匹配度：{careerGuidance.Assessment.CareerMatchScore}%");
        scoreLabel.IsBold = true;
        scoreLabel.FontSize = 12;
        scoreLabel.FontFamily = "微软雅黑";

        var summaryPara = doc.CreateParagraph();
        summaryPara.IndentationLeft = 360;
        var summaryRun = summaryPara.CreateRun();
        summaryRun.SetText(careerGuidance.Assessment.Summary);
        summaryRun.FontSize = 11;
        summaryRun.FontFamily = "微软雅黑";

        AddBulletSection(doc, "核心优势", careerGuidance.Assessment.Strengths);
        AddBulletSection(doc, "待提升领域", careerGuidance.Assessment.AreasForImprovement);

        doc.CreateParagraph();

        // Recommended Paths
        AddHeading(doc, "推荐职业路径");

        foreach (var path in careerGuidance.RecommendedPaths)
        {
            var pathPara = doc.CreateParagraph();
            var pathRun = pathPara.CreateRun();
            pathRun.SetText($"{path.Title}（匹配度：{path.MatchScore}%）");
            pathRun.IsBold = true;
            pathRun.FontSize = 12;
            pathRun.FontFamily = "微软雅黑";

            var descPara = doc.CreateParagraph();
            descPara.IndentationLeft = 360;
            var descRun = descPara.CreateRun();
            descRun.SetText(path.Description);
            descRun.FontFamily = "微软雅黑";

            var pathTable = doc.CreateTable(1, 2);
            pathTable.SetColumnWidth(0, 3000);
            pathTable.SetColumnWidth(1, 9000);

            var row = pathTable.GetRow(0);
            SetCellText(row.GetCell(0), "薪资范围");
            SetCellText(row.GetCell(1), path.SalaryRange);

            AddTableRow(pathTable, "发展潜力", path.GrowthPotential);
            AddTableRow(pathTable, "所需技能", string.Join("、", path.RequiredSkills));

            doc.CreateParagraph();
        }

        // Skill Gaps
        AddHeading(doc, "技能差距分析");

        var gapTable = doc.CreateTable(1, 4);
        gapTable.SetColumnWidth(0, 2400);
        gapTable.SetColumnWidth(1, 2400);
        gapTable.SetColumnWidth(2, 2400);
        gapTable.SetColumnWidth(3, 1800);

        var headerRow = gapTable.GetRow(0);
        SetCellText(headerRow.GetCell(0), "技能");
        SetCellText(headerRow.GetCell(1), "当前水平");
        SetCellText(headerRow.GetCell(2), "目标水平");
        SetCellText(headerRow.GetCell(3), "优先级");

        foreach (var gap in careerGuidance.SkillGaps)
        {
            var dataRow = gapTable.CreateRow();
            SetCellText(dataRow.GetCell(0), gap.Skill);
            SetCellText(dataRow.GetCell(1), gap.CurrentLevel);
            SetCellText(dataRow.GetCell(2), gap.TargetLevel);
            SetCellText(dataRow.GetCell(3), gap.Priority);
        }

        doc.CreateParagraph();

        // Action Plan
        AddHeading(doc, "行动计划");

        foreach (var action in careerGuidance.ActionPlan)
        {
            var actionPara = doc.CreateParagraph();
            var actionRun = actionPara.CreateRun();
            actionRun.SetText($"[{action.Priority}] {action.Title}");
            actionRun.IsBold = true;
            actionRun.FontSize = 12;
            actionRun.FontFamily = "微软雅黑";

            var actionDescPara = doc.CreateParagraph();
            actionDescPara.IndentationLeft = 360;
            var actionDescRun = actionDescPara.CreateRun();
            actionDescRun.SetText(action.Description);
            actionDescRun.FontFamily = "微软雅黑";

            var timelinePara = doc.CreateParagraph();
            timelinePara.IndentationLeft = 360;
            var timelineLabel = timelinePara.CreateRun();
            timelineLabel.SetText($"时间线：{action.Timeline}");
            timelineLabel.FontSize = 11;
            timelineLabel.SetColor("666666");
            timelineLabel.FontFamily = "微软雅黑";

            doc.CreateParagraph();
        }

        // Next Steps
        AddBulletSection(doc, "下一步行动", careerGuidance.NextSteps);

        // Footer
        doc.CreateParagraph();
        var footerPara = doc.CreateParagraph();
        footerPara.Alignment = ParagraphAlignment.RIGHT;
        var footerRun = footerPara.CreateRun();
        footerRun.SetText($"生成日期：{DateTime.Now:yyyy-MM-dd}");
        footerRun.FontSize = 10;
        footerRun.SetColor("999999");
        footerRun.FontFamily = "微软雅黑";

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

        var borderPara = doc.CreateParagraph();
        borderPara.SpacingAfter = 100;
        var borderRun = borderPara.CreateRun();
        borderRun.SetText(new string('─', 60));
        borderRun.FontSize = 8;
        borderRun.SetColor("CCCCCC");
    }

    private static void AddBulletSection(XWPFDocument doc, string title, List<string> items)
    {
        var titlePara = doc.CreateParagraph();
        titlePara.IndentationLeft = 360;
        var titleRun = titlePara.CreateRun();
        titleRun.SetText($"{title}：");
        titleRun.IsBold = true;
        titleRun.FontSize = 11;
        titleRun.FontFamily = "微软雅黑";

        foreach (var item in items)
        {
            AddBulletItem(doc, item);
        }
    }

    private static void AddBulletItem(XWPFDocument doc, string text)
    {
        var para = doc.CreateParagraph();
        para.IndentationLeft = 720;
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

    private static void AddTableRow(XWPFTable table, string label, string value)
    {
        var row = table.CreateRow();
        SetCellText(row.GetCell(0), label);
        SetCellText(row.GetCell(1), value);
    }
}
