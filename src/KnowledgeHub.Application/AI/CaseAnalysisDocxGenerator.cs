using System;
using System.Collections.Generic;
using System.IO;
using KnowledgeHub.Application.AI.Dtos;
using NPOI.XWPF.UserModel;

namespace KnowledgeHub.Application.AI;

public static class CaseAnalysisDocxGenerator
{
    public static byte[] Generate(CaseAnalysisDto caseAnalysis)
    {
        using var ms = new MemoryStream();
        var doc = new XWPFDocument();

        // Title
        var titlePara = doc.CreateParagraph();
        titlePara.Alignment = ParagraphAlignment.CENTER;
        var titleRun = titlePara.CreateRun();
        titleRun.SetText(caseAnalysis.Title);
        titleRun.FontSize = 22;
        titleRun.IsBold = true;
        titleRun.FontFamily = "微软雅黑";

        // Summary
        doc.CreateParagraph();
        var summaryHeading = doc.CreateParagraph();
        var summaryRun = summaryHeading.CreateRun();
        summaryRun.SetText("案例摘要");
        summaryRun.FontSize = 14;
        summaryRun.IsBold = true;
        summaryRun.FontFamily = "微软雅黑";

        var summaryPara = doc.CreateParagraph();
        summaryPara.IndentationLeft = 360;
        var summaryContentRun = summaryPara.CreateRun();
        summaryContentRun.SetText(caseAnalysis.Summary);
        summaryContentRun.FontSize = 11;
        summaryContentRun.FontFamily = "微软雅黑";

        doc.CreateParagraph();

        // Background
        AddHeading(doc, "背景分析");

        var bgTable = doc.CreateTable(1, 2);
        bgTable.SetColumnWidth(0, 3000);
        bgTable.SetColumnWidth(1, 9000);

        var row = bgTable.GetRow(0);
        SetCellText(row.GetCell(0), "行业");
        SetCellText(row.GetCell(1), caseAnalysis.Background.Industry);

        AddTableRow(bgTable, "时间范围", caseAnalysis.Background.Timeframe);
        AddTableRow(bgTable, "场景背景", caseAnalysis.Background.Context);
        AddTableRow(bgTable, "相关方", string.Join("、", caseAnalysis.Background.Stakeholders));

        doc.CreateParagraph();

        // Key Issues
        AddHeading(doc, "关键问题");

        foreach (var issue in caseAnalysis.KeyIssues)
        {
            var issuePara = doc.CreateParagraph();
            var issueRun = issuePara.CreateRun();
            issueRun.SetText($"问题 {issue.Id}：{issue.Title}");
            issueRun.IsBold = true;
            issueRun.FontSize = 12;
            issueRun.FontFamily = "微软雅黑";

            var descPara = doc.CreateParagraph();
            descPara.IndentationLeft = 360;
            descPara.CreateRun().SetText($"描述：{issue.Description}");

            var impactPara = doc.CreateParagraph();
            impactPara.IndentationLeft = 360;
            var impactLabel = impactPara.CreateRun();
            impactLabel.SetText("影响：");
            impactLabel.IsBold = true;
            impactPara.CreateRun().SetText(issue.Impact);

            var severityPara = doc.CreateParagraph();
            severityPara.IndentationLeft = 360;
            var severityLabel = severityPara.CreateRun();
            severityLabel.SetText("严重程度：");
            severityLabel.IsBold = true;
            severityPara.CreateRun().SetText(issue.Severity);

            doc.CreateParagraph();
        }

        // Solutions
        AddHeading(doc, "解决方案");

        foreach (var solution in caseAnalysis.Solutions)
        {
            var solPara = doc.CreateParagraph();
            var solRun = solPara.CreateRun();
            solRun.SetText($"方案 {solution.Id}：{solution.Title}");
            solRun.IsBold = true;
            solRun.FontSize = 12;
            solRun.FontFamily = "微软雅黑";

            var solDescPara = doc.CreateParagraph();
            solDescPara.IndentationLeft = 360;
            solDescPara.CreateRun().SetText(solution.Description);

            if (solution.Steps.Count > 0)
            {
                var stepsLabel = doc.CreateParagraph();
                stepsLabel.IndentationLeft = 360;
                var stepsLabelRun = stepsLabel.CreateRun();
                stepsLabelRun.SetText("实施步骤：");
                stepsLabelRun.IsBold = true;

                for (int i = 0; i < solution.Steps.Count; i++)
                {
                    AddBulletItem(doc, $"{i + 1}. {solution.Steps[i]}");
                }
            }

            var outcomePara = doc.CreateParagraph();
            outcomePara.IndentationLeft = 360;
            var outcomeLabel = outcomePara.CreateRun();
            outcomeLabel.SetText("预期效果：");
            outcomeLabel.IsBold = true;
            outcomePara.CreateRun().SetText(solution.ExpectedOutcome);

            doc.CreateParagraph();
        }

        // Key Insights
        AddSection(doc, "关键洞察", caseAnalysis.KeyInsights);

        // Recommendations
        AddSection(doc, "建议", caseAnalysis.Recommendations);

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

    private static void AddSection(XWPFDocument doc, string title, List<string> items)
    {
        AddHeading(doc, title);
        foreach (var item in items)
        {
            AddBulletItem(doc, item);
        }
        doc.CreateParagraph();
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

    private static void AddTableRow(XWPFTable table, string label, string value)
    {
        var row = table.CreateRow();
        SetCellText(row.GetCell(0), label);
        SetCellText(row.GetCell(1), value);
    }
}
