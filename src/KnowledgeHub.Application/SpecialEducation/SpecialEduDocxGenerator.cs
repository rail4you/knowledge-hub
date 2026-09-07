using System.Collections.Generic;
using System.IO;
using System.Linq;
using KnowledgeHub.Application.SpecialEducation;
using KnowledgeHub.SpecialEducation.Dtos;
using NPOI.XWPF.UserModel;
using static KnowledgeHub.Application.SpecialEducation.SpecialTeachingDesignAppService;
using static KnowledgeHub.Application.SpecialEducation.SpecialIepAppService;

namespace KnowledgeHub.Application.SpecialEducation;

public static class SpecialEduDocxGenerator
{
    public static byte[] GenerateTeachingDesign(SpecialTeachingDesignParseResult doc)
    {
        using var ms = new MemoryStream();
        var d = new XWPFDocument();
        AddTitle(d, $"特殊教育教学设计方案：{doc.Title}");
        AddMeta(d, $"学科：{doc.Subject}  学段：{doc.Grade}  课时：{doc.Duration}分钟  日期：{System.DateTime.Now:yyyy-MM-dd}");
        AddSection(d, "教学目标", doc.Objectives);
        AddSection(d, "教学重点", doc.KeyPoints);
        AddSection(d, "教学难点", doc.Difficulties);
        AddHeading(d, "教学过程");
        foreach (var s in doc.Sections)
        {
            AddSubHeading(d, $"{s.Name}（{s.Duration}分钟）");
            AddPara(d, s.Content);
            foreach (var a in s.Activities) AddBullet(d, a);
        }
        AddSection(d, "教学评价", doc.Assessment);
        AddSection(d, "板书设计", doc.BoardDesign);
        AddSection(d, "教学方法", doc.Methods);
        AddSection(d, "教学资源", doc.Resources);
        AddSection(d, "课后作业", doc.Homework);
        AddSection(d, "配套课件大纲", doc.SlidesOutline);
        AddSection(d, "教学活动设计", doc.Activities);
        AddSection(d, "评估工具", doc.AssessmentTools);
        AddHeading(d, "课程标准依据");
        AddPara(d, doc.StandardBasis);
        AddPara(d, "说明：本方案为 AI 辅助草案，需教师审核确认后方可实施。");
        d.Write(ms);
        return ms.ToArray();
    }

    public static byte[] GenerateIep(IepParseResult doc, string studentName)
    {
        using var ms = new MemoryStream();
        var d = new XWPFDocument();
        AddTitle(d, $"IEP 个别化教育计划：{studentName}");
        AddHeading(d, "学生现状分析");
        AddPara(d, doc.ProfileSummary);
        AddSection(d, "长期目标", doc.LongTermGoals);
        AddSection(d, "短期目标", doc.ShortTermGoals);
        AddSection(d, "教学策略", doc.Strategies);
        AddSection(d, "评估方式", doc.Evaluation);
        AddSection(d, "家校协同", doc.HomeSchool);
        AddHeading(d, "法规依据");
        AddPara(d, doc.LegalBasis);
        AddPara(d, "说明：本方案为 AI 辅助草案，需评估团队与家长确认后实施，符合《残疾人教育条例》及特殊教育 IEP 相关规范要求。");
        d.Write(ms);
        return ms.ToArray();
    }

    public static byte[] GenerateResource(string title, List<string> content, string modality, List<SpecialEduResourceAppService.BraillePair>? pairs = null)
    {
        using var ms = new MemoryStream();
        var d = new XWPFDocument();
        AddTitle(d, $"{SpecialEduResourceAppService.ModalityDisplayName(modality)}：{title}");
        foreach (var c in content) AddBullet(d, c);
        if (pairs != null)
        {
            foreach (var p in pairs)
            {
                AddSubHeading(d, $"明文：{p.Text}" + (string.IsNullOrWhiteSpace(p.Pinyin) ? "" : $"（{p.Pinyin}）"));
                AddPara(d, $"盲文：{p.Braille}");
                if (!string.IsNullOrWhiteSpace(p.Note)) AddPara(d, $"点位说明：{p.Note}");
                d.CreateParagraph();
            }
        }
        AddPara(d, "说明：本资源为 AI 辅助生成，盲文点位须经教师核对后使用。");
        d.Write(ms);
        return ms.ToArray();
    }

    private static void AddTitle(XWPFDocument d, string text)
    {
        var p = d.CreateParagraph();
        p.Alignment = ParagraphAlignment.CENTER;
        var r = p.CreateRun();
        r.SetText(text); r.FontSize = 20; r.IsBold = true; r.FontFamily = "微软雅黑";
        d.CreateParagraph();
    }

    private static void AddMeta(XWPFDocument d, string text)
    {
        var p = d.CreateParagraph();
        p.Alignment = ParagraphAlignment.CENTER;
        var r = p.CreateRun();
        r.SetText(text); r.FontSize = 10; r.FontFamily = "微软雅黑";
        d.CreateParagraph();
    }

    private static void AddHeading(XWPFDocument d, string text)
    {
        var p = d.CreateParagraph();
        var r = p.CreateRun();
        r.SetText(text); r.FontSize = 14; r.IsBold = true; r.FontFamily = "微软雅黑";
    }

    private static void AddSubHeading(XWPFDocument d, string text)
    {
        var p = d.CreateParagraph();
        var r = p.CreateRun();
        r.SetText(text); r.FontSize = 12; r.IsBold = true; r.FontFamily = "微软雅黑";
    }

    private static void AddPara(XWPFDocument d, string text)
    {
        var p = d.CreateParagraph();
        var r = p.CreateRun();
        r.SetText(text ?? ""); r.FontSize = 11; r.FontFamily = "微软雅黑";
    }

    private static void AddSection(XWPFDocument d, string title, List<string> items)
    {
        AddHeading(d, title);
        if (items.Count == 0) AddPara(d, "（无）");
        foreach (var i in items) AddBullet(d, i);
        d.CreateParagraph();
    }

    private static void AddBullet(XWPFDocument d, string text)
    {
        var p = d.CreateParagraph();
        p.IndentationLeft = 360;
        var r = p.CreateRun();
        r.SetText($"• {text}"); r.FontSize = 11; r.FontFamily = "微软雅黑";
    }
}
