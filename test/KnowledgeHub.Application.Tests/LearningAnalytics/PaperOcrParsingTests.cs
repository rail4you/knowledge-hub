using System.Collections.Generic;
using System.Linq;
using KnowledgeHub.LearningAnalytics.Ocr;
using Shouldly;
using Xunit;

namespace KnowledgeHub.LearningAnalytics;

/// <summary>
/// 纸质试卷 OCR 解析回归测试。
///
/// 依据 scripts/paper-exam-sample 生成的《网络安全技术》试卷样张，
/// 校验两种 OCR 输出形态都能解析出正确的题号 / 得分 / 满分：
/// 1) 图片 OCR（qwen-vl-ocr）：模型按 JSON 输出；
/// 2) PDF 直传（qwen3.5-ocr）：模型按逐行转写输出。
/// </summary>
public class PaperOcrParsingTests
{
    /// <summary>样张卷面：6 道题，学生得分 5 / 5 / 5 / 7 / 0 / 5，满分 5 / 5 / 5 / 10 / 5 / 5。</summary>
    private const string Transcript = """
        网络安全技术 期末考试试卷
        学号：demo_student 姓名：张晨 班级：计算机应用技术 2301 班 考试日期：2026-10-20 课程：网络安全技术
        一、判断题
        1. HTTPS 使用 TLS 协议保证传输过程中的机密性与完整性。（5分） 得分：5分
        作答：正确
        二、单选题
        2. 以下属于对称加密算法的是？（5分） 得分：5分
        A. RSA B. AES C. ECDSA D. SHA-256
        作答：B
        三、填空题
        3. 数字签名主要保证数据的完整性和 ______ 性。（5分） 得分：5分
        作答：不可否认
        四、简答题
        4. 简述对称加密与非对称加密的区别及适用场景。（10分） 得分：7分
        作答：对称加密加解密用同一把密钥，速度快，适合大批量数据加密；非对称加密用公钥私钥对，速度慢，适合密钥交换和数字签名。
        五、单选题
        5. 防火墙的默认策略应遵循哪一原则？（5分） 得分：0分
        A. 全部允许 B. 最小权限 C. 随机放行 D. 按 IP 长度放行
        作答：A
        六、单选题
        6. 用于检测网络入侵行为的系统缩写是？（5分） 得分：5分
        A. IDS B. DNS C. DHCP D. NAT
        作答：A
        阅卷人签字：王老师 总分：27分 / 35 分
        """;

    [Fact]
    public void 图片OCR_JSON输出_应解析出题号_得分_满分()
    {
        const string json = """
            {
              "studentNumber": "demo_student",
              "studentName": "张晨",
              "totalScore": 27,
              "questions": [
                {"no": "1", "score": 5, "fullScore": 5, "rawText": "HTTPS 使用 TLS 协议保证传输过程"},
                {"no": "2", "score": 5, "fullScore": 5, "rawText": "以下属于对称加密算法的是"},
                {"no": "3", "score": 5, "fullScore": 5, "rawText": "数字签名主要保证数据的完整性和"},
                {"no": "4", "score": 7, "fullScore": 10, "rawText": "简述对称加密与非对称加密的区别"},
                {"no": "5", "score": 0, "fullScore": 5, "rawText": "防火墙的默认策略应遵循哪一原则"},
                {"no": "6", "score": 5, "fullScore": 5, "rawText": "用于检测网络入侵行为的系统缩写是"}
              ],
              "confidence": 0.92
            }
            """;

        var result = QwenPaperOcrRecognizer.ParseResult(json);

        result.Questions.Count.ShouldBe(6);
        result.StudentName.ShouldBe("张晨");
        result.StudentNumber.ShouldBe("demo_student");
        result.TotalScore.ShouldBe(27);
        result.Confidence.ShouldBeGreaterThan(0.8);

        // 题号顺序 = 题库顺序（章节 sortOrder + 建题时间），保证顺序匹配可用
        result.Questions.Select(q => q.QuestionNo).ShouldBe(new[] { "1", "2", "3", "4", "5", "6" });
        result.Questions.Select(q => q.Score!.Value).ShouldBe(new[] { 5d, 5d, 5d, 7d, 0d, 5d });
        result.Questions.Select(q => q.FullScore!.Value).ShouldBe(new[] { 5d, 5d, 5d, 10d, 5d, 5d });
    }

    [Fact]
    public void PDF转写输出_应解析出同样的题目与得分()
    {
        var result = QwenPaperOcrRecognizer.ParseTranscript(Transcript);

        result.ShouldNotBeNull();
        result!.StudentName.ShouldBe("张晨");
        result.StudentNumber.ShouldBe("demo_student");

        result.Questions.Count.ShouldBe(6);
        result.Questions.Select(q => q.QuestionNo).ShouldBe(new[] { "1", "2", "3", "4", "5", "6" });
        result.Questions.Select(q => q.Score!.Value).ShouldBe(new[] { 5d, 5d, 5d, 7d, 0d, 5d });
        result.Questions.Select(q => q.FullScore!.Value).ShouldBe(new[] { 5d, 5d, 5d, 10d, 5d, 5d });
    }

    [Fact]
    public void PDF转写输出_卷头与落款行不应被当成题目()
    {
        var result = QwenPaperOcrRecognizer.ParseTranscript(Transcript);

        result.ShouldNotBeNull();

        // 「总分：27分 / 35 分」「阅卷人签字」等落款行不产生题目
        result!.Questions.Any(q => q.RawText != null && q.RawText.Contains("总分")).ShouldBeFalse();
        result.Questions.Any(q => q.RawText != null && q.RawText.Contains("阅卷")).ShouldBeFalse();

        // 总分取题目得分之和（卷面落款行已被跳过）
        result.TotalScore.ShouldBe(27);
    }

    [Fact]
    public void 题号与得分成对提取_手写得分优先于印刷满分()
    {
        // 同一行里既有印刷满分（括号内）又有手写得分（括号外）
        var (score, full) = QwenPaperOcrRecognizer.ExtractScoresFromLine("1. 某题（10分） 得分：7分");
        score.ShouldBe(7);
        full.ShouldBe(10);

        // 只有印刷满分时，得分为空（交由人工校对）
        var (scoreOnly, fullOnly) = QwenPaperOcrRecognizer.ExtractScoresFromLine("2. 另一题（5分）");
        scoreOnly.ShouldBeNull();
        fullOnly.ShouldBe(5);
    }

    [Fact]
    public void 题号归一化_应去掉题干前缀并保留子题号()
    {
        QwenPaperOcrRecognizer.NormalizeQuestionNo("5(1)", 1).ShouldBe("5.1");
        QwenPaperOcrRecognizer.NormalizeQuestionNo("12. 下列说法正确的是", 1).ShouldBe("12");
        QwenPaperOcrRecognizer.NormalizeQuestionNo("2.192.168.1.0/24 网段", 1).ShouldBe("2");
    }
}
